using System;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// 画像生成結果を表すDTO。
    /// </summary>
    public class GeneratedImageResult
    {
        /// <summary>
        /// 画像生成時のコンテキスト（null の場合あり）。
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// 画像のバイト配列。
        /// </summary>
        public byte[] ImageBytes { get; set; } = [];

        /// <summary>
        /// 画像の拡張子（例: ".png"）。
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// 実際に使用された画像プロンプト（null の場合あり）。
        /// </summary>
        public string? ImagePrompt { get; set; }
    }
}
