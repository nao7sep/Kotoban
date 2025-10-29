namespace Kotoban.Core.Models
{
    /// <summary>
    /// 学習エントリの状態を定義します。
    /// </summary>
    public enum EntryStatus
    {
        /// <summary>
        /// AIによるコンテンツ生成が必要な状態。
        /// </summary>
        PendingAiGeneration,

        /// <summary>
        /// AIによって生成されたコンテンツが人間によるレビューを待っている状態。
        /// </summary>
        PendingApproval,

        /// <summary>
        /// 人間によるレビューが完了し、承認された状態。
        /// </summary>
        Approved
    }
}
