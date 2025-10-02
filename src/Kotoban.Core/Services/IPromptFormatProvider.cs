namespace Kotoban.Core.Services
{
    /// <summary>
    /// プロンプトフォーマットを取得するためのインターフェース。
    /// </summary>
    public interface IPromptFormatProvider
    {
        /// <summary>
        /// 説明文生成用プロンプトフォーマットを取得する。
        /// </summary>
        /// <returns>説明文生成用プロンプトフォーマットの文字列。</returns>
        Task<string> GetExplanationPromptFormatAsync();

        /// <summary>
        /// 画像生成用プロンプトフォーマットを取得する。
        /// </summary>
        /// <returns>画像生成用プロンプトフォーマットの文字列。</returns>
        Task<string> GetImagePromptFormatAsync();
    }
}
