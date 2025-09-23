using System;

namespace Kotoban.Core.Models;

/// <summary>
/// 生成された画像の情報を表します。
/// </summary>
public class GeneratedImage
{
    /// <summary>
    /// 画像ファイルの相対パス。
    /// </summary>
    public string RelativeImagePath { get; set; } = string.Empty;

    /// <summary>
    /// 画像生成用のコンテキスト。
    /// </summary>
    public string? ImageContext { get; set; }

    /// <summary>
    /// 画像生成が完了したUTCタイムスタンプ。
    /// </summary>
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>
    /// AIが画像を生成した際に返された、画像を再現するためのプロンプト。
    /// </summary>
    public string? ImagePrompt { get; set; }
}