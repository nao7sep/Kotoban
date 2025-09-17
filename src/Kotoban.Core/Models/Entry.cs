using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Models;

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
    /// 学習する単語または概念。
    /// </summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>
    /// AI生成のために用語を明確にするための追加コンテキスト。
    /// </summary>
    public string? ContextForAi { get; set; }

    /// <summary>
    /// ユーザーが追加した個人的なメモ。
    /// </summary>
    public string? UserNote { get; set; }

    /// <summary>
    /// アイテムの現在のワークフローステータス。
    /// </summary>
    public EntryStatus Status { get; set; }

    /// <summary>
    /// このアイテムが作成されたUTCタイムスタンプ。
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// AIによるコンテンツ生成が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ContentGeneratedAtUtc { get; set; }

    /// <summary>
    /// AIによる画像生成が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ImageGeneratedAtUtc { get; set; }

    /// <summary>
    /// 人間による承認が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ApprovedAtUtc { get; set; }

    /// <summary>
    /// さまざまなレベルの説明を格納します。
    /// </summary>
    public Dictionary<ExplanationLevel, string> Explanations { get; set; } = new();

    /// <summary>
    /// 説明画像の相対ファイルパス。
    /// </summary>
    public string? RelativeImagePath { get; set; }

    /// <summary>
    /// AIが画像を生成した際に返された、画像を再現するためのプロンプト。
    /// </summary>
    public string? ImagePrompt { get; set; }

    #region ビジネスロジック

    /// <summary>
    /// 生成されたAIの説明を登録します。
    /// </summary>
    /// <param name="newExplanations">新しい説明のディクショナリ</param>
    public void RegisterGeneratedExplanations(Dictionary<ExplanationLevel, string> newExplanations)
    {
        Explanations = newExplanations;
        ContentGeneratedAtUtc = DateTime.UtcNow;
        UpdateStatusAfterGeneration();
    }

    /// <summary>
    /// 生成されたAIの画像を登録します。
    /// </summary>
    /// <param name="imagePath">画像の相対パス</param>
    /// <param name="imagePrompt">画像の生成に使用されたプロンプト</param>
    public void RegisterGeneratedImage(string imagePath, string imagePrompt)
    {
        RelativeImagePath = imagePath;
        ImagePrompt = imagePrompt;
        ImageGeneratedAtUtc = DateTime.UtcNow;
        UpdateStatusAfterGeneration();
    }

    /// <summary>
    /// コンテンツが生成または再生成された後にステータスを更新します。
    /// </summary>
    private void UpdateStatusAfterGeneration()
    {
        Status = EntryStatus.PendingApproval;
        ApprovedAtUtc = null;
    }

    /// <summary>
    /// AIによって生成されたすべてのコンテンツをクリアし、ステータスをリセットします。
    /// </summary>
    public void ClearAiContent()
    {
        Explanations.Clear();
        RelativeImagePath = null;
        ImagePrompt = null;
        ContentGeneratedAtUtc = null;
        ImageGeneratedAtUtc = null;
        ApprovedAtUtc = null;
        Status = EntryStatus.PendingAiGeneration;
    }

    /// <summary>
    /// エントリを承認済みとしてマークします。
    /// </summary>
    public void Approve()
    {
        if (Status == EntryStatus.PendingApproval)
        {
            Status = EntryStatus.Approved;
            ApprovedAtUtc = DateTime.UtcNow;
        }
    }

    #endregion
}