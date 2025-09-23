namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API のトランスポート層（認証やエンドポイントなど）に関する情報を保持するクラスです。
/// </summary>
public class OpenAiTransportContext
{
    /// <summary>
    /// OpenAI API の設定。
    /// </summary>
    private readonly OpenAiSettings _openAiSettings;

    /// <summary>
    /// API キー。
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// API ベース URL。
    /// </summary>
    public string ApiBase { get; set; }

    /// <summary>
    /// OpenAiSettings から値を注入するためのコンストラクタ。
    /// </summary>
    /// <param name="settings">OpenAI API 設定</param>
    public OpenAiTransportContext(OpenAiSettings settings)
    {
        _openAiSettings = settings;

        ApiKey = settings.ApiKey;
        ApiBase = settings.ApiBase;
    }
}
