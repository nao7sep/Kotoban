using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
    /// <summary>
    /// OpenAI API のエラーレスポンスを表すモデルクラスです。
    /// </summary>
    public class OpenAiErrorResponse : OpenAiApiObjectBase
    {
        /// <summary>
        /// エラー情報。
        /// </summary>
        [JsonPropertyName("error")]
        public OpenAiError? Error { get; set; }
    }
}
