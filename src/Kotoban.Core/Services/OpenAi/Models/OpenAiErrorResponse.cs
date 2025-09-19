namespace Kotoban.Core.Services.OpenAi.Models;

/// <summary>
/// OpenAI API のエラーレスポンスを表すモデルクラスです。
/// </summary>
public class OpenAiErrorResponse : OpenAiApiObjectBase
{
    /// <summary>
    /// エラー情報。
    /// </summary>
    public OpenAiError? Error { get; set; }
}

/// <summary>
/// OpenAI API のエラー詳細を表すモデルクラスです。
/// </summary>
public class OpenAiError : OpenAiApiObjectBase
{
    /// <summary>
    /// エラーメッセージ。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// エラータイプ。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// エラーコード。
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// エラーのパラメータ名（存在する場合）。
    /// </summary>
    public string? Param { get; set; }
}