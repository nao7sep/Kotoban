using System;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// AIによる画像生成の結果を格納します。
    /// </summary>
    public class GeneratedImageResult
    {
        /// <summary>
        /// 画像生成に使用されたコンテキスト。
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// 画像のバイナリデータ。
        /// </summary>
        public byte[] ImageBytes { get; set; } = [];

        /// <summary>
        /// 画像のファイル拡張子（例: ".png"）。
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// 画像の生成に使用されたプロンプト。
        /// </summary>
        public string? ImagePrompt { get; set; }
    }
}
