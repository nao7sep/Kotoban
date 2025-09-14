namespace Kotoban.Core.Models;

/// <summary>
/// 学習項目の状態を追跡するための列挙型。
/// </summary>
public enum EntryStatus
{
    /// <summary>
    /// 新規作成され、AIによる処理が必要な状態。
    /// </summary>
    PendingAiGeneration,

    /// <summary>
    /// AIによるコンテンツが生成され、人間のレビューを待っている状態。
    /// </summary>
    PendingApproval,

    /// <summary>
    /// レビューされ、使用が承認された状態。
    /// </summary>
    Approved
}
