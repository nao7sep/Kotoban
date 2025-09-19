using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kotoban.Core.Services.OpenAi.Models;

namespace Kotoban.Core.Services.OpenAi.Json;

/// <summary>
/// OpenAiChatRequest の AdditionalData をフラット化するためのカスタム JsonConverter です。
/// </summary>
public class OpenAiChatRequestConverter : JsonConverter<OpenAiChatRequest>
{
    public override void Write(Utf8JsonWriter writer, OpenAiChatRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // 標準プロパティをシリアライズ
        writer.WriteString("model", value.Model);
        writer.WritePropertyName("messages");
        JsonSerializer.Serialize(writer, value.Messages, options);

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

    public override OpenAiChatRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // このコンバーターはシリアライズ専用です。
        throw new NotImplementedException();
    }
}