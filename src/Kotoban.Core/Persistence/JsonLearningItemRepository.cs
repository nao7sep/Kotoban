using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kotoban.Core.Persistence;

/// <summary>
/// 学習項目をJSONファイルに永続化するためのリポジトリ実装。
/// </summary>
public class JsonLearningItemRepository : ILearningItemRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonLearningItemRepository> _logger;
    private List<LearningItem> _items = new();

    /// <summary>
    /// JsonLearningItemRepositoryの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="filePath">データが格納されているJSONファイルのパス。</param>
    /// <param name="logger">ロギング用のロガーインスタンス。</param>
    public JsonLearningItemRepository(string filePath, ILogger<JsonLearningItemRepository> logger)
    {
        _filePath = filePath;
        _logger = logger;
        LoadData();
    }

    /// <summary>
    /// ファイルからデータをロードします。
    /// </summary>
    private void LoadData()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _items = new List<LearningItem>();
                return;
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _items = new List<LearningItem>();
                return;
            }

            _items = JsonSerializer.Deserialize<List<LearningItem>>(json) ?? new List<LearningItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "データファイルのデシリアライズ中にエラーが発生しました: {FilePath}", _filePath);
            throw; // Or handle more gracefully depending on requirements
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データファイルの読み込み中に予期せぬエラーが発生しました: {FilePath}", _filePath);
            throw;
        }
    }

    /// <summary>
    /// メモリ内のデータをファイルに非同期で保存します。
    /// </summary>
    private async Task SaveDataAsync()
    {
        // 1. Backup
        try
        {
            if (File.Exists(_filePath))
            {
                var backupPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileName(_filePath)}.{Guid.NewGuid()}.bak");
                File.Copy(_filePath, backupPath, true);
                _logger.LogInformation("データファイルのバックアップを作成しました: {BackupPath}", backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "データファイルのバックアップ作成に失敗しました: {FilePath}", _filePath);
        }

        // 2. Serialize and Save
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(_items, options);
            
            // UTF-8 with BOM
            var encoding = new UTF8Encoding(true);
            await File.WriteAllTextAsync(_filePath, json, encoding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データファイルの保存中にエラーが発生しました: {FilePath}", _filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<LearningItem>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<LearningItem>>(_items.ToList());
    }

    /// <inheritdoc />
    public Task<LearningItem?> GetByIdAsync(Guid id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        return Task.FromResult(item);
    }

    /// <inheritdoc />
    public async Task AddAsync(LearningItem item)
    {
        if (item.Id == Guid.Empty)
        {
            item.Id = Guid.NewGuid();
        }
        _items.Add(item);
        await SaveDataAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(LearningItem item)
    {
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index != -1)
        {
            _items[index] = item;
            await SaveDataAsync();
        }
        else
        {
            _logger.LogWarning("更新対象の項目が見つかりませんでした: ID {ItemId}", item.Id);
            // Or throw an exception
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            _items.Remove(item);
            await SaveDataAsync();
        }
        else
        {
            _logger.LogWarning("削除対象の項目が見つかりませんでした: ID {ItemId}", id);
        }
    }
}
