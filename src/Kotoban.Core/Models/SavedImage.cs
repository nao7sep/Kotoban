using System;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// 保存された画像に関する情報を格納します。
    /// </summary>
    public class SavedImage
    {
        /// <summary>
        /// 画像のファイル名（拡張子を含む）。
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 画像生成に使用されたコンテキスト。
        /// </summary>
        public string? ImageContext { get; set; }

        /// <summary>
        /// 画像が生成されたUTCタイムスタンプ。
        /// </summary>
        public DateTime GeneratedAtUtc { get; set; }

        /// <summary>
        /// AIによって修正され、実際に画像生成に使用された最終的なプロンプト。
        /// </summary>
        public string? ImagePrompt { get; set; }
    }
}
