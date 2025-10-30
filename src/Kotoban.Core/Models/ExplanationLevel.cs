namespace Kotoban.Core.Models
{
    /// <summary>
    /// 説明の難易度を定義します。
    /// </summary>
    public enum ExplanationLevel
    {
        /// <summary>
        /// 約6歳の子どもを対象とした、ひらがなとカタカナのみを使用するレベル。
        /// 説明は短く、具体的で、視覚的に想像しやすい内容です。
        /// </summary>
        Easy,

        /// <summary>
        /// 約12歳の子どもを対象とし、小学校で習う標準的な漢字を使用するレベル。
        /// 簡単なレベルより詳細で、複数の視点から解説します。
        /// </summary>
        Moderate,

        /// <summary>
        /// 大人と上級学習者を対象とし、常用漢字を使用するレベル。
        /// 語源、文化的背景、微妙なニュアンスなど、深い知識を含む包括的な解説を提供します。
        /// </summary>
        Advanced
    }
}
