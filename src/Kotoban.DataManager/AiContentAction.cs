namespace Kotoban.DataManager
{
    /// <summary>
    /// 指定項目の AI コンテンツの状態に基づく動的メニューに使われる。
    /// これがないと、メニュー項目の文字列での switch になる。
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
