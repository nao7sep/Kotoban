using Kotoban.Core.Models;
using Kotoban.Core.Utils;
using System.IO;
using System.Text;

namespace Kotoban.Core.Services
{
    /// <summary>
    /// ファイルからプロンプトフォーマットを読み込むサービス。
    /// </summary>
    public class PromptFormatProvider : IPromptFormatProvider
    {
        private readonly KotobanSettings _settings;

        /// <summary>
        /// 説明文生成用プロンプトフォーマットファイルのパス。
        /// </summary>
        public string ExplanationPromptFormatFile { get; }

        /// <summary>
        /// 画像生成用プロンプトフォーマットファイルのパス。
        /// </summary>
        public string ImagePromptFormatFile { get; }

        /// <summary>
        /// コンストラクタ。パスの検証と絶対パス変換を行う。
        /// </summary>
        /// <param name="settings">プロンプトファイルのパスを含む設定。</param>
        /// <exception cref="InvalidOperationException">パスが未設定の場合</exception>
        public PromptFormatProvider(KotobanSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ExplanationPromptFormatFile))
            {
                throw new InvalidOperationException("ExplanationPromptFormatFile is required.");
            }
            if (string.IsNullOrWhiteSpace(settings.ImagePromptFormatFile))
            {
                throw new InvalidOperationException("ImagePromptFormatFile is required.");
            }

            _settings = settings;

            ExplanationPromptFormatFile = _settings.ExplanationPromptFormatFile;
            if (!Path.IsPathFullyQualified(ExplanationPromptFormatFile))
            {
                ExplanationPromptFormatFile = AppPath.GetAbsolutePath(ExplanationPromptFormatFile);
            }

            ImagePromptFormatFile = _settings.ImagePromptFormatFile;
            if (!Path.IsPathFullyQualified(ImagePromptFormatFile))
            {
                ImagePromptFormatFile = AppPath.GetAbsolutePath(ImagePromptFormatFile);
            }
        }

        /// <summary>
        /// 説明文生成用プロンプトフォーマットをファイルから取得する。
        /// </summary>
        /// <returns>説明文生成用プロンプトフォーマットの文字列。</returns>
        public async Task<string> GetExplanationPromptFormatAsync()
        {
            // 開発中の利便性を考慮し、プロンプトファイルはキャッシュせず、常にファイルから直接読み込みます。
            // これにより、アプリケーションを再起動することなくプロンプトの変更を即座に反映できます。
            return await File.ReadAllTextAsync(ExplanationPromptFormatFile, Encoding.UTF8).ConfigureAwait(false);
        }

        /// <summary>
        /// 画像生成用プロンプトフォーマットをファイルから取得する。
        /// </summary>
        /// <returns>画像生成用プロンプトフォーマットの文字列。</returns>
        public async Task<string> GetImagePromptFormatAsync()
        {
            // 開発中の利便性を考慮し、プロンプトファイルはキャッシュせず、常にファイルから直接読み込みます。
            // これにより、アプリケーションを再起動することなくプロンプトの変更を即座に反映できます。
            return await File.ReadAllTextAsync(ImagePromptFormatFile, Encoding.UTF8).ConfigureAwait(false);
        }
    }
}
