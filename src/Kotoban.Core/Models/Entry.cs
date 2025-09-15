using System.Text.Json.Serialization;

namespace Kotoban.Core.Models;

/// <summary>
/// 単一の学習エントリ。
/// </summary>
public class Entry
{
    /// <summary>
    /// プライマリキー。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// アイテムの現在のワークフローステータス。
    /// </summary>
    public EntryStatus Status { get; set; }

    /// <summary>
    /// このアイテムが作成されたUTCタイムスタンプ。
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// AIによるコンテンツ生成が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ContentGeneratedAtUtc { get; set; }

    /// <summary>
    /// AIによる画像生成が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ImageGeneratedAtUtc { get; set; }

    /// <summary>
    /// 人間による承認が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ApprovedAtUtc { get; set; }

    /// <summary>
    /// 学習する単語または概念。
    /// </summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// AI生成のために用語を明確にするための追加コンテキスト。
    /// </summary>
    public string? ContextForAi { get; set; }

    /// <summary>
    /// ユーザーが追加した個人的なメモ。
    /// </summary>
    public string? UserNote { get; set; }

    /// <summary>
    /// さまざまなレベルの説明を格納します。
    /// </summary>
    public Dictionary<ExplanationLevel, string> Explanations { get; set; } = new();

    /// <summary>
    /// 説明画像のオプションURL。
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// AIが画像を生成した際に返された、画像を再現するためのプロンプト。
    /// </summary>
    public string? ImagePrompt { get; set; }
}
