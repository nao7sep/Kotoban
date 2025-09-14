namespace Kotoban.Core.Models;

/// <summary>
/// 語彙の単語を表します。
/// </summary>
public class VocabularyItem : LearningItem
{
    /// <summary>
    /// 使用法を示す例文のリスト。
    /// </summary>
    public List<string> UsageExamples { get; set; } = new();
}
