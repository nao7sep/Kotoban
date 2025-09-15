namespace Kotoban.Core.Models;

/// <summary>
/// 説明の難易度を定義するための列挙型。
/// </summary>
public enum ExplanationLevel
{
    /// <summary>
    /// 未就学児・低学年向け（例：ひらがな/カタカナのみ）。
    /// </summary>
    Easy,

    /// <summary>
    /// 高学年向け（例：小学校で習う漢字を含む）。
    /// </summary>
    Moderate,

    /// <summary>
    /// 中学生以上向け（例：常用漢字を含む）。
    /// </summary>
    Advanced
}
