using System;
using System.Collections.Generic;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// AIによる説明生成の結果と、その生成に使用されたコンテキストを保持します。
    /// </summary>
    public class GeneratedExplanationResult
    {
        /// <summary>
        /// 説明生成に使用されたコンテキスト。
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// 生成された難易度別の説明。
        /// </summary>
        public Dictionary<ExplanationLevel, string> Explanations { get; set; } = [];
    }
}
