using System;
using System.Collections.Generic;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// 1回分のAI説明生成結果とそのコンテキストを保持するクラス。
    /// </summary>
    public class GeneratedExplanationResult
    {
        /// <summary>
        /// 説明生成時のコンテキスト（null の場合あり）。
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// 生成された説明（レベルごとの辞書）。
        /// </summary>
        public Dictionary<ExplanationLevel, string> Explanations { get; set; } = [];
    }
}
