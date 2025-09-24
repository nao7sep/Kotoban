using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models;

// Responses API も検討した。
// その前に Chat Completions でつくっちまっていたので、消して Responses API でつくり直すほどの理由もなく、Kotoban では Chat Completions でいく。
// もっとも、二つを比較しての判断として、今回においては、実装がまだだったとしても Chat Completions でつくっていた可能性が高い。
// そういったところを今後の参考のために冗長気味に書いておいた。
// https://github.com/nao7sep/coding-notes/blob/main/strategic-considerations-for-choosing-between-openais-chat-completions-and-responses-apis.md

/// <summary>
/// OpenAI Chat API へのリクエストを表すモデルクラスです。
/// </summary>
public class OpenAiChatRequest : OpenAiApiObjectBase
{
    /// <summary>
    /// 使用するモデル名。
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// メッセージのリスト。
    /// </summary>
    [JsonPropertyName("messages")]
    public List<OpenAiChatMessage> Messages { get; set; } = new();
}

/// <summary>
/// OpenAI Chat API のメッセージを表すモデルクラスです。
/// </summary>
public class OpenAiChatMessage : OpenAiApiObjectBase
{
    /// <summary>
    /// メッセージの役割（system, user, assistant など）。
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// メッセージ本文。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
