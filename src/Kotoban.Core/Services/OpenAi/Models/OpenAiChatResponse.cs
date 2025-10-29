using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
    /// <summary>
    /// OpenAI Chat API のレスポンスを表すモデルクラスです。
    /// </summary>
    public class OpenAiChatResponse : OpenAiApiObjectBase
    {
        /// <summary>
        /// レスポンスの ID。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// レスポンスタイプ。
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        /// <summary>
        /// 作成日時（UNIXタイムスタンプ）。
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; }

        /// <summary>
        /// 生成されたメッセージのリスト。
        /// </summary>
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoice> Choices { get; set; } = new();

        /// <summary>
        /// 使用されたトークン数などの情報。
        /// </summary>
        [JsonPropertyName("usage")]
        public OpenAiChatUsage? Usage { get; set; }
    }

    /// <summary>
    /// OpenAI Chat API の選択肢（生成結果）を表すモデルクラスです。
    /// </summary>
    public class OpenAiChatChoice : OpenAiApiObjectBase
    {
        /// <summary>
        /// 選択肢のインデックス。
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// 生成されたメッセージ。
        /// </summary>
        [JsonPropertyName("message")]
        public OpenAiChatMessage Message { get; set; } = new();

        /// <summary>
        /// 終了理由（stop, length など）。
        /// </summary>
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    /// <summary>
    /// OpenAI Chat API の使用量情報を表すモデルクラスです。
    /// </summary>
    public class OpenAiChatUsage : OpenAiApiObjectBase
    {
        /// <summary>
        /// プロンプトトークン数。
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        /// <summary>
        /// コンプレーショントークン数。
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        /// <summary>
        /// 合計トークン数。
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
