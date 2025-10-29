using System;
using System.Collections.Generic;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// 単一の学習エントリ。
    /// </summary>
    public class Entry
    {
        /// <summary>
        /// プライマリキー。
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 読み仮名。
        /// </summary>
        public string Reading { get; set; } = string.Empty;

        /// <summary>
        /// 表記。
        /// </summary>
        public string? Expression { get; set; }

        /// <summary>
        /// AIが単語を特定するための一般的なコンテキスト。
        /// </summary>
        public string? GeneralContext { get; set; }

        /// <summary>
        /// 説明生成用のコンテキスト。
        /// </summary>
        public string? ExplanationContext { get; set; }

        /// <summary>
        /// 画像生成用のコンテキスト。
        /// </summary>
        public string? ImageContext { get; set; }

        /// <summary>
        /// ユーザーが追加した個人的なメモ。
        /// </summary>
        public string? UserNote { get; set; }

        /// <summary>
        /// エントリの現在のワークフローステータス。
        /// </summary>
        public EntryStatus Status { get; set; }

        /// <summary>
        /// このエントリが作成されたUTCタイムスタンプ。
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// AIによる説明生成が完了したUTCタイムスタンプ。
        /// </summary>
        public DateTime? ExplanationGeneratedAtUtc { get; set; }

        /// <summary>
        /// AIによる画像生成が完了したUTCタイムスタンプ。
        /// </summary>
        public DateTime? ImageGeneratedAtUtc { get; set; }

        /// <summary>
        /// 人間による承認が完了したUTCタイムスタンプ。
        /// </summary>
        public DateTime? ApprovedAtUtc { get; set; }

        /// <summary>
        /// 難易度別の説明。
        /// </summary>
        public Dictionary<ExplanationLevel, string> Explanations { get; set; } = new();

        /// <summary>
        /// 説明画像のファイル名（拡張子を含む）。
        /// </summary>
        public string? ImageFileName { get; set; }

        /// <summary>
        /// 画像を再現するためにAIが使用したプロンプト。
        /// </summary>
        public string? ImagePrompt { get; set; }

        /// <summary>
        /// 生成されたAIの説明を登録します。
        /// </summary>
        /// <param name="explanationContext">説明生成に使用されたコンテキスト。</param>
        /// <param name="newExplanations">新しい説明のディクショナリ。</param>
        public void RegisterGeneratedExplanations(string? explanationContext, Dictionary<ExplanationLevel, string> newExplanations)
        {
            ExplanationContext = explanationContext;
            Explanations = newExplanations;
            ExplanationGeneratedAtUtc = DateTime.UtcNow;
            Status = EntryStatus.PendingApproval;
            ApprovedAtUtc = null;
        }

        /// <summary>
        /// 生成されたAIの画像を登録します。
        /// </summary>
        /// <param name="imageContext">画像生成に使用されたコンテキスト。</param>
        /// <param name="imageFileName">画像ファイル名（拡張子付き）。</param>
        /// <param name="imagePrompt">画像の生成に使用されたプロンプト。</param>
        public void RegisterGeneratedImage(string? imageContext, string imageFileName, string? imagePrompt)
        {
            ImageContext = imageContext;
            ImageFileName = imageFileName;
            ImagePrompt = imagePrompt;
            ImageGeneratedAtUtc = DateTime.UtcNow;
            if (Explanations.Count == 0)
            {
                Status = EntryStatus.PendingAiGeneration;
                ApprovedAtUtc = null;
            }
            else
            {
                Status = EntryStatus.PendingApproval;
                ApprovedAtUtc = null;
            }
        }

        /// <summary>
        /// AIによって生成されたすべてのコンテンツをクリアし、ステータスをリセットします。
        /// </summary>
        public void ClearAiContent()
        {
            Explanations.Clear();
            ImageFileName = null;
            ImagePrompt = null;
            Status = EntryStatus.PendingAiGeneration;
            ExplanationGeneratedAtUtc = null;
            ImageGeneratedAtUtc = null;
            ApprovedAtUtc = null;
        }

        /// <summary>
        /// エントリを承認済みとしてマークします。
        /// </summary>
        public void Approve()
        {
            if (Status != EntryStatus.PendingApproval)
            {
                throw new InvalidOperationException("Only entries pending approval can be approved.");
            }
            Status = EntryStatus.Approved;
            ApprovedAtUtc = DateTime.UtcNow;
        }
    }
}
