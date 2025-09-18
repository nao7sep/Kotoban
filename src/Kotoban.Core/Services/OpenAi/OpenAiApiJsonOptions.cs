using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi;

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
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        BaseResponseDeserializationOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }
}
