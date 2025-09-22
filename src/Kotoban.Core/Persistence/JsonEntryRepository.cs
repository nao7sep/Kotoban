using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence.Json;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Persistence;

/// <summary>
/// 項目をJSONファイルに永続化するためのリポジトリ実装。
/// </summary>
public class JsonEntryRepository : IEntryRepository
{
    private readonly string _filePath;
    private readonly JsonRepositoryBackupMode _backupMode;
    private readonly string _backupDirectory;
    private List<Entry> _items = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly int _maxBackupFiles;

    /// <summary>
    /// データストアのファイルパスを取得します。
    /// </summary>
    public string DataFilePath => _filePath;

    /// <summary>
    /// このリポジトリが使用するバックアップ戦略を取得します。
    /// </summary>
    public JsonRepositoryBackupMode BackupMode => _backupMode;

    /// <summary>
    /// 現在メモリにロードされている項目の読み取り専用リストを取得します。
    /// </summary>
    public IReadOnlyList<Entry> Items => _items.AsReadOnly();

    /// <summary>
    /// このリポジトリで使用されるJSONシリアライゼーション設定を取得します。
    /// </summary>
    public JsonSerializerOptions JsonOptions => _jsonOptions;

    /// <summary>
    /// 保持するバックアップファイルの最大数を取得します。
    /// </summary>
    public int MaxBackupFiles => _maxBackupFiles;

    /// <summary>
    /// JsonEntryRepositoryの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="filePath">データが格納されているJSONファイルのパス。</param>
    /// <param name="backupMode">このリポジトリが使用するバックアップ戦略。</param>
    /// <param name="backupDirectory">バックアップファイルを保存するディレクトリのパス。</param>
    /// <param name="maxBackupFiles">保持するバックアップファイルの最大数。</param>
    public JsonEntryRepository(string filePath, JsonRepositoryBackupMode backupMode, string backupDirectory, int maxBackupFiles)
    {
        _filePath = filePath;
        _backupMode = backupMode;
        _backupDirectory = backupDirectory;
        _maxBackupFiles = maxBackupFiles;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // ここで JsonStringEnumConverter を追加するのは、2つの理由からです。
        // 1. Entry モデル内の全ての enum (Status プロパティと Explanations ディクショナリのキーである ExplanationLevel) を文字列としてシリアライズ/デシリアライズするため。
        // 2. モデルクラス自体に特定のシリアライズ形式 (JSON) の詳細が漏れ出すのを防ぎ、関心の分離を維持するため。
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        _jsonOptions.Converters.Add(new MultilineStringConverter());

        LoadData();
    }

    /// <summary>
    /// ファイルからデータをロードし、作成日時でソートします。
    /// </summary>
    private void LoadData()
    {
        if (!File.Exists(_filePath))
        {
            _items = new List<Entry>();
            return;
        }

        var json = File.ReadAllText(_filePath, Encoding.UTF8);

        // ファイルが空、空白、またはJSONリテラルの "null" の場合、
        // 新しい空のリストとして扱います。

        // 分かりにくい AI コメントなので追記: JSON リテラルの "null" は、デシリアライズ時に、構造を持つ JSON でなく単一の「値」として null になる。
        // Serialize/Deserialize は、カルチャーの影響を受けないラウンドトリップのメソッドとして、JSON 外のコンテキストでも使われつつある。
        // 入力が空白系文字列でなく null が戻ったなら、"null" だったと考えてよい。
        // それ以外の、しっかりと壊れている JSON なら、JsonException が投げられる。

        if (string.IsNullOrWhiteSpace(json))
        {
            _items = new List<Entry>();
            return;
        }

        // JSONが不正な形式である場合、JsonSerializerは例外をスローし、
        // これは呼び出し元に伝播します。
        var items = JsonSerializer.Deserialize<List<Entry>>(json, _jsonOptions) ?? new List<Entry>();
        _items = items.OrderBy(e => e.CreatedAtUtc).ToList();
    }

    /// <summary>
    /// メモリ内のデータをソートしてファイルに非同期で保存します。
    /// </summary>
    private async Task SaveDataAsync()
    {
        var exceptions = new List<Exception>();

        // バックアップ
        if (_backupMode == JsonRepositoryBackupMode.CreateCopyInTemp)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    Directory.CreateDirectory(_backupDirectory);

                    // 1秒に2回以上のバックアップが行われるケースを想定しにくいので、タイムスタンプの精度はこれで十分。
                    // 万が一にもそういうことがあったなら、差分がなく無意味なバックアップだろうし、上書き保存なのでたぶん落ちない。
                    var timestamp = DateTimeUtils.UtcNowTimestamp();
                    var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(_filePath);
                    // バックアップディレクトリに保存するので、拡張子を .bak などにしない。
                    var backupFileName = $"{originalFileNameWithoutExtension}-{timestamp}.json";
                    var backupPath = Path.Combine(_backupDirectory, backupFileName);

                    File.Copy(_filePath, backupPath, true);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // 保存
        try
        {
            // ファイル内での一貫した順序を保証するためにリストをソートします
            _items = _items.OrderBy(e => e.CreatedAtUtc).ToList();
            var json = JsonSerializer.Serialize(_items, _jsonOptions);
            DirectoryUtils.EnsureParentDirectoryExists(_filePath);
            await File.WriteAllTextAsync(_filePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }

        // クリーンアップ
        if (_backupMode == JsonRepositoryBackupMode.CreateCopyInTemp)
        {
            try
            {
                if (_maxBackupFiles > 0)
                {
                    Directory.CreateDirectory(_backupDirectory);
                    var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(_filePath);
                    var backupFiles = Directory.GetFiles(_backupDirectory)
                        .Where(f => Path.GetFileName(f).StartsWith(originalFileNameWithoutExtension + "-") &&
                                    Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (backupFiles.Length > _maxBackupFiles)
                    {
                        var filesToDelete = backupFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(backupFiles.Length - _maxBackupFiles);
                        foreach (var file in filesToDelete)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception deleteEx)
                            {
                                exceptions.Add(new Exception($"Failed to delete old backup file '{file}'.", deleteEx));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException("One or more errors occurred during the save operation. See inner exceptions for details.", exceptions);
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<Entry>> GetAllAsync(EntryStatus? status = null)
    {
        // JSON では _items に全データが入っているので Get* で絞り込んでも意味がない。
        // ワークフローを考え、また、SQL もゆるく想定するなら、状態による絞り込みは、少なくとも SQL では効果的。
        var items = status.HasValue ? _items.Where(i => i.Status == status.Value) : _items;
        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task<Entry?> GetByIdAsync(Guid id)
    {
        // "Get"操作では、項目が見つからないことは例外的な状況ではありません。
        // 呼び出し元が存在を確認できるように、nullを返します。
        var item = _items.FirstOrDefault(i => i.Id == id);
        return Task.FromResult(item);
    }

    /// <inheritdoc />
    public async Task AddAsync(Entry item)
    {
        if (item.Id != Guid.Empty)
        {
            // SQL 系のデータベースで auto-incremented な ID のところに INSERT コマンドで値を指定するとエラーになりうることを参考に。
            // 指定の必要のない GUID を指定するのは、判明すれば2秒で直せる実装ミスなので、厳しめに対応。
            throw new InvalidOperationException("Cannot add an entry that already has an ID.");
        }

        item.Id = Guid.NewGuid();
        _items.Add(item);
        await SaveDataAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Entry item)
    {
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index != -1)
        {
            _items[index] = item;
            await SaveDataAsync();
        }
        else
        {
            throw new KeyNotFoundException($"An entry with ID '{item.Id}' was not found and cannot be updated.");
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
            throw new KeyNotFoundException($"An entry with ID '{id}' was not found and cannot be deleted.");
        }
    }
}
