using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
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
