using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Services;

/// <summary>
/// 画像ファイルの管理とワークフローを提供するサービスの実装。
/// </summary>
public class ImageManager : IImageManager
{
    private readonly KotobanSettings _settings;

    /// <summary>
    /// 最終的な画像ファイルを保存するディレクトリの絶対パス。
    /// </summary>
    public string FinalImageDirectory { get; }

    /// <summary>
    /// 画像生成用の一時ファイルを保存するディレクトリの絶対パス。
    /// </summary>
    public string TempImageDirectory { get; }

    /// <summary>
    /// 最終的な画像ファイルの命名パターン。
    /// </summary>
    public string FinalImageFileNamePattern { get; }

    /// <summary>
    /// 一時画像ファイルの命名パターン。
    /// </summary>
    public string TempImageFileNamePattern { get; }

    /// <summary>
    /// ImageManagerの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="settings">Kotobanの設定</param>
    public ImageManager(KotobanSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.FinalImageDirectory))
        {
            throw new InvalidOperationException("FinalImageDirectory is required.");
        }
        if (string.IsNullOrWhiteSpace(settings.TempImageDirectory))
        {
            throw new InvalidOperationException("TempImageDirectory is required.");
        }
        if (string.IsNullOrWhiteSpace(settings.FinalImageFileNamePattern))
        {
            throw new InvalidOperationException("FinalImageFileNamePattern is required.");
        }
        if (string.IsNullOrWhiteSpace(settings.TempImageFileNamePattern))
        {
            throw new InvalidOperationException("TempImageFileNamePattern is required.");
        }

        _settings = settings;

        FinalImageDirectory = _settings.FinalImageDirectory;
        if (!Path.IsPathFullyQualified(FinalImageDirectory))
        {
            FinalImageDirectory = AppPath.GetAbsolutePath(FinalImageDirectory);
        }

        TempImageDirectory = _settings.TempImageDirectory;
        if (TempImageDirectory.Equals("%TEMP%", StringComparison.OrdinalIgnoreCase))
        {
            TempImageDirectory = Path.Combine(Path.GetTempPath(), "Kotoban", "Temp", "Images");
        }
        else if (!Path.IsPathFullyQualified(TempImageDirectory))
        {
            TempImageDirectory = AppPath.GetAbsolutePath(TempImageDirectory);
        }

        FinalImageFileNamePattern = _settings.FinalImageFileNamePattern;
        TempImageFileNamePattern = _settings.TempImageFileNamePattern;
    }

    /// <inheritdoc />
    public async Task<SavedImage?> StartImageEditingAsync(Entry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ImageFileName))
        {
            return null;
        }

        var currentImagePath = Path.Combine(FinalImageDirectory, entry.ImageFileName);
        if (!File.Exists(currentImagePath))
        {
            // その後すぐ画像をつくることが多いのでなんとかなりそうだし、
            // 例外を投げてしまうことで後続の処理に進めず、上書きで直るものも直らなくなってしまう。
            // しかし、データの整合性が失われているのは事実であり、厳密にやっていくなら見逃してはいけないこと。
            // まずは投げておき、しばらくアプリを使い、この例外が飛んでくるなら、原因をつぶしたい。
            throw new FileNotFoundException($"Final image file not found: {currentImagePath}");
        }

        // 一時ディレクトリに既存画像をコピー

        Directory.CreateDirectory(TempImageDirectory);

        var extension = Path.GetExtension(entry.ImageFileName);
        var tempFileName = string.Format(_settings.TempImageFileNamePattern, entry.Id, "0", extension);
        var tempImagePath = Path.Combine(TempImageDirectory, tempFileName);

        await FileUtils.CopyAsync(currentImagePath, tempImagePath, overwrite: true);

        var result = new SavedImage
        {
            FileName = tempFileName,
            ImageContext = entry.ImageContext,
            // これも落とすほどでないが、null だと何かがおかしいのは間違いないので。
            GeneratedAtUtc = entry.ImageGeneratedAtUtc ?? throw new InvalidOperationException("ImageGeneratedAtUtc is null"),
            ImagePrompt = entry.ImagePrompt
        };

        return result;
    }

    /// <inheritdoc />
    public async Task<SavedImage> SaveGeneratedImageAsync(
        Entry entry,
        byte[] imageBytes,
        string extension,
        int attemptNumber,
        string? imageContext,
        DateTime generatedAtUtc,
        string? imagePrompt)
    {
        Directory.CreateDirectory(TempImageDirectory);

        var tempFileName = string.Format(_settings.TempImageFileNamePattern, entry.Id, attemptNumber, extension);
        var tempImagePath = Path.Combine(TempImageDirectory, tempFileName);

        await File.WriteAllBytesAsync(tempImagePath, imageBytes);

        return new SavedImage
        {
            FileName = tempFileName,
            ImageContext = imageContext,
            GeneratedAtUtc = generatedAtUtc,
            ImagePrompt = imagePrompt
        };
    }

    /// <inheritdoc />
    public Task<string> FinalizeImageAsync(Entry entry, SavedImage selectedImage)
    {
        var tempImagePath = Path.Combine(TempImageDirectory, selectedImage.FileName);
        if (!File.Exists(tempImagePath))
        {
            throw new FileNotFoundException($"Temporary image file not found: {tempImagePath}");
        }

        Directory.CreateDirectory(FinalImageDirectory);

        var extension = Path.GetExtension(tempImagePath);
        var finalFileName = string.Format(_settings.FinalImageFileNamePattern, entry.Id, extension);
        var finalImagePath = Path.Combine(FinalImageDirectory, finalFileName);

        // ドライブをまたぐなどしなければ一瞬で終わるだろうから、非同期化は不要。
        File.Move(tempImagePath, finalImagePath, overwrite: true);

        return Task.FromResult(finalFileName);
    }

    /// <inheritdoc />
    public async Task CleanupTempImagesAsync(Guid? entryId)
    {
        if (!Directory.Exists(TempImageDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(TempImageDirectory);

        if (entryId.HasValue)
        {
            // 両方を小文字にしてからの比較の方が微妙に速いかもしれないが、
            // 個人的な好みとして、僅差なら、できるだけ動的（？）に実装したい。
            var entryIdString = entryId.Value.ToString();
            files = files.Where(f => Path.GetFileName(f).StartsWith(entryIdString, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        else
        {
            // entryId が null の場合、ファイル名の先頭36文字が GUID としてパースできるものだけ削除対象にする
            // 例: "123e4567-e89b-12d3-a456-426614174000-0.png"
            // GUID の "D" フォーマットはハイフンを含む36文字: 8-4-4-4-12
            files = files.Where(f =>
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(f);
                if (fileNameWithoutExtension.Length < 36)
                    return false;

                var guidSpan = fileNameWithoutExtension.AsSpan(0, 36);
                return Guid.TryParseExact(guidSpan, "D", out _);
            }).ToArray();
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

        return;
    }
}