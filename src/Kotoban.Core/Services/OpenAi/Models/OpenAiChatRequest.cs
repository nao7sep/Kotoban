using System.Collections.Generic;

namespace Kotoban.Core.Services.OpenAi.Models;

/// <summary>
/// OpenAI Chat API へのリクエストを表すモデルクラスです。
/// </summary>
public class OpenAiChatRequest
{
    /// <summary>
    /// 使用するモデル名。
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// メッセージのリスト。
    /// </summary>
    public List<OpenAiChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// 追加のパラメータ（必要に応じて拡張）。
    /// </summary>
    public Dictionary<string, object>? AdditionalParameters { get; set; }
}

/// <summary>
/// OpenAI Chat API のメッセージを表すモデルクラスです。
/// </summary>
public class OpenAiChatMessage
{
    /// <summary>
    /// メッセージの役割（system, user, assistant など）。
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// メッセージ本文。
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
