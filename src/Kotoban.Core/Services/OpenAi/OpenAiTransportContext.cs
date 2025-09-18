namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API のトランスポート層（認証やエンドポイントなど）に関する情報を保持するクラスです。
/// </summary>
public class OpenAiTransportContext
{
    /// <summary>
    /// API キー。
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API ベース URL。
    /// </summary>
    public string ApiBase { get; set; } = "https://api.openai.com/v1/";
}
