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
                // null 値を持つプロパティをシリアライズから除外することで、ペイロードのサイズを削減します。
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                // structured model outputs 機能では、`additionalProperties` という厳密なキー名が要求されます。
                // しかし、`PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` を設定すると、このキーが `additional_properties` に変換されてしまい、API エラーを引き起こします。
                // この問題を回避するため、`PropertyNamingPolicy` は設定せず、代わりに各プロパティに `[JsonPropertyName]` 属性を明示的に付与しています。

                // デバッグ時の可読性を向上させるため、JSON をインデントして出力します。
                WriteIndented = true
            };

            BaseResponseDeserializationOptions = new JsonSerializerOptions
            {
                // リクエストと同様の理由で、`PropertyNamingPolicy` は設定しません。
            };
        }
    }
}
