using System;
using System.Collections.Generic;

namespace Kotoban.Core.Utils
{
    public static class WebUtils
    {
        /// <summary>
        /// 主要な画像Content-Typeと拡張子の対応表。
        /// キーはContent-Type、値はドット付き拡張子（例: ".png"）。外部からも参照可能。
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ImageContentTypeToExtensionMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "image/png", ".png" },
            { "image/jpeg", ".jpg" },
            { "image/jpg", ".jpg" },
            { "image/gif", ".gif" },
            { "image/bmp", ".bmp" },
            { "image/webp", ".webp" },
            { "image/tiff", ".tiff" },
            { "image/x-icon", ".ico" },
            { "image/svg+xml", ".svg" }
        };

        /// <summary>
        /// 画像のContent-Typeからファイル拡張子（ドット付き）を取得します。
        /// 未対応の場合は fallbackExtension を返します。
        /// </summary>
        /// <param name="contentType">MIME Content-Type 文字列（例: "image/png"）</param>
        /// <param name="fallbackExtension">Content-Type が未対応の場合に返す拡張子。デフォルトは ".png"。</param>
        /// <returns>ドット付きのファイル拡張子（例: ".png"）</returns>
        public static string GetImageFileExtensionFromContentType(string? contentType, string fallbackExtension = ".png")
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return fallbackExtension;

            if (ImageContentTypeToExtensionMap.TryGetValue(contentType, out var ext))
                return ext;

            return fallbackExtension;
        }
    }
}
