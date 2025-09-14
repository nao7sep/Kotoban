using System.Text.Json.Serialization;

namespace Kotoban.Core.Models;

/// <summary>
/// 学習可能なすべてのアイテムのポリモーフィックな基本クラス。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(VocabularyItem), typeDiscriminator: "vocabulary")]
[JsonDerivedType(typeof(ConceptItem), typeDiscriminator: "concept")]
public abstract class LearningItem
{
    /// <summary>
    /// プライマリキー。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 学習する単語または概念。
    /// </summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// AI生成のために用語を明確にするための追加コンテキスト。
    /// </summary>
    public string ContextForAi { get; set; } = string.Empty;

    /// <summary>
    /// アイテムの現在のワークフローステータス。
    /// </summary>
    public EntryStatus Status { get; set; }

    /// <summary>
    /// さまざまなレベルの説明を格納します。
    /// </summary>
    public Dictionary<ExplanationLevel, string> Explanations { get; set; } = new();

    /// <summary>
    /// 説明画像のオプションURL。
    /// </summary>
    public string? ImageUrl { get; set; }
}
