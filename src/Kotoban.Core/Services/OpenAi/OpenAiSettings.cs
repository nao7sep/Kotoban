using System;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API へのアクセスに必要な設定をカプセル化します。
/// </summary>
public class OpenAiSettings
{
    /// <summary>
    /// OpenAI API キー。
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI API ベース URL。
    /// </summary>
    public string ApiBase { get; set; } = "https://api.openai.com/v1/";

    /// <summary>
    /// チャットモデル名。
    /// </summary>
    public string ChatModel { get; set; } = "gpt-5";

    /// <summary>
    /// 画像生成モデル名。
    /// </summary>
    public string ImageModel { get; set; } = "gpt-image-1";

    /// <summary>
    /// OpenAI API リクエストのデフォルトタイムアウト時間。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(180);
}
