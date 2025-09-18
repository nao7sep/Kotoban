namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API のトランスポート層（認証やエンドポイントなど）に関する情報を保持するクラスです。
/// </summary>
public class OpenAiTransportContext
{
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
        // OpenAiSettings から必要な値をコピー
        ApiKey = settings.ApiKey;
        ApiBase = settings.ApiBase;
    }
}
