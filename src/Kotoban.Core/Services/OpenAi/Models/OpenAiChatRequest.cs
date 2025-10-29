using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
    // OpenAI は Chat Completions から Responses API への移行を推奨していますが、
    // Kotoban では以下の理由により Chat Completions を継続使用します。
    //
    // 【Responses API との比較検討結果】
    // 1. 会話ログ管理: Responses API では OpenAI 側での管理が必須となり、
    //    ローカルでの柔軟な会話ログ操作（必要なメッセージのみ選択送信等）が困難
    // 2. 機能面: Chat Completions の既存機能で Kotoban の要件は充足
    // 3. 実装済み: 既に Chat Completions ベースで実装済みであり、移行の必要性が低い
    //
    // 【Chat Completions を選択する基準】
    // - AI 機能が Chat Completions で充足している
    // - messages の動的操作が必要（コンテキスト圧縮、関連メッセージ選択等）
    // - stateless な処理を重視する
    //
    // 【Responses API を選択すべき場合】
    // - 新規開発で長期的な派生開発が予想される
    // - コスト最適化を重視する（40-80%のコスト削減が期待できる）
    // - OpenAI 側での会話管理で問題ない

    /// <summary>
    /// OpenAI Chat API へのリクエストを表すモデルクラスです。
    /// </summary>
    public class OpenAiChatRequest : OpenAiApiObjectBase
    {
        /// <summary>
        /// 使用するモデル名。
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// メッセージのリスト。
        /// </summary>
        [JsonPropertyName("messages")]
        public List<OpenAiChatMessage> Messages { get; set; } = new();
    }

    /// <summary>
    /// OpenAI Chat API のメッセージを表すモデルクラスです。
    /// </summary>
    public class OpenAiChatMessage : OpenAiApiObjectBase
    {
        /// <summary>
        /// メッセージの役割（system, user, assistant など）。
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// メッセージ本文。
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
