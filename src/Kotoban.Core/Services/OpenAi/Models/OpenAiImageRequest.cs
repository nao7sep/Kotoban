namespace Kotoban.Core.Services.OpenAi.Models;

/// <summary>
/// OpenAI 画像生成 API へのリクエストを表すモデルクラスです。
/// </summary>
public class OpenAiImageRequest : OpenAiApiObjectBase
{
    /// <summary>
    /// 使用するモデル名。
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 画像生成のためのプロンプト。
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 画像の生成枚数。
    /// </summary>
    public int? N { get; set; }

    /// <summary>
    /// 画像の品質（例: "standard", "hd"）。
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    /// 画像サイズ（例: "1024x1024"）。
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// レスポンス形式（"url" または "b64_json"）。
    /// </summary>
    public string? ResponseFormat { get; set; }
}
