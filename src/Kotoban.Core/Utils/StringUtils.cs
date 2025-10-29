using System;
using System.Collections.Generic;
using System.Linq;

namespace Kotoban.Core.Utils
{
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
                    if (!foundMeaningfulLine)
                    {
                        // 意味のある行が現れる前の意味のない行は無視される
                    }

                    else
                    {
                        // 空行が見つかったことを記録。
                        // 次に意味のある行が現れたときに空行を一つだけ追加する。
                        foundEmptyLine = true;
                    }
                }

                else
                {
                    if (foundEmptyLine)
                    {
                        // 直前の空行が記録されていれば、一つだけ追加する。
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
