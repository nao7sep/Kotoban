using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi
{
    /// <summary>
    /// OpenAI API との通信用の基本的な JsonSerializerOptions を提供します。
    /// </summary>
    public static class OpenAiApiJsonOptions
    {
        /// <summary>
        /// OpenAI API リクエストの基本的なシリアライズオプションを取得します。
        /// スネークケースの命名規則を使用し、null 値を無視します。
        /// </summary>
        public static JsonSerializerOptions BaseRequestSerializationOptions { get; }

        /// <summary>
        /// OpenAI API レスポンスの基本的なデシリアライズオプションを取得します。
        /// スネークケースの命名規則を使用します。
        /// </summary>
        public static JsonSerializerOptions BaseResponseDeserializationOptions { get; }

        static OpenAiApiJsonOptions()
        {
            BaseRequestSerializationOptions = new JsonSerializerOptions
            {
                // API に送るデータなので、少しでも小さく。
                // null まみれのデータだと、かなり大きな差になる。
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                // API に送るだけのもので、自分が見ることは稀。
                // Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

                // structured model outputs には、additionalProperties というパラメーターが必要。
                // これは、_ が含まれていたり、大文字・小文字が違っていたりだと認識されない。
                // これに対応するため、全てのプロパティーに JsonPropertyName 属性をつけた。
                // PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

                // 自分が見ることは稀だが、リクエストもレスポンスもトレースに入るので、
                // 書式化されている方が、複雑な構造のデータを送るときにデバッグしやすい。
                WriteIndented = true
            };

            BaseResponseDeserializationOptions = new JsonSerializerOptions
            {
                // BaseRequestSerializationOptions と同じ理由で、あっては柔軟性が下がる。
                // PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
        }
    }
}
