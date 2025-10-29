using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Services
{
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
                // データの整合性が失われているため、本来はエラーとして処理すべき状況です。
                // しかし、この直後に新しい画像を生成して上書きするワークフローが多いため、
                // ここで例外を投げると、正常な回復処理を妨げてしまう可能性があります。
                // ログには記録し、開発中にこの問題が頻発するようであれば、根本的な原因を調査する必要があります。
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
                // 既存の画像データであるため、ImageGeneratedAtUtc は null であってはなりません。
                // null の場合はデータ不整合を示唆するため、InvalidOperationException をスローします。
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

            // 同一ドライブ内のファイル移動は高速に完了するため、同期的に処理します。
            File.Move(tempImagePath, finalImagePath, overwrite: true);

            return Task.FromResult(finalFileName);
        }

        /// <inheritdoc />
        /// <inheritdoc />
    #pragma warning disable CS1998 // この非同期メソッドには 'await' 演算子がないため、同期的に実行されます
        public async Task CleanupTempImagesAsync(Guid? entryId)
    #pragma warning restore CS1998
        {
            if (!Directory.Exists(TempImageDirectory))
            {
                return;
            }

            var files = Directory.GetFiles(TempImageDirectory);

            if (entryId.HasValue)
            {
                // 大文字と小文字を区別せずに、ファイル名が entryId で始まるファイルを対象とします。
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
                // ファイルが他のプロセスによってロックされている場合、IOException が発生する可能性があります。
                // このメソッドはUIと分離されており、ユーザーに再試行を促すなどの対話ができないため、
                // 想定される一部の例外を抑制し、処理を継続します。
                // クリーンアップはベストエフォートで実行され、失敗したファイルは次回の実行時に再試行されることを期待します。

                try
                {
                    File.Delete(file);
                }
                catch (DirectoryNotFoundException)
                {
                    // ディレクトリが存在しない場合、GetFiles でファイルが返されないため、この例外は発生しないはずです。
                }
                catch (FileNotFoundException)
                {
                    // GetFiles で取得したファイルが存在しない場合、この例外は発生しないはずです。
                }
                catch (UnauthorizedAccessException)
                {
                    // ファイルへのアクセス権がない場合、通常はファイルの作成もできないため、この例外は発生しないはずです。
                }
                catch (IOException)
                {
                    // 他のプロセスがファイルをロックしている場合など、一時的なファイルアクセスの問題は無視します。
                }
            }

            return;
        }
    }
}
