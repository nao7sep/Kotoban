using System;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API へのアクセスに関するネットワーク設定を管理します。
/// </summary>
public class OpenAiNetworkSettings
{
    /// <summary>
    /// OpenAI API の設定。
    /// </summary>
    private readonly OpenAiSettings _settings;

    /// <summary>
    /// OpenAI API リクエストのデフォルトタイムアウト時間。
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// DI用: OpenAiSettings からタイムアウト値を取得して初期化します。
    /// </summary>
    public OpenAiNetworkSettings(OpenAiSettings settings)
    {
        _settings = settings;
        Timeout = _settings.Timeout;
    }
}
