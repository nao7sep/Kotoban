using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kotoban.Core.Models;

namespace Kotoban.Core.Persistence
{
    /// <summary>
    /// データアクセス操作の契約を定義します。
    /// </summary>
    public interface IEntryRepository
    {
        /// <summary>
        /// すべての項目を非同期で取得します。
        /// </summary>
        /// <param name="status">取得する項目のステータスをフィルタリングするためのオプションのパラメーター。</param>
        /// <param name="cancellationToken">非同期操作をキャンセルするためのトークン。</param>
        /// <returns>項目のコレクション。</returns>
        Task<IEnumerable<Entry>> GetAllAsync(EntryStatus? status = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// IDによって特定の項目を非同期で取得します。
        /// </summary>
        /// <param name="id">取得する項目のID。</param>
        /// <param name="cancellationToken">非同期操作をキャンセルするためのトークン。</param>
        /// <returns>見つかった場合は項目、それ以外の場合はnull。</returns>
        Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 新しい項目を非同期で追加します。
        /// </summary>
        /// <param name="item">追加する項目。</param>
        /// <param name="cancellationToken">非同期操作をキャンセルするためのトークン。</param>
        Task AddAsync(Entry item, CancellationToken cancellationToken = default);

        /// <summary>
        /// 既存の項目を非同期で更新します。
        /// </summary>
        /// <param name="item">更新する項目。</param>
        /// <param name="cancellationToken">非同期操作をキャンセルするためのトークン。</param>
        Task UpdateAsync(Entry item, CancellationToken cancellationToken = default);

        /// <summary>
        /// IDによって項目を非同期で削除します。
        /// </summary>
        /// <param name="id">削除する項目のID。</param>
        /// <param name="cancellationToken">非同期操作をキャンセルするためのトークン。</param>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
