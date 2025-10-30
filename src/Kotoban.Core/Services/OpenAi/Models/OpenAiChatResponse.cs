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
}
