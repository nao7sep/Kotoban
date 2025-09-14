namespace Kotoban.Core.Models;

/// <summary>
/// 説明の難易度を定義するための列挙型。
/// </summary>
public enum ExplanationLevel
{
    /// <summary>
    /// 幼児向け（例：ひらがな/カタカナのみ）。
    /// </summary>
    Easy,

    /// <summary>
    /// 年長の子供向け（例：小学校レベルの漢字）。
    /// </summary>
    Moderate,

    /// <summary>
    /// 大人または上級学習者向け。
    /// </summary>
    Advanced
}
