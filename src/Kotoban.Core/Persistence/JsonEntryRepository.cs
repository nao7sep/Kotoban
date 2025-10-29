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
                // 誰にも送信しないデータなので、データ量の節約だったり、
                // null にしてデフォルト値にフォールバックさせたりは、ここでは不要。
                // DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                // こうしておかないと、ローカルで差分を取るのが絶望的に。
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

                // 同じく差分の取りやすさのため。
                WriteIndented = true
            };

            // ここで JsonStringEnumConverter を追加するのは、2つの理由からです。
            // 1. Entry モデル内の全ての enum (Status プロパティと Explanations ディクショナリのキーである ExplanationLevel) を文字列としてシリアライズ/デシリアライズするため。
            // 2. モデルクラス自体に特定のシリアライズ形式 (JSON) の詳細が漏れ出すのを防ぎ、関心の分離を維持するため。
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
            _jsonOptions.Converters.Add(new MultilineStringConverter());

            // コンストラクターを async にできないので、今後は外側でこれを呼び出す。
            // LoadDataAsync();
        }

        /// <summary>
        /// ファイルからデータをロードし、作成日時でソートします。
        /// </summary>
        public async Task LoadDataAsync()
        {
            if (!File.Exists(DataFile))
            {
                _items = new List<Entry>();
                return;
            }

            var json = await File.ReadAllTextAsync(DataFile, Encoding.UTF8);

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
        /// アトミックな保存操作のため、一時ファイルを使用します。
        /// </summary>
        private async Task SaveDataAsync()
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

                        // 1秒に2回以上のバックアップが行われるケースを想定しにくいので、タイムスタンプの精度はこれで十分。
                        // 万が一にもそういうことがあったなら、差分がなく無意味なバックアップだろうし、上書き保存なのでたぶん落ちない。
                        var timestamp = DateTimeUtils.UtcNowTimestamp();
                        var originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(DataFile);
                        // バックアップディレクトリに保存するので、拡張子を .bak などにしない。
                        var backupFileName = $"{originalFileNameWithoutExtension}-{timestamp}.json";
                        var backupPath = Path.Combine(BackupDirectory, backupFileName);

                        await FileUtils.CopyAsync(DataFile, backupPath, true);
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
                    await File.WriteAllTextAsync(tempFile, json, Encoding.UTF8);

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
}
