using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
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
}
