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
    /// 読みがな。日本語の場合、通常はひらがなで表記されます。
    /// </summary>
    public string Reading { get; set; } = string.Empty;

    /// <summary>
    /// 表記。日本語の場合、通常は漢字やカタカナが含まれます。
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
    /// アイテムの現在のワークフローステータス。
    /// </summary>
    public EntryStatus Status { get; set; }

    /// <summary>
    /// このアイテムが作成されたUTCタイムスタンプ。
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// AIによる説明生成が完了したUTCタイムスタンプ（オプション）。
    /// </summary>
    public DateTime? ExplanationGeneratedAtUtc { get; set; }

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
    /// 画像パスが存在していても、このプロパティに値が設定されるとは限りません。
    /// 画像生成モデルが使用したプロンプトを返さない場合があるためです。
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
        ExplanationGeneratedAtUtc = DateTime.UtcNow;
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
        ExplanationGeneratedAtUtc = null;
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