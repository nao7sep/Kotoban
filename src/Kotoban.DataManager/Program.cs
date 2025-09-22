using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Services.OpenAi;
using Kotoban.Core.Services.Web;
using Kotoban.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Kotoban.DataManager;

/// <summary>
/// Kotobanデータ管理用のコンソールアプリケーション。
/// </summary>
public class Program
{
    // 指定項目の AI コンテンツの状態に基づく動的メニューに使われる。
    // これがないと、メニュー項目の文字列での switch になる。
    private enum AiContentAction
    {
        Generate,
        Regenerate,
        Approve,
        Delete,
        Exit
    }

    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // =============================================================================

        // Serilogの手動セットアップ（ファイルロギング用）
        var timestamp = DateTimeUtils.UtcNowTimestamp();
        var logFilePath = AppPath.GetAbsolutePath(Path.Combine("Logs", $"Kotoban-{timestamp}.log"));
        DirectoryUtils.EnsureParentDirectoryExists(logFilePath);

        // 蛇足コメント: Serilog についてよく知らず、Log.Logger にインスタンスをあてがっては、それをサービス登録していた。
        // コードの後半では Log.Error などを使っていて、たぶん動作は今と同じだったが、DI の徹底により派生開発耐性をつける今の手法とは違った。

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
            .WriteTo.File(logFilePath)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger, dispose: true);

        // =============================================================================

        // 単一 UI に単一セットのデータが関連づけられる設計なので、そのデータもサービス登録してしまう。
        // AI がやりだしたときには、「Program.メンバー」または「ほかのクラス.静的メンバー」でよいのではと思った。
        // しかし、Scoped, Singleton, Transient の違いを学び、サービス登録による拡張性を理解した。

        var kotobanSettings = new KotobanSettings();
        // このBindは、"Kotoban"セクションが存在しない場合や値がマッピングできない場合でも例外を投げず、kotobanSettingsのプロパティはデフォルト値のままになります。
        builder.Configuration.GetSection("Kotoban").Bind(kotobanSettings);
        builder.Services.AddSingleton(kotobanSettings);

        // データファイルパスの処理
        var dataFilePath = kotobanSettings.DataFilePath;
        if (!Path.IsPathFullyQualified(dataFilePath))
        {
            dataFilePath = AppPath.GetAbsolutePath(dataFilePath);
        }

        // バックアップディレクトリの処理（%TEMP%プレースホルダーの処理）
        var backupDirectory = kotobanSettings.BackupDirectory;
        if (string.Equals(backupDirectory, "%TEMP%", StringComparison.OrdinalIgnoreCase))
        {
            backupDirectory = Path.Combine(Path.GetTempPath(), "Kotoban", "Backups");
        }
        else if (!Path.IsPathFullyQualified(backupDirectory))
        {
            backupDirectory = AppPath.GetAbsolutePath(backupDirectory);
        }

        builder.Services.AddSingleton<IEntryRepository>(provider =>
        {
            return new JsonEntryRepository(
                dataFilePath,
                JsonRepositoryBackupMode.CreateCopyInTemp,
                backupDirectory,
                kotobanSettings.MaxBackupFiles
            );
        });

        // =============================================================================

        var openAiSettings = new OpenAiSettings();
        // このBindは、"OpenAi"セクションが存在しない場合や値がマッピングできない場合でも例外を投げず、openAiSettingsのプロパティはデフォルト値のままになります。
        builder.Configuration.GetSection("OpenAi").Bind(openAiSettings);
        builder.Services.AddSingleton(openAiSettings);

        // OpenAiSettings は DI により OpenAiNetworkSettings や OpenAiRequestFactory のコンストラクタに自動的に注入されます。
        builder.Services.AddSingleton<OpenAiNetworkSettings>();
        // OpenAiTransportContext は「トランスポート層の責務」を分離するため個別クラス化しています。
        // 認証やエンドポイントなど「リクエストモデル」や「ネットワーク設定モデル」に収まらない情報をまとめる用途です。
        // このアプリではインスタンスが複数必要になる場面はないため、シングルトンで登録しています（シンプルさ優先）。
        builder.Services.AddSingleton<OpenAiTransportContext>();
        builder.Services.AddSingleton<OpenAiRequestFactory>();

        // AddHttpClient() は IHttpClientFactory をDIコンテナに登録し、HttpClientのライフサイクル管理や拡張機能を有効にします。
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<OpenAiApiClient>();
        builder.Services.AddSingleton<WebClient>();

        // =============================================================================

        // サービス登録が終われば、ビルドしてホストを取る。
        // こっからは、この構成でいきまっせ～と。
        var host = builder.Build();

        // ここで logger と先ほどの serilogLogger の違いをちゃんと理解しておくことは非常に重要。
        // （マイクを GPT-4.1 に）。

        // serilogLogger は Serilog の生のロガーインスタンスであり、Serilog 独自の API（Write, Information, Error など）を直接使ってログ出力できます。
        // 一方、logger（ILogger<Program>）は Microsoft.Extensions.Logging の抽象ロガーで、DI（依存性注入）経由で取得し、アプリ全体で統一的に利用することが推奨されます。
        //
        // AddSerilog で Serilog をロギングプロバイダーとして登録しているため、ILogger<T> で出力したログも最終的には serilogLogger によって処理され、
        // Serilog の設定（出力先・フォーマット・フィルタなど）が適用されます。
        //
        // ここで <Program> となっているのは「ロガーのカテゴリ名」として型名（この場合は "Kotoban.DataManager.Program"）が自動的に付与されるためです。
        // これにより、ログ出力時に「どのクラスから出たログか」を Serilog 側で判別でき、ログのフィルタリングや出力フォーマットでカテゴリごとの制御が可能になります。
        //
        // まとめ：
        //   - アプリケーションコードでは serilogLogger を直接使わず、ILogger<T>（ここでは ILogger<Program>）を使うのがベストプラクティス。
        //   - ILogger<T> を使うことで、.NET 標準のロギングAPIの恩恵（DI, カテゴリ分け, テスト容易性など）と Serilog の高機能な出力を両立できる。
        //   - <T> には通常「現在のクラス名」を指定し、カテゴリごとにログを分けることで、運用・保守・分析がしやすくなる。

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            // 長々と書いたが、このブロックのほとんどはアプリ名とバージョンの取得。
            // 今のところほかで必要でない情報なので、ここにベタ書き。

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyTitle = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
            if (string.IsNullOrWhiteSpace(assemblyTitle))
            {
                throw new InvalidOperationException("Assembly title is not defined.");
            }

            var version = assembly.GetName().Version;
            if (version == null)
            {
                throw new InvalidOperationException("Assembly version is not defined.");
            }
            var versionString = version.Build == 0 ? $"{version.Major}.{version.Minor}" : version.ToString(3);

            Console.WriteLine($"{assemblyTitle} v{versionString}");
            Console.WriteLine($"Data file: {dataFilePath}");
            logger.LogInformation("Application starting.");

            // ここで host を丸ごと渡すのはベストプラクティスでないと。
            await RunApplicationLoop(host.Services);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "An unhandled exception occurred during application execution.");
        }
        finally
        {
            logger.LogInformation("Application shutting down.");

            // SerilogのLoggerはAddSerilog(dispose: true)で登録しているため、
            // ホストのDispose時に自動的にフラッシュ・クローズされる。
            // そのため、ここでCloseAndFlushAsyncを明示的に呼ぶ必要はありません。

            // AI コメントに追記: Log を使っていたときには必要だったこと。
            // 静的プロパティーなので、閉じてフラッシュするタイミングが自分では分からない。
            // しかし、サービス登録すれば、施設管理者が「そろそろ片づけろ」と言う。
            //
            // 静的プロパティーにデータを放り込んでいく構成は、初期化のタイミングが Lazy 頼みになったり、
            // dispose のことを「プロセスが消えるときにどうせ」と開き直ったりになりがち。
            // そのあたりもスマートにできそうで、今後の開発ではデフォルトでこのデザインパターンを採用できそう。

            // await logger.CloseAndFlushAsync();

            // Mac で ReadKey が例外を投げたので、ReadLine に変更した。
            // https://github.com/nao7sep/coding-notes/blob/main/understanding-the-console-readkey-exception-in-a-dotnet-async-finally-block-on-macos.md
            Console.Write("Enterキーを押して終了します...");
            Console.ReadLine();
        }
    }

    private static async Task RunApplicationLoop(IServiceProvider services)
    {
        // scope とは、app-wide でなく、その中の特定のライフサイクルのこと。
        // WPF の MainWindows がイメージとして近そう。
        // それがつくられる前にも、それが破棄されたあとにも、プロセスは存在する。
        // そこで使うサービスとは別に、MainWindow のスコープでも、そのためのサービスが用意される。
        // こうやっておけば、たとえば、RunApplicationLoop を複数回実行しても、app-wide なサービスに影響しない。
        //
        // という構成により、このメソッドの内部で呼ばれるメソッドには、scope 用の services が渡される。
        // app-wide のものとの区別のため、メソッドの引数名も scopedServices とすることを考えたが、
        // scoped がついていてもいなくても受け取るもの次第なので、ただ冗長になる。

        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var logger = scopedServices.GetRequiredService<ILogger<Program>>();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== メインメニュー ===");
            Console.WriteLine("1. 項目を追加する");
            Console.WriteLine("2. データを仕上げる"); // AIコンテンツの生成と項目の承認を流れ作業で。
            Console.WriteLine("3. 項目のリストを表示する");
            Console.WriteLine("4. 項目を表示・更新する");
            Console.WriteLine("5. 項目を削除する");
            Console.WriteLine("6. 終了する");
            Console.Write("選択してください: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await AddItemAsync(scopedServices);
                        break;
                    case "2":
                        await FinalizeAllItemsAsync(scopedServices);
                        break;
                    case "3":
                        await ViewAllItemsAsync(scopedServices);
                        break;
                    case "4":
                        await UpdateItemAsync(scopedServices);
                        break;
                    case "5":
                        await DeleteItemAsync(scopedServices);
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("無効な選択です。もう一度お試しください。");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during the operation.");
            }
        }
    }

    private static async Task<bool> CheckForDuplicatesAsync(IServiceProvider services, string reading, Guid? excludeId = null)
    {
        var repository = services.GetRequiredService<IEntryRepository>();

        var allItems = await repository.GetAllAsync();
        var query = allItems.Where(e => string.Equals(e.Reading, reading, StringComparison.OrdinalIgnoreCase));

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        var duplicates = query.ToList();

        if (duplicates.Any())
        {
            Console.WriteLine("同じ読みがなを持つ既存の項目が見つかりました。");
            foreach (var dup in duplicates)
            {
                Console.WriteLine($"ID: {dup.Id} | 用語: {GetDisplayText(dup)} | ステータス: {dup.Status}");
            }

            var proceed = ReadString("このまま続行しますか？ (y/n): ", "n");
            if (!string.Equals(proceed, "y", StringComparison.OrdinalIgnoreCase))
            {
                return false; // キャンセル
            }
        }

        return true; // 続行
    }

    private static async Task AddItemAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();

        Console.WriteLine();
        Console.WriteLine("=== 項目の追加 ===");

        var newItem = new Entry();

        while (true)
        {
            var reading = ReadString("読みがな: ");
            if (!string.IsNullOrWhiteSpace(reading))
            {
                newItem.Reading = reading.Trim();
                break;
            }
            Console.WriteLine("読みがなは必須です。");
        }

        if (!await CheckForDuplicatesAsync(services, newItem.Reading))
        {
            Console.WriteLine("項目の追加をキャンセルしました。");
            return;
        }

        newItem.Expression = ReadString("表記 (オプション): ");
        newItem.GeneralContext = ReadString("一般的なコンテキスト (オプション): ");
        newItem.UserNote = ReadString("ユーザーメモ (オプション): ");
        newItem.Status = EntryStatus.PendingAiGeneration;
        newItem.CreatedAtUtc = DateTime.UtcNow;

        await repository.AddAsync(newItem);
        Console.WriteLine($"項目 '{GetDisplayText(newItem)}' が追加されました。ID: {newItem.Id}");
        await ShowAiContentMenuAsync(newItem, services);
    }

    private static async Task FinalizeAllItemsAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine();
        Console.WriteLine("=== 未承認項目の仕上げ ===");

        var allItems = await repository.GetAllAsync();
        var itemsToFinalize = allItems
            .Where(i => i.Status != EntryStatus.Approved)
            .OrderBy(i => i.CreatedAtUtc)
            .ToList();

        if (!itemsToFinalize.Any())
        {
            Console.WriteLine("すべての項目が承認済みです。");
            return;
        }

        Console.WriteLine($"未承認の項目が{itemsToFinalize.Count}件見つかりました。");

        var currentItemIndex = 0;
        while (currentItemIndex < itemsToFinalize.Count)
        {
            // itemsToFinalize の項目をそのまま読むことも可能だが、レポジトリーパターンを尊重し、常に最新情報を取得する。
            var currentItem = await repository.GetByIdAsync(itemsToFinalize[currentItemIndex].Id);
            if (currentItem == null || currentItem.Status == EntryStatus.Approved)
            {
                currentItemIndex++;
                continue;
            }

            // 仕上げ時には、たとえば更新後にもう一度読み返すなども有益なので、毎回表示。
            PrintItemDetails(currentItem);

            Console.WriteLine();
            Console.WriteLine("=== 仕上げメニュー ===");
            Console.WriteLine("1. 項目データを更新する");
            Console.WriteLine("2. AIコンテンツを管理する");
            Console.WriteLine("3. 次の項目へ");
            Console.WriteLine("4. 仕上げプロセスを終了する");
            var choice = ReadString("選択してください: ");

            switch (choice)
            {
                case "1": // 項目データを更新する
                    // いきなり「新しい値を～」より、空行に続けて、どのモードに入ったのか明示した方が分かりやすい。
                    Console.WriteLine();
                    Console.WriteLine("=== 項目の更新 ===");
                    await UpdateItemCoreAsync(currentItem, services, showAiMenu: false);
                    break;
                case "2": // AIコンテンツを管理する
                    await ShowAiContentMenuAsync(currentItem, services, printItemDetails: false);
                    break;
                case "3": // 次の項目へ
                    // 最終確認をしてから次へ進めるようにしておく。
                    // それを確認しての移動なので、ここで追加の表示は不要。
                    currentItemIndex++;
                    break;
                case "4": // 仕上げプロセスを終了する
                    Console.WriteLine("仕上げプロセスを終了します。");
                    return;
                default:
                    Console.WriteLine("無効な選択です。");
                    break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("すべての未承認項目の処理が完了しました。");
    }

    private static async Task ViewAllItemsAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();

        Console.WriteLine();
        Console.WriteLine("=== 項目一覧 ===");
        Console.WriteLine("どのステータスの項目を表示しますか？");
        Console.WriteLine("1. すべて");
        Console.WriteLine($"2. AI生成待ち ({EntryStatus.PendingAiGeneration})");
        Console.WriteLine($"3. 承認待ち ({EntryStatus.PendingApproval})");
        Console.WriteLine($"4. 承認済み ({EntryStatus.Approved})");
        var choice = ReadString("選択してください [1]: ", "1");

        IEnumerable<Entry> itemsToShow;

        switch (choice)
        {
            case "2":
                itemsToShow = await repository.GetAllAsync(EntryStatus.PendingAiGeneration);
                break;
            case "3":
                itemsToShow = await repository.GetAllAsync(EntryStatus.PendingApproval);
                break;
            case "4":
                itemsToShow = await repository.GetAllAsync(EntryStatus.Approved);
                break;
            default:
                itemsToShow = await repository.GetAllAsync();
                break;
        }

        var entryList = itemsToShow.ToList();
        if (!entryList.Any())
        {
            Console.WriteLine("表示する項目がありません。");
            return;
        }

        // 1行ずつの入力や出力のところでは空行は不要だが、リスト表示の直前にないと違和感がある。
        Console.WriteLine();

        foreach (var item in entryList)
        {
            Console.WriteLine($"ID: {item.Id} | 用語: {GetDisplayText(item)} | ステータス: {item.Status}");
        }
    }

    private static async Task<Entry?> SelectItemAsync(IServiceProvider services, string prompt)
    {
        var repository = services.GetRequiredService<IEntryRepository>();

        var input = ReadString(prompt);
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("操作をキャンセルしました。");
            return null;
        }

        if (Guid.TryParse(input, out var id))
        {
            var item = await repository.GetByIdAsync(id);
            if (item == null)
            {
                Console.WriteLine($"ID '{id}' の項目が見つかりませんでした。");
            }
            return item;
        }
        else
        {
            var allItems = await repository.GetAllAsync();
            var matchingItems = allItems
                .Where(i => i.Reading.Equals(input.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingItems.Count == 0)
            {
                Console.WriteLine($"読みがな '{input}' に一致する項目が見つかりませんでした。");
                return null;
            }
            else if (matchingItems.Count == 1)
            {
                return matchingItems[0];
            }
            else
            {
                Console.WriteLine($"読みがな '{input}' に一致する項目が複数見つかりました。");
                for (int i = 0; i < matchingItems.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {GetDisplayText(matchingItems[i])} (ID: {matchingItems[i].Id})");
                }
                var choiceStr = ReadString("項目を選択してください: ");
                if (int.TryParse(choiceStr, out var choiceInt) && choiceInt > 0 && choiceInt <= matchingItems.Count)
                {
                    return matchingItems[choiceInt - 1];
                }
                else
                {
                    Console.WriteLine("無効な選択です。操作をキャンセルしました。");
                    return null;
                }
            }
        }
    }

    private static async Task UpdateItemAsync(IServiceProvider services)
    {
        Console.WriteLine();
        Console.WriteLine("=== 項目の表示・更新 ===");

        var itemToUpdate = await SelectItemAsync(services, "表示・更新する項目のIDまたは読みがな: ");
        if (itemToUpdate == null)
        {
            // null が返った理由は、SelectItemAsync により表示される。
            return;
        }

        await UpdateItemCoreAsync(itemToUpdate, services, showAiMenu: true);
    }

    // FinalizeAllItemsAsync の実装のため、UpdateItemAsync のコア部分を切り出した。
    // それ以外に、このメソッドが存在するデザイン上の理由はない。
    private static async Task UpdateItemCoreAsync(Entry item, IServiceProvider services, bool showAiMenu)
    {
        var repository = services.GetRequiredService<IEntryRepository>();

        var originalReading = item.Reading;
        var originalExpression = item.Expression;
        var originalGeneralContext = item.GeneralContext;
        var originalUserNote = item.UserNote;

        Console.WriteLine("新しい値を入力してください（変更しない場合はEnter）:");

        var newReading = ReadString($"読みがな [{item.Reading}]: ", item.Reading);

        // 読みがなは並び替えに使われるものなので、add/update の両方でトリムされる。
        // ほかは、前後に空白が入ろうと実害が軽微なので、トリム沼を回避しておく。
        // 必須データなので、有効な文字列が得られなかったなら元の値にフォールバック。
        newReading = newReading?.Trim();
        if (string.IsNullOrWhiteSpace(newReading))
            newReading = item.Reading;

        if (newReading != originalReading)
        {
            if (!await CheckForDuplicatesAsync(services, newReading, item.Id))
            {
                Console.WriteLine("項目の更新をキャンセルしました。");
                return;
            }
        }

        var newExpression = ReadString($"表記 [{item.Expression ?? "なし"}]: ", item.Expression);
        var newGeneralContext = ReadString($"一般的なコンテキスト [{item.GeneralContext ?? "なし"}]: ", item.GeneralContext);
        var newUserNote = ReadString($"ユーザーメモ [{item.UserNote ?? "なし"}]: ", item.UserNote);

        var dataHasChanged = newReading != originalReading ||
                             newExpression != originalExpression ||
                             newGeneralContext != originalGeneralContext ||
                             newUserNote != originalUserNote;

        if (dataHasChanged)
        {
            item.Reading = newReading;
            item.Expression = newExpression;
            item.GeneralContext = newGeneralContext;
            item.UserNote = newUserNote;

            bool hadAiContent = item.Status != EntryStatus.PendingAiGeneration;

            // まずテキストの変更を保存。
            // DeleteAiContentAsync も保存を行うため、二度手間になっていると AI に怒られることがある。
            // これは仕様。
            //
            // UpdateAsync は保存「のみ」を行うと確約されたものでなく、むしろ persistent storage を意識せず「項目の更新」を行うもの。
            // 実装を追えば確かに瞬間的に二度の保存になって無駄だが、
            // ロジックで考えるなら、保存の処理を伴うかどうか「分からない」UpdateAsync をここで呼ぶことは誤りでない。
            //
            // エントリーの更新という低頻度の処理において微々たるコストが発生するが、論理的な正しさをここでは優先。
            // DeleteAiContentAsync を「削除」と「保存」に分ける選択肢もあるが、そこまでつくり込むこともないのでこのへんで。

            await repository.UpdateAsync(item);
            Console.WriteLine("項目を更新しました。");

            if (hadAiContent)
            {
                await DeleteAiContentAsync(item, services, "項目データが変更されたため、既存のAIコンテンツはクリアされました。");
            }
        }

        if (showAiMenu)
        {
            await ShowAiContentMenuAsync(item, services);
        }
    }

    private static async Task DeleteItemAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();

        Console.WriteLine();
        Console.WriteLine("=== 項目の削除 ===");

        var itemToDelete = await SelectItemAsync(services, "削除する項目のIDまたは読みがな: ");
        if (itemToDelete == null)
        {
            // null が返った理由は、SelectItemAsync により表示される。
            return;
        }

        Console.Write($"本当に '{GetDisplayText(itemToDelete)}' を削除しますか？ (y/n): ");
        var confirmation = Console.ReadLine();

        if (confirmation?.ToLower() == "y")
        {
            // データベースから削除する前に、関連ファイルをクリーンアップ。
            // DeleteAiContentAsync は特殊になっていて、メッセージを出力しないことも可能。
            // ここのように、もっと大きなまとまりが消されたなら、AI コンテンツが消されたと出力する必要はない。
            await DeleteAiContentAsync(itemToDelete, services, completionMessage: null);
            await repository.DeleteAsync(itemToDelete.Id);
            Console.WriteLine("項目が削除されました。");
        }
        else
        {
            Console.WriteLine("削除はキャンセルされました。");
        }
    }

    private static async Task ShowAiContentMenuAsync(Entry item, IServiceProvider services, bool printItemDetails = true)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();

        if (printItemDetails)
        {
            // ループで毎回表示するとうるさいので、最初に一度だけ表示。
            PrintItemDetails(item);
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== AIコンテンツ管理メニュー ===");

            var options = new Dictionary<string, (AiContentAction Action, string DisplayText)>();
            var optionIndex = 1;

            if (item.Status == EntryStatus.PendingAiGeneration)
            {
                options.Add(optionIndex++.ToString(), (AiContentAction.Generate, "AIコンテンツを生成する"));
            }
            else
            {
                options.Add(optionIndex++.ToString(), (AiContentAction.Regenerate, "AIコンテンツを再生成する"));
                if (item.Status == EntryStatus.PendingApproval)
                {
                    options.Add(optionIndex++.ToString(), (AiContentAction.Approve, "AIコンテンツを承認する"));
                }
                options.Add(optionIndex++.ToString(), (AiContentAction.Delete, "AIコンテンツを削除する"));
            }
            options.Add(optionIndex.ToString(), (AiContentAction.Exit, "メインメニューに戻る"));

            foreach (var opt in options)
            {
                Console.WriteLine($"{opt.Key}. {opt.Value.DisplayText}");
            }
            var choice = ReadString("選択してください: ");

            if (choice == null || !options.TryGetValue(choice, out var selectedOption))
            {
                Console.WriteLine("無効な選択です。");
                continue;
            }

            try
            {
                var selectedAction = selectedOption.Action;
                switch (selectedAction)
                {
                    case AiContentAction.Generate:
                    case AiContentAction.Regenerate:
                        await GenerateOrUpdateAiContentAsync(item, services, selectedAction);
                        break;
                    case AiContentAction.Approve:
                        await ApproveAiContentAsync(item, services);
                        return; // 承認後はメニューを抜ける
                    case AiContentAction.Delete:
                        await DeleteAiContentAsync(item, services, "AIコンテンツが削除されました。");
                        break;
                    case AiContentAction.Exit:
                        return;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred in the AI Content Menu.");
            }
        }
    }

    private static async Task GenerateOrUpdateAiContentAsync(Entry item, IServiceProvider services, AiContentAction action)
    {
        Console.WriteLine();
        var actionText = action == AiContentAction.Generate ? "生成" : "再生成";
        Console.WriteLine($"AIコンテンツの{actionText}機能は現在実装されていません。");
        await Task.CompletedTask;
    }

    private static async Task ApproveAiContentAsync(Entry item, IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        item.Approve();
        await repository.UpdateAsync(item);
        Console.WriteLine("コンテンツが承認されました。");
    }

    /// <summary>
    /// ほかのメソッドと異なり、完了時のメッセージがオプションになっている。付近のものとかぶってうるさくなるなら、こちらを黙らせる。
    /// </summary>
    private static async Task DeleteAiContentAsync(Entry item, IServiceProvider services, string? completionMessage)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // 画像ファイルの物理削除
        if (!string.IsNullOrWhiteSpace(item.RelativeImagePath))
        {
            var imagePath = AppPath.GetAbsolutePath(item.RelativeImagePath);
            if (File.Exists(imagePath))
            {
                // 迷ったが、例外をキャッチせず、ログへの書き込みも行わず、
                // ここで問題があればエントリーの削除が行われないように変更した。
                //
                // 基本的な構造として、このアプリでは、メインメニューと AI メニューの二つが try/catch で何でもキャッチする。
                // そのため、それぞれのコマンドのコードはシンプルになっていて、何かあれば、すぐそこで処理が止まる。
                //
                // このメソッドも、ファイルを消せなければ項目も消さない方が、パソコンの再起動だけで問題を解決できるだろう。
                // 逆に、ファイルが残ったのに項目を消しては、そのままユーザーが忘れることで、いわゆる orphan file が残る。

                File.Delete(imagePath);
            }
        }

        // エントリのビジネスロジックを呼び出してフィールドをクリア
        item.ClearAiContent();

        await repository.UpdateAsync(item);

        if (!string.IsNullOrWhiteSpace(completionMessage))
        {
            Console.WriteLine(completionMessage);
        }
    }

    #region ヘルパーメソッド

    private static string GetDisplayText(Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Expression))
        {
            return $"{entry.Reading} ({entry.Expression})";
        }
        return entry.Reading;
    }

    /// <summary>
    /// トリミングなしで返すので、読み取り側で適宜。
    /// 読み取るメソッドがトリミングも行うと呼び出し側の選択肢が減る。
    /// </summary>
    private static string? ReadString(string prompt, string? defaultValue = null)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    private static Guid? ReadGuid(string prompt)
    {
        while (true)
        {
            var input = ReadString(prompt);
            if (string.IsNullOrWhiteSpace(input)) return null;
            if (Guid.TryParse(input, out var result))
            {
                return result;
            }
            Console.WriteLine("無効なGUID形式です。もう一度お試しください。");
        }
    }

    private static void PrintItemDetails(Entry item)
    {
        Console.WriteLine();
        Console.WriteLine("=== 項目の詳細 ===");
        Console.WriteLine($"ID: {item.Id}");
        Console.WriteLine($"用語: {GetDisplayText(item)}");
        Console.WriteLine($"一般的なコンテキスト: {item.GeneralContext ?? "なし"}");
        Console.WriteLine($"説明生成用のコンテキスト: {item.ExplanationContext ?? "なし"}");
        Console.WriteLine($"画像生成用のコンテキスト: {item.ImageContext ?? "なし"}");
        Console.WriteLine($"ユーザーメモ: {item.UserNote ?? "なし"}");
        Console.WriteLine($"ステータス: {item.Status}");

        Console.WriteLine();
        Console.WriteLine("=== タイムスタンプ ===");
        Console.WriteLine($"作成日時: {DateTimeUtils.FormatForDisplay(item.CreatedAtUtc)}");
        Console.WriteLine($"説明生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ExplanationGeneratedAtUtc)}");
        Console.WriteLine($"画像生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ImageGeneratedAtUtc)}");
        Console.WriteLine($"承認日時: {DateTimeUtils.FormatNullableForDisplay(item.ApprovedAtUtc)}");

        Console.WriteLine();
        Console.WriteLine("=== 説明 ===");
        if (item.Explanations.Any())
        {
            foreach (var (level, text) in item.Explanations)
            {
                Console.WriteLine($"{level}: {text}");
            }
        }
        else
        {
            Console.WriteLine("なし");
        }

        Console.WriteLine();
        Console.WriteLine("=== 画像 ===");
        Console.WriteLine($"画像ファイルパス: {item.RelativeImagePath ?? "なし"}");
        Console.WriteLine($"画像プロンプト: {item.ImagePrompt ?? "なし"}");
    }

    #endregion
}