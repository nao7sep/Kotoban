using System.Collections.Generic;

namespace Kotoban.Core.Services.OpenAi.Models;

/// <summary>
/// OpenAI 画像生成 API のレスポンスを表すモデルクラスです。
/// </summary>
public class OpenAiImageResponse : OpenAiApiObjectBase
{
    /// <summary>
    /// 作成日時（UNIXタイムスタンプ）。
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// 生成された画像データのリスト。
    /// </summary>
    public List<OpenAiImageData> Data { get; set; } = new();
}

/// <summary>
/// OpenAI 画像生成 API の画像データを表すモデルクラスです。
/// </summary>
public class OpenAiImageData : OpenAiApiObjectBase
{
    /// <summary>
    /// 画像の URL（ResponseFormat が "url" の場合）。
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 画像の Base64 文字列（ResponseFormat が "b64_json" の場合）。
    /// </summary>
    public string? B64Json { get; set; }

    /// <summary>
    /// 画像生成に利用されたプロンプト。
    /// </summary>
    public string? RevisedPrompt { get; set; }
}
