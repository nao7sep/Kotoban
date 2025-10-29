namespace Kotoban.DataManager.Models
{
    /// <summary>
    /// 指定項目の AI コンテンツの状態に基づく動的メニューで使用される列挙型です。
    /// 文字列ベースの switch 文を避け、型安全なメニュー操作を提供します。
    /// </summary>
    internal enum AiContentAction
    {
        Generate,
        Regenerate,
        Approve,
        Delete,
        Exit
    }
}
