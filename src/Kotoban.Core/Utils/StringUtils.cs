using System;
using System.Collections.Generic;
using System.Linq;

namespace Kotoban.Core.Utils
{
    /// <summary>
    /// 文字列処理に関するユーティリティメソッドを提供します。
    /// </summary>
    public static class StringUtils
    {
        /// <summary>
        /// 複数行文字列を正規化します。
        /// </summary>
        /// <param name="value">処理する文字列。</param>
        /// <returns>正規化された行のシーケンス。</returns>
        public static IEnumerable<string> NormalizeMultilineString(string value)
        {
            if (value == null)
            {
                yield break;
            }

            using var reader = new StringReader(value);

            string? line;
            bool foundMeaningfulLine = false;
            bool foundEmptyLine = false;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmedLine = line.TrimEnd();

                if (trimmedLine.Length == 0)
                {
                    if (foundMeaningfulLine)
                    {
                        // 意味のある行が出現した後に初めて空行を記録する
                        foundEmptyLine = true;
                    }
                    // else: 先頭の空行は無視する
                }
                else
                {
                    if (foundEmptyLine)
                    {
                        // 直前に記録された空行があれば、ここで一度だけ出力する
                        yield return string.Empty;
                        foundEmptyLine = false;
                    }

                    yield return trimmedLine;
                    foundMeaningfulLine = true;
                }
            }
        }
    }
}
