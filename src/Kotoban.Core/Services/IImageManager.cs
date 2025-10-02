using System;
using System.Threading.Tasks;
using Kotoban.Core.Models;

namespace Kotoban.Core.Services;

/// <summary>
/// 画像ファイルの管理とワークフローを提供するサービス。
/// </summary>
public interface IImageManager
{
    /// <summary>
    /// 画像編集セッションを開始します。既存の画像がある場合は一時ディレクトリにコピーします。
    /// </summary>
    /// <param name="entry">編集対象のエントリ</param>
    /// <returns>既存の画像情報、または画像が存在しない場合はnull</returns>
    Task<SavedImage?> StartImageEditingAsync(Entry entry);

    /// <summary>
    /// 生成された画像を一時ディレクトリに保存します。
    /// </summary>
    /// <param name="entry">対象のエントリ</param>
    /// <param name="imageBytes">画像データ</param>
    /// <param name="extension">ファイル拡張子</param>
    /// <param name="attemptNumber">試行回数</param>
    /// <param name="imageContext">画像生成用のコンテキスト</param>
    /// <param name="generatedAtUtc">画像生成完了時刻</param>
    /// <param name="imagePrompt">画像生成に使用されたプロンプト</param>
    /// <returns>保存された画像の情報</returns>
    Task<SavedImage> SaveGeneratedImageAsync(
        Entry entry,
        byte[] imageBytes,
        string extension,
        int attemptNumber,
        string? imageContext,
        DateTime generatedAtUtc,
        string? imagePrompt);

    /// <summary>
    /// 選択された画像を最終的な保存場所に移動します。
    /// </summary>
    /// <param name="entry">対象のエントリ</param>
    /// <param name="selectedImage">選択された画像情報</param>
    /// <returns>最終的なファイル名</returns>
    Task<string> FinalizeImageAsync(Entry entry, SavedImage selectedImage);

    /// <summary>
    /// 一時画像ファイルをクリーンアップします。
    /// </summary>
    /// <param name="entryId">対象のエントリID。nullの場合は、ファイル名がGUIDで始まるすべてのファイルを削除</param>
    Task CleanupTempImagesAsync(Guid? entryId);
}