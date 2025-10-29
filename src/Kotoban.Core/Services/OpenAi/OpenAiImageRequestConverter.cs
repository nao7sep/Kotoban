using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kotoban.Core.Services.OpenAi.Models;

namespace Kotoban.Core.Services.OpenAi
{
    /// <summary>
    /// OpenAiImageRequest の AdditionalData をフラット化するためのカスタム JsonConverter です。
    /// </summary>
    public class OpenAiImageRequestConverter : JsonConverter<OpenAiImageRequest>
    {
        public override void Write(Utf8JsonWriter writer, OpenAiImageRequest value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // 標準プロパティをシリアライズ
            writer.WriteString("model", value.Model);
            writer.WriteString("prompt", value.Prompt);

            if (value.N.HasValue)
            {
                writer.WriteNumber("n", value.N.Value);
            }

            if (value.Quality != null)
            {
                writer.WriteString("quality", value.Quality);
            }

            if (value.Size != null)
            {
                writer.WriteString("size", value.Size);
            }

            if (value.Style != null)
            {
                writer.WriteString("style", value.Style);
            }

            if (value.ResponseFormat != null)
            {
                writer.WriteString("response_format", value.ResponseFormat);
            }

            // AdditionalData をフラット化
            if (value.AdditionalData != null)
            {
                foreach (var (key, val) in value.AdditionalData)
                {
                    writer.WritePropertyName(key);
                    JsonSerializer.Serialize(writer, val, options);
                }
            }

            writer.WriteEndObject();
        }

        public override OpenAiImageRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // このコンバーターはシリアライズ専用です。
            throw new NotImplementedException();
        }
    }
}
