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

        var dataFilePath = builder.Configuration["DataFilePath"] ?? "Kotoban-Data.json";
        if (!Path.IsPathFullyQualified(dataFilePath))
        {
            dataFilePath = AppPath.GetAbsolutePath(dataFilePath);
        }
        var maxBackupFiles = builder.Configuration.GetValue("MaxBackupFiles", 100);

        builder.Services.AddSingleton<IEntryRepository>(provider =>
        {
            return new JsonEntryRepository(
                dataFilePath,
                JsonRepositoryBackupMode.CreateCopyInTemp,
                maxBackupFiles
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
            if (string.IsNullOrEmpty(assemblyTitle))
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
            Console.WriteLine("2. すべての項目を表示する");
            Console.WriteLine("3. 項目を更新する");
            Console.WriteLine("4. 項目を削除する");
            Console.WriteLine("5. 終了する");
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
                        await ViewAllItemsAsync(scopedServices);
                        break;
                    case "3":
                        await UpdateItemAsync(scopedServices);
                        break;
                    case "4":
                        await DeleteItemAsync(scopedServices);
                        break;
                    case "5":
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

    private static async Task AddItemAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine();
        Console.WriteLine("=== 項目の追加 ===");

        var newItem = new Entry();

        while (true)
        {
            var term = ReadString("用語: ", string.Empty);
            if (!string.IsNullOrWhiteSpace(term))
            {
                newItem.Term = term;
                break;
            }
            Console.WriteLine("用語は必須です。");
        }

        newItem.ContextForAi = ReadString("AI用のコンテキスト (オプション): ");
        newItem.UserNote = ReadString("ユーザーメモ (オプション): ");

        newItem.Status = EntryStatus.PendingAiGeneration;
        newItem.CreatedAtUtc = DateTime.UtcNow;

        await repository.AddAsync(newItem);
        Console.WriteLine($"項目 '{newItem.Term}' が追加されました。ID: {newItem.Id}");

        await ShowAiContentMenuAsync(newItem, services);
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
        Console.Write("選択してください [1]: ");

        var choice = ReadString(string.Empty, "1");
        var allItems = await repository.GetAllAsync();
        IEnumerable<Entry> itemsToShow;

        switch (choice)
        {
            case "2":
                itemsToShow = allItems.Where(i => i.Status == EntryStatus.PendingAiGeneration);
                break;
            case "3":
                itemsToShow = allItems.Where(i => i.Status == EntryStatus.PendingApproval);
                break;
            case "4":
                itemsToShow = allItems.Where(i => i.Status == EntryStatus.Approved);
                break;
            default:
                itemsToShow = allItems;
                break;
        }

        var entryList = itemsToShow.ToList();
        if (!entryList.Any())
        {
            Console.WriteLine("表示する項目がありません。");
            return;
        }

        foreach (var item in entryList)
        {
            Console.WriteLine($"ID: {item.Id} | 用語: {item.Term} | ステータス: {item.Status}");
        }

        Console.WriteLine();
        Console.Write("詳細を表示したい項目のIDを入力してください（スキップはEnter）: ");
        var idInput = Console.ReadLine();
        if (Guid.TryParse(idInput, out var id))
        {
            // フィルタリングされたリストではなく、リポジトリから直接IDで項目を取得する必要がある
            var itemToShow = await repository.GetByIdAsync(id);
            if (itemToShow != null)
            {
                PrintItemDetails(itemToShow);
            }
            else
            {
                Console.WriteLine("指定されたIDの項目が見つかりません。");
            }
        }
    }

    private static async Task UpdateItemAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine();
        Console.WriteLine("=== 項目の更新 ===");
        var id = ReadGuid("更新する項目のID: ");
        if (id == Guid.Empty) return;

        var item = await repository.GetByIdAsync(id);
        if (item == null)
        {
            Console.WriteLine("指定されたIDの項目が見つかりません。");
            return;
        }

        PrintItemDetails(item);
        Console.WriteLine("新しい値を入力してください（変更しない場合はEnter）:");

        var originalTerm = item.Term;
        var originalContextForAi = item.ContextForAi;
        var originalUserNote = item.UserNote;

        var newTerm = ReadString($"用語 [{item.Term}]: ", item.Term);
        var newContextForAi = ReadString($"AI用のコンテキスト [{item.ContextForAi ?? "なし"}]: ", item.ContextForAi);
        var newUserNote = ReadString($"ユーザーメモ [{item.UserNote ?? "なし"}]: ", item.UserNote);

        var dataHasChanged = newTerm != originalTerm ||
                             newContextForAi != originalContextForAi ||
                             newUserNote != originalUserNote;

        if (dataHasChanged)
        {
            item.Term = newTerm ?? item.Term;
            item.ContextForAi = newContextForAi;
            item.UserNote = newUserNote;

            bool hadAiContent = item.Status != EntryStatus.PendingAiGeneration;

            // まずテキストの変更を保存。
            // DeleteAiContentAsync も保存を行うため、二度手間になっていると AI に怒られることがある。
            // これは仕様。
            // UpdateAsync は保存「のみ」を行うと確約されたものでなく、むしろ persistent storage を意識せず「項目の更新」を行うもの。
            // 実装を追えば確かに瞬間的に二度の保存になって無駄だが、ロジックで考えるなら、保存の処理を伴うかどうか「分からない」UpdateAsync をここで呼ぶことは誤りでない。
            // エントリーの更新という稀でない処理において微々たるコストが発生するが、論理的な正しさをここでは優先。
            // DeleteAiContentAsync を「削除」と「保存」に分ける選択肢もあるが、そこまでつくり込むこともないのでこのへんで。
            await repository.UpdateAsync(item);
            Console.WriteLine("テキスト項目を更新しました。");

            if (hadAiContent)
            {
                await DeleteAiContentAsync(item, services, "項目データが変更されたため、既存のAIコンテンツはクリアされました。");
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("項目データに変更はありませんでした。");
        }

        await ShowAiContentMenuAsync(item, services);
        Console.WriteLine("項目の更新が完了しました。");
    }

    private static async Task DeleteItemAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine();
        Console.WriteLine("=== 項目の削除 ===");
        var id = ReadGuid("削除する項目のID: ");
        if (id == Guid.Empty) return;

        var item = await repository.GetByIdAsync(id);
        if (item == null)
        {
            Console.WriteLine($"ID '{id}' の項目が見つかりませんでした。");
            return;
        }

        Console.Write($"本当に '{item.Term}' を削除しますか？ (y/n): ");
        var confirmation = Console.ReadLine();

        if (confirmation?.ToLower() == "y")
        {
            // データベースから削除する前に、関連ファイルをクリーンアップ。
            // DeleteAiContentAsync は特殊になっていて、メッセージを出力しないことも可能。
            // ここのように、もっと大きなまとまりが消されたなら、AI コンテンツが消されたと出力する必要はない。
            await DeleteAiContentAsync(item, services, completionMessage: null);
            await repository.DeleteAsync(id);
            Console.WriteLine("項目が削除されました。");
        }
        else
        {
            Console.WriteLine("削除はキャンセルされました。");
        }
    }

    private static async Task ShowAiContentMenuAsync(Entry item, IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== AIコンテンツ管理メニュー ===");
            PrintItemDetails(item);

            var options = new Dictionary<string, (AiContentAction Action, string DisplayText)>();
            var optionIndex = 1;

            if (item.Status == EntryStatus.PendingAiGeneration)
            {
                options.Add((optionIndex++).ToString(), (AiContentAction.Generate, "AIコンテンツを生成する"));
            }
            else
            {
                options.Add((optionIndex++).ToString(), (AiContentAction.Regenerate, "AIコンテンツを再生成する"));
                if (item.Status == EntryStatus.PendingApproval)
                {
                    options.Add((optionIndex++).ToString(), (AiContentAction.Approve, "AIコンテンツを承認する"));
                }
                options.Add((optionIndex++).ToString(), (AiContentAction.Delete, "AIコンテンツを削除する"));
            }
            options.Add(optionIndex.ToString(), (AiContentAction.Exit, "メインメニューに戻る"));

            foreach (var opt in options)
            {
                Console.WriteLine($"{opt.Key}. {opt.Value.DisplayText}");
            }
            Console.Write("選択してください: ");
            var choice = Console.ReadLine();

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
                // ちょくちょくありそうなので日本語でも出力することを考えたが、うるささが勝るのでやめておく。
                // Console.WriteLine("エラーが発生しました。もう一度お試しください。");
            }
        }
    }

    private static async Task GenerateOrUpdateAiContentAsync(Entry item, IServiceProvider services, AiContentAction action)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var logger = services.GetRequiredService<ILogger<Program>>();

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
        if (!string.IsNullOrEmpty(item.RelativeImagePath))
        {
            try
            {
                var imagePath = AppPath.GetAbsolutePath(item.RelativeImagePath);
                if (File.Exists(imagePath))
                {
                    File.Delete(imagePath);
                }
            }
            catch (Exception ex)
            {
                // ロギングするが、処理は続行。
                // コンソールにも出力される。
                logger.LogError(ex, $"Failed to delete image file: {item.RelativeImagePath}");
            }
        }

        // エントリのビジネスロジックを呼び出してフィールドをクリア
        item.ClearAiContent();

        await repository.UpdateAsync(item);

        if (!string.IsNullOrEmpty(completionMessage))
        {
            Console.WriteLine(completionMessage);
        }
    }

    #region ヘルパーメソッド

    private static string? ReadString(string prompt, string? defaultValue = null)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    private static Guid ReadGuid(string prompt)
    {
        while (true)
        {
            var input = ReadString(prompt);
            if (string.IsNullOrWhiteSpace(input)) return Guid.Empty;
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
        Console.WriteLine($"用語: {item.Term}");
        Console.WriteLine($"AI用コンテキスト: {item.ContextForAi ?? "なし"}");
        Console.WriteLine($"ユーザーメモ: {item.UserNote ?? "なし"}");
        Console.WriteLine($"ステータス: {item.Status}");

        Console.WriteLine();
        Console.WriteLine("=== タイムスタンプ ===");
        Console.WriteLine($"作成日時: {DateTimeUtils.FormatForDisplay(item.CreatedAtUtc)}");
        Console.WriteLine($"コンテンツ生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ContentGeneratedAtUtc)}");
        Console.WriteLine($"画像生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ImageGeneratedAtUtc)}");
        Console.WriteLine($"承認日時: {DateTimeUtils.FormatNullableForDisplay(item.ApprovedAtUtc)}");

        Console.WriteLine();
        Console.WriteLine("=== 説明 ===");
        if (item.Explanations.Any())
        {
            foreach (var (level, text) in item.Explanations)
            {
                Console.WriteLine($"  - {level}: {text}");
            }
        }
        else
        {
            Console.WriteLine("  (なし)");
        }

        Console.WriteLine();
        Console.WriteLine("=== 画像 ===");
        Console.WriteLine($"画像ファイルパス: {item.RelativeImagePath ?? "なし"}");
        Console.WriteLine($"画像プロンプト: {item.ImagePrompt ?? "なし"}");
    }

    #endregion
}