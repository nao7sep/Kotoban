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
using Kotoban.Core.Utils;

namespace Kotoban.Core.Persistence
{
    /// <summary>
    /// 項目をJSONファイルに永続化するためのリポジトリ実装。
    /// </summary>
    public class JsonEntryRepository : IEntryRepository
    {
        private readonly KotobanSettings _settings;

        /// <summary>
        /// データが格納されているJSONファイルのパス。
        /// </summary>
        public string DataFile { get; }

        /// <summary>
        /// バックアップファイルを保存するディレクトリのパス。
        /// </summary>
        public string BackupDirectory { get; }

        /// <summary>
        /// このリポジトリが使用するバックアップ戦略。
        /// </summary>
        public JsonRepositoryBackupMode BackupMode { get; }

        /// <summary>
        /// 保持するバックアップファイルの最大数。
        /// </summary>
        public int MaxBackupFiles { get; }

        private List<Entry> _items = new();

        /// <summary>
        /// 現在メモリにロードされている項目の読み取り専用リストを取得します。
        /// </summary>
        public IReadOnlyList<Entry> Items => _items.AsReadOnly();

        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// このリポジトリで使用されるJSONシリアライゼーション設定を取得します。
        /// </summary>
        public JsonSerializerOptions JsonOptions => _jsonOptions;

        /// <summary>
        /// JsonEntryRepositoryの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="settings">Kotobanの設定</param>
        /// <exception cref="InvalidOperationException">必須設定が不足している場合</exception>
        public JsonEntryRepository(KotobanSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.JsonDataFile))
            {
                throw new InvalidOperationException("JsonDataFile is required.");
            }
            if (string.IsNullOrWhiteSpace(settings.BackupDirectory))
            {
                throw new InvalidOperationException("BackupDirectory is required.");
            }

            _settings = settings;

            DataFile = _settings.JsonDataFile;
            if (!Path.IsPathFullyQualified(DataFile))
            {
                DataFile = AppPath.GetAbsolutePath(DataFile);
            }

            BackupDirectory = _settings.BackupDirectory;
            if (BackupDirectory.Equals("%TEMP%", StringComparison.OrdinalIgnoreCase))
            {
                BackupDirectory = Path.Combine(Path.GetTempPath(), "Kotoban", "Backups");
            }
            else if (!Path.IsPathFullyQualified(BackupDirectory))
            {
                BackupDirectory = AppPath.GetAbsolutePath(BackupDirectory);
            }

            BackupMode = _settings.BackupMode;
            MaxBackupFiles = _settings.MaxBackupFiles;

            _jsonOptions = new JsonSerializerOptions
            {
                // データ整合性を維持するため、null 値もシリアライズします。
                // DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                // 非ASCII文字をエスケープせず、可読性を高めます。
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

                // JSON出力を整形し、バージョン管理システムでの差分比較を容易にします。
                WriteIndented = true
            };

            // モデルの関心の分離を維持しつつ、enumを文字列としてシリアライズするためにコンバータを追加します。
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
            _jsonOptions.Converters.Add(new MultilineStringConverter());

            // 非同期のコンストラクターはC#の言語仕様としてサポートされていないため、
            // データロードは外部から明示的に呼び出す必要があります。
            // LoadDataAsync();
        }

        /// <summary>
        /// ファイルからデータをロードし、作成日時でソートします。
        /// </summary>
        public async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(DataFile))
            {
                _items = new List<Entry>();
                return;
            }

            var json = await File.ReadAllTextAsync(DataFile, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            // ファイルが空、空白、またはJSONリテラルの "null" の場合、
            // 新しい空のリストとして扱います。
            // "null" リテラルは、デシリアライズされると単一のnull値になります。
            // 不正な形式のJSONが指定された場合は、JsonExceptionがスローされます。
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
        /// アトミックな保存操作のため、一時ファイルを使用します。
        /// </summary>
        private async Task SaveDataAsync(CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();

            // バックアップ
            if (BackupMode == JsonRepositoryBackupMode.CreateCopy)
            {
                try
                {
                    if (File.Exists(DataFile))
                    {
                        Directory.CreateDirectory(BackupDirectory);

                        // タイムスタンプの競合は稀であり、発生した場合でも差分がないため、
                        // 現在のタイムスタンプ精度で十分です。
                        var timestamp = DateTimeUtils.UtcNowTimestamp();
                        var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(DataFile);
                        // バックアップファイルは元の拡張子を維持します。
                        var backupFileName = $"{originalFileNameWithoutExtension}-{timestamp}.json";
                        var backupPath = Path.Combine(BackupDirectory, backupFileName);

                        await FileUtils.CopyAsync(DataFile, backupPath, true, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // アトミックな保存操作：一時ファイルに書き込み、成功したら元ファイルと置き換える
            try
            {
                // ファイル内での一貫した順序を保証するためにリストをソートします
                _items = _items.OrderBy(e => e.CreatedAtUtc).ToList();
                var json = JsonSerializer.Serialize(_items, _jsonOptions);

                // 元ファイルと同じディレクトリに一時ファイルを作成
                DirectoryUtils.EnsureParentDirectoryExists(DataFile);
                var tempFile = DataFile + ".tmp";

                try
                {
                    // 一時ファイルにデータを書き込み
                    await File.WriteAllTextAsync(tempFile, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

                    // 書き込みが成功したら、元ファイルを削除して一時ファイルをリネーム
                    // これによりアトミックな置き換えを実現
                    if (File.Exists(DataFile))
                    {
                        File.Delete(DataFile);
                    }
                    File.Move(tempFile, DataFile);
                }
                catch
                {
                    // 一時ファイルの書き込みまたは置き換えに失敗した場合、
                    // 一時ファイルが残っていれば削除する
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {
                            // 一時ファイルの削除に失敗しても、元の例外を優先する
                        }
                    }
                    throw; // 元の例外を再スロー
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            // クリーンアップ
            if (BackupMode == JsonRepositoryBackupMode.CreateCopy)
            {
                try
                {
                    if (MaxBackupFiles > 0)
                    {
                        Directory.CreateDirectory(BackupDirectory);
                        var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(DataFile);
                        var backupFiles = Directory.GetFiles(BackupDirectory)
                            .Where(f => Path.GetFileName(f).StartsWith(originalFileNameWithoutExtension + "-") &&
                                        Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        if (backupFiles.Length > MaxBackupFiles)
                        {
                            var filesToDelete = backupFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(backupFiles.Length - MaxBackupFiles);
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
        public Task<IEnumerable<Entry>> GetAllAsync(EntryStatus? status = null, CancellationToken cancellationToken = default)
        {
            // この実装は全データをメモリ内でフィルタリングしますが、
            // SQLなど他の永続化層では、この絞り込みがパフォーマンスに寄与する可能性があります。
            var items = status.HasValue ? _items.Where(i => i.Status == status.Value) : _items;
            return Task.FromResult(items);
        }

        /// <inheritdoc />
        public Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // "Get"操作において項目が見つからないのは通常の動作であり、例外ではありません。
            // 呼び出し元が存在を確認できるよう、nullを返します。
            var item = _items.FirstOrDefault(i => i.Id == id);
            return Task.FromResult(item);
        }

        /// <inheritdoc />
        public async Task AddAsync(Entry item, CancellationToken cancellationToken = default)
        {
            if (item.Id != Guid.Empty)
            {
                // データベースの自動インクリメントIDと同様に、
                // 新規追加される項目にはIDが事前に割り当てられていてはなりません。
                throw new InvalidOperationException("Cannot add an entry that already has an ID.");
            }

            item.Id = Guid.NewGuid();
            _items.Add(item);
            await SaveDataAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UpdateAsync(Entry item, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index != -1)
            {
                _items[index] = item;
                await SaveDataAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new KeyNotFoundException($"An entry with ID '{item.Id}' was not found and cannot be updated.");
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                await SaveDataAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new KeyNotFoundException($"An entry with ID '{id}' was not found and cannot be deleted.");
            }
        }
    }
}
