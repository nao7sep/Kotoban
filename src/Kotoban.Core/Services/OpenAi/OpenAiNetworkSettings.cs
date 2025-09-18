using System;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API へのアクセスに関するネットワーク設定を管理します。
/// </summary>
public class OpenAiNetworkSettings
{
    /// <summary>
    /// OpenAI API リクエストのデフォルトタイムアウト時間。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 指定されたタイムアウト値で新しいインスタンスを作成します。
    /// </summary>
    public OpenAiNetworkSettings(TimeSpan? timeout = null)
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 設定バインディング用のパラメータなしコンストラクタ。
    /// </summary>
    public OpenAiNetworkSettings() { }
}
