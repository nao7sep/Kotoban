using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
    /// <summary>
    /// OpenAI 画像生成 API へのリクエストを表すモデルクラスです。
    /// </summary>
    public class OpenAiImageRequest : OpenAiApiObjectBase
    {
        /// <summary>
        /// 使用するモデル名。
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 画像生成のためのプロンプト。
        /// </summary>
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// 画像の生成枚数。
        /// </summary>
        [JsonPropertyName("n")]
        public int? N { get; set; }

        /// <summary>
        /// 画像の品質（例: "standard", "hd"）。
        /// </summary>
        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        /// <summary>
        /// 画像サイズ（例: "1024x1024"）。
        /// </summary>
        [JsonPropertyName("size")]
        public string? Size { get; set; }

        /// <summary>
        /// 画像のスタイル（例: "vivid", "natural"）。
        /// </summary>
        [JsonPropertyName("style")]
        public string? Style { get; set; }

        /// <summary>
        /// レスポンス形式（"url" または "b64_json"）。
        /// </summary>
        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }
    }
}
