namespace Kotoban.DataManager.Models
{
    /// <summary>
    /// 指定項目のAIコンテンツの状態に基づく動的メニューに使われる列挙型。
    /// これがないと、メニュー項目の文字列でのswitchになる。
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
