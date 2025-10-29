using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kotoban.Core.Models;

namespace Kotoban.Core.Services.OpenAi
{
    /// <summary>
    /// AIによるコンテンツ生成を調整するサービス。
    /// </summary>
    public interface IAiContentService
    {
        /// <summary>
        /// AIを使用して指定されたエントリの解説文を生成します。
        /// </summary>
        /// <param name="entry">解説文を生成するエントリ。</param>
        /// <param name="newExplanationContext">生成に使用する新しいコンテキスト。</param>
        /// <returns>
        /// 生成された解説文とそのコンテキストを格納した <see cref="GeneratedExplanationResult"/>。
        /// </returns>
        Task<GeneratedExplanationResult> GenerateExplanationsAsync(Entry entry, string? newExplanationContext);

        /// <summary>
        /// AIを使用して指定されたエントリの画像を生成し、一時的に保存します。
        /// </summary>
        /// <param name="entry">画像を生成するエントリ。</param>
        /// <param name="newImageContext">生成に使用する新しい画像コンテキスト。</param>
        /// <param name="attemptNumber">生成試行回数。</param>
        /// <returns>
        /// 生成された画像の情報を格納した <see cref="GeneratedImageResult"/>。
        /// </returns>
        Task<GeneratedImageResult> GenerateImageAsync(Entry entry, string? newImageContext);
    }
}
