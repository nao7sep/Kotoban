using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
    /// <summary>
    /// OpenAI 画像生成 API のレスポンスを表すモデルクラスです。
    /// </summary>
    public class OpenAiImageResponse : OpenAiApiObjectBase
    {
        /// <summary>
        /// 作成日時（UNIXタイムスタンプ）。
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; }

        /// <summary>
        /// 生成された画像データのリスト。
        /// </summary>
        [JsonPropertyName("data")]
        public List<OpenAiImageData> Data { get; set; } = new();
    }
}
