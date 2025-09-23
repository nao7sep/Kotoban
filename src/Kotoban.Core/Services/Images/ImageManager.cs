using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Services.Images;

/// <summary>
/// 画像ファイルの管理とワークフローを提供するサービスの実装。
/// </summary>
public class ImageManager : IImageManager
{
    private readonly KotobanSettings _settings;
    private readonly string _finalImageDirectory;
    private readonly string _tempImageDirectory;

    /// <summary>
    /// ImageManagerの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="settings">Kotobanの設定</param>
    /// <param name="finalImageDirectory">最終画像ディレクトリの絶対パス</param>
    /// <param name="tempImageDirectory">一時画像ディレクトリの絶対パス</param>
    public ImageManager(KotobanSettings settings, string finalImageDirectory, string tempImageDirectory)
    {
        _settings = settings;
        _finalImageDirectory = finalImageDirectory;
        _tempImageDirectory = tempImageDirectory;
    }

    /// <inheritdoc />
    public Task<GeneratedImage?> StartImageEditingAsync(Entry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RelativeImagePath))
        {
            return Task.FromResult<GeneratedImage?>(null);
        }

        var currentImagePath = Path.Combine(_finalImageDirectory, entry.RelativeImagePath);
        if (!File.Exists(currentImagePath))
        {
            // その後すぐ画像をつくることが多いのでなんとかなりそうだし、
            // 例外を投げてしまうことで後続の処理に進めず、上書きで直るものも直らなくなってしまう。
            // しかし、データの整合性が失われているのは事実であり、厳密にやっていくなら見逃してはいけないこと。
            // まずは投げておき、しばらくアプリを使い、この例外が飛んでくるなら、原因をつぶしたい。
            throw new FileNotFoundException($"Final image file not found: {currentImagePath}");
        }

        // 一時ディレクトリに既存画像をコピー

        Directory.CreateDirectory(_tempImageDirectory);

        var extension = Path.GetExtension(entry.RelativeImagePath);
        var tempFileName = string.Format(_settings.TempImageFileNamePattern, entry.Id, "0", extension);
        var tempImagePath = Path.Combine(_tempImageDirectory, tempFileName);

        File.Copy(currentImagePath, tempImagePath, overwrite: true);

        var result = new GeneratedImage
        {
            RelativeImagePath = Path.GetRelativePath(_tempImageDirectory, tempImagePath),
            ImageContext = entry.ImageContext,
            // これも落とすほどでないが、null だと何かがおかしいのは間違いないので。
            GeneratedAtUtc = entry.ImageGeneratedAtUtc ?? throw new InvalidOperationException("ImageGeneratedAtUtc is null"),
            ImagePrompt = entry.ImagePrompt
        };

        return Task.FromResult<GeneratedImage?>(result);
    }

    /// <inheritdoc />
    public async Task<GeneratedImage> SaveGeneratedImageAsync(
        Entry entry,
        byte[] imageData,
        string extension,
        int attemptNumber,
        string? imageContext,
        DateTime generatedAtUtc,
        string? imagePrompt)
    {
        Directory.CreateDirectory(_tempImageDirectory);

        var tempFileName = string.Format(_settings.TempImageFileNamePattern, entry.Id, attemptNumber, extension);
        var tempImagePath = Path.Combine(_tempImageDirectory, tempFileName);

        await File.WriteAllBytesAsync(tempImagePath, imageData);

        return new GeneratedImage
        {
            RelativeImagePath = Path.GetRelativePath(_tempImageDirectory, tempImagePath),
            ImageContext = imageContext,
            GeneratedAtUtc = generatedAtUtc,
            ImagePrompt = imagePrompt
        };
    }

    /// <inheritdoc />
    public Task<string> FinalizeImageAsync(Entry entry, GeneratedImage selectedImage)
    {
        var tempImagePath = Path.Combine(_tempImageDirectory, selectedImage.RelativeImagePath);
        if (!File.Exists(tempImagePath))
        {
            throw new FileNotFoundException($"Temporary image file not found: {tempImagePath}");
        }

        Directory.CreateDirectory(_finalImageDirectory);

        var extension = Path.GetExtension(tempImagePath);
        var finalFileName = string.Format(_settings.FinalImageFileNamePattern, entry.Id, extension);
        var finalImagePath = Path.Combine(_finalImageDirectory, finalFileName);

        File.Move(tempImagePath, finalImagePath, overwrite: true);

        var relativePath = Path.GetRelativePath(_finalImageDirectory, finalImagePath);
        return Task.FromResult(relativePath);
    }

    /// <inheritdoc />
    public Task CleanupTempImagesAsync(Guid? entryId)
    {
        if (!Directory.Exists(_tempImageDirectory))
        {
            return Task.CompletedTask;
        }

        var files = Directory.GetFiles(_tempImageDirectory);

        if (entryId.HasValue)
        {
            // 両方を小文字にしてからの比較の方が微妙に速いかもしれないが、
            // 個人的な好みとして、僅差なら、できるだけ動的（？）に実装したい。
            var entryIdString = entryId.Value.ToString();
            files = files.Where(f => Path.GetFileName(f).StartsWith(entryIdString, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        foreach (var file in files)
        {
            // 画像のプレビューに使っているアプリが画像をロックしていれば、ここではふつうに例外が飛ぶ。
            // ここで「消せません」を表示して、再試行かキャンセルかを選んでもらうなどは、
            // ユーザービリティーを大きく高めない一方で、ライブラリーの設計を崩す。
            //
            // というのも、このクラスは UI から separate されているもので、
            // SoC を保ったまま UI ロジックを入れるとなると、
            // こちらのコードを複数のメソッドに細分化した上で Program.cs のコードと癒着させることになる。
            //
            // 次善策として、起こりえない例外およびユーザーの正常な操作に起因する例外のみ抑制する。

            try
            {
                File.Delete(file);
            }
            catch (DirectoryNotFoundException)
            {
                // ディレクトリーがなければ、削除のプロセスに入らないはず。
            }
            catch (FileNotFoundException)
            {
                // ファイルがなければ、Directory.GetFiles で得られないはず。
            }
            catch (UnauthorizedAccessException)
            {
                // 権限の関係で消せないなら、そもそもつくれなかったはず。
            }
            catch (IOException)
            {
                // ほかのアプリが開いているなどで消せないなら、いったんスルー。
                // ユーザーがそのうち気づき、使うアプリをかえるとか、ワークフローをかえるとかを期待。
                // プログラム終了時にもクリーンアップが行われるだろうから、累積的な問題にはならないはず。
            }
        }

        return Task.CompletedTask;
    }
}