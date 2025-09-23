namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API のトランスポート層（認証やエンドポイントなど）に関する情報を保持するクラスです。
/// </summary>
public class OpenAiTransportContext
{
    /// <summary>
    /// OpenAI API の設定。
    /// </summary>
    private readonly OpenAiSettings _settings;

    /// <summary>
    /// API キー。
    /// </summary>
    public string ApiKey { get; }

    /// <summary>
    /// API ベース URL。
    /// </summary>
    public string ApiBase { get; }

    /// <summary>
    /// OpenAiSettings から値を注入するためのコンストラクタ。
    /// </summary>
    /// <param name="settings">OpenAI API 設定</param>
    /// <exception cref="InvalidOperationException">APIキーまたはベースURLが未設定の場合</exception>
    public OpenAiTransportContext(OpenAiSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("ApiKey is required.");
        }
        if (string.IsNullOrWhiteSpace(settings.ApiBase))
        {
            throw new InvalidOperationException("ApiBase is required.");
        }

        _settings = settings;
        ApiKey = _settings.ApiKey;
        ApiBase = _settings.ApiBase;
    }
}
