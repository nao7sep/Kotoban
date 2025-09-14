using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kotoban.Core.Models;

namespace Kotoban.Core.Persistence;

/// <summary>
/// データアクセス操作の契約を定義します。
/// </summary>
public interface ILearningItemRepository
{
    /// <summary>
    /// すべての学習項目を非同期で取得します。
    /// </summary>
    /// <returns>学習項目のコレクション。</returns>
    Task<IEnumerable<LearningItem>> GetAllAsync();

    /// <summary>
    /// IDによって特定の学習項目を非同期で取得します。
    /// </summary>
    /// <param name="id">取得する項目のID。</param>
    /// <returns>見つかった場合は学習項目、それ以外の場合はnull。</returns>
    Task<LearningItem?> GetByIdAsync(Guid id);

    /// <summary>
    /// 新しい学習項目を非同期で追加します。
    /// </summary>
    /// <param name="item">追加する学習項目。</param>
    Task AddAsync(LearningItem item);

    /// <summary>
    /// 既存の学習項目を非同期で更新します。
    /// </summary>
    /// <param name="item">更新する学習項目。</param>
    Task UpdateAsync(LearningItem item);

    /// <summary>
    /// IDによって学習項目を非同期で削除します。
    /// </summary>
    /// <param name="id">削除する項目のID。</param>
    Task DeleteAsync(Guid id);
}
