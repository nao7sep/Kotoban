using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Persistence.Json;

/// <summary>
/// 文字列のシリアライズとデシリアライズを、複数行文字列の特別な処理を含めて扱います。
/// null の文字列は null としてシリアライズされます。
/// 単一行の文字列は JSON 文字列としてシリアライズされます。
/// 複数行の文字列は、空行に関する特別な処理を伴う文字列の配列としてシリアライズされます。
/// </summary>
public class MultilineStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            // 改行入りだが配列として保存されていない文字列は、いずれ書き出されるときに配列になる。
            // そういう文字列に \r\n または \n のどちらが含まれていても、書き出し時にノーマライズ（？）される。
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var lines = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                if (reader.TokenType == JsonTokenType.String)
                {
                    lines.Add(reader.GetString()!);
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        throw new JsonException("Unexpected token type for string deserialization.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (!value.Contains('\n'))
        {
            writer.WriteStringValue(value);
            return;
        }

        var processedLines = StringUtils.NormalizeMultilineString(value).ToList();

        // 処理後、結果が単一行になった場合は、文字列として書き込む
        if (processedLines.Count <= 1)
        {
            writer.WriteStringValue(processedLines.FirstOrDefault());
            return;
        }

        writer.WriteStartArray();
        foreach (var line in processedLines)
        {
            writer.WriteStringValue(line);
        }
        writer.WriteEndArray();
    }
}
