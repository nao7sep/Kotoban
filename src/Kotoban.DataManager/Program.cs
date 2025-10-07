using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Services;
using Kotoban.Core.Services.OpenAi;
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
#if DEBUG
        // 型 Program は、.csproj ファイルを特定し、UserSecretsId を探すことに使われる。
        // 同じアセンブリーに含まれる型ならなんでもよいとのこと。
        builder.Configuration.AddUserSecrets<Program>();
#endif
        // =============================================================================

        // Serilogの手動セットアップ（ファイルロギング用）
        var timestamp = DateTimeUtils.UtcNowTimestamp();
        var logFilePath = AppPath.GetAbsolutePath(Path.Combine("Logs", $"Kotoban-{timestamp}.log"));
        DirectoryUtils.EnsureParentDirectoryExists(logFilePath);

        // 蛇足コメント: Serilog についてよく知らず、Log.Logger にインスタンスをあてがっては、それをサービス登録していた。
        // コードの後半では Log.Error などを使っていて、たぶん動作は今と同じだったが、DI の徹底により派生開発耐性をつける今の手法とは違った。

        var serilogLogger = new LoggerConfiguration()
#if DEBUG
            // やりとりした JSON が LogTrace により出力される。
            // Serilog には Verbose が、Microsoft の LogLevel には Trace がある。
            // Trace and Verbose are already treated as synonyms とのこと。
            // https://github.com/serilog/serilog-extensions-logging/issues/57
            .MinimumLevel.Verbose()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
            .WriteTo.File(logFilePath)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger, dispose: true);

        // =============================================================================

        // 単一 UI に単一セットのデータが関連づけられる設計なので、そのデータもサービス登録してしまう。
        // AI がやりだしたときには、「Program.メンバー」または「ほかのクラス.静的メンバー」でよいのではと思った。
        // しかし、Scoped, Singleton, Transient の違いを学び、サービス登録による拡張性を理解した。

        // しばらくしての追記: クラスのインスタンスを生成して Add* するもの、
        // <インターフェース, クラス> と書くもの、<インターフェース>(インスタンス) と書くものが混在するように。
        //
        // まず <I, C> は、型指定が I でも C でも取れるインスタンスを DI により生成する。
        // コンストラクターに必要なサービスがその時点で見つからないと、「解決できない」を旨とする例外が飛ぶ。
        // <Int>(Ins) は、すぐに Ins を使いたいときや、その初期化を自分で行いたいときに適する。
        // (I) だけのものは、インターフェースのあるクラスには適さない。
        //
        // インターフェースのあるクラスに（インターフェースにはない）固有の機能を入れて、
        // クラスの型によりインスタンスを取得してそれらを使うのは、たぶんどこかに改善の余地がある。
        // DataFile などが欲しくて Main メソッドではそれに近いことをしているが、
        // どうせ DataManager では SQL データベースに対応しないだろうから、
        // もうちょっとシンプルにつくってもよかったかもしれない。
        //
        // サービス管理をやってみての教訓は、次の通り。
        // - 設定のセクションごとにドメインモデルをつくり、そこには生データをそのまま入れておく。
        // - 実装を切り替えうるサービスクラスなら、インターフェースをつくり、少なくともロジックは切り替えられるのを保証する。
        // - 基本的にあらゆるサービスクラスを DI ベースでつくり、外部で加工したパラメーターを受け付けない。
        // - それぞれのサービスクラスで加工したデータを、共益性があるなら、すぐには不要でもプロパティーとして公開する。
        //
        // 今回のコードは、入念にリファクタリングしたので、それなりにちゃんとした設計のはず。
        // ただ、永続的ストレージに関するところのみ、「どうせ JSON しか対応しない」との前提で妥協している。
        //
        // いろいろなデータソースに対応するプロジェクトなら、たとえばバックアップ方法はそれぞれ大きく異なるだろうから、
        // appsettings.json などには PersistentStorageSettings などをつくり、その中に Json, Sql などをつくり、
        // IPersistentStorageSettings をつくり、JsonPersistentStorageSettings, SqlPersistentStorageSettings などを実装するのが一つの方法。
        // Json, Sql などの上位にある共通的な項目も入れておきながらも、下位でそれぞれの値を上書きできれば、シンプルな実装で多くのことができそう。

        var kotobanSettings = new KotobanSettings();
        builder.Configuration.GetSection("Kotoban").Bind(kotobanSettings);
        builder.Services.AddSingleton(kotobanSettings);

        var openAiSettings = new OpenAiSettings();
        builder.Configuration.GetSection("OpenAi").Bind(openAiSettings);
        builder.Services.AddSingleton(openAiSettings);

        var repository = new JsonEntryRepository(kotobanSettings);
        await repository.LoadDataAsync();
        builder.Services.AddSingleton<IEntryRepository>(repository);

        builder.Services.AddSingleton<OpenAiNetworkSettings>();

        // OpenAiTransportContext は「トランスポート層の責務」を分離するため個別クラス化しています。
        // 認証やエンドポイントなど「リクエストモデル」や「ネットワーク設定モデル」に収まらない情報をまとめる用途です。
        // このアプリではインスタンスが複数必要になる場面はないため、シングルトンで登録しています（シンプルさ優先）。
        builder.Services.AddSingleton<OpenAiTransportContext>();

        builder.Services.AddSingleton<IPromptFormatProvider, PromptFormatProvider>();

        builder.Services.AddSingleton<OpenAiRequestFactory>();

        builder.Services.AddSingleton<OpenAiApiClient>();

        // AddHttpClient() は IHttpClientFactory をDIコンテナに登録し、HttpClientのライフサイクル管理や拡張機能を有効にします。
        builder.Services.AddHttpClient();

        builder.Services.AddSingleton<WebClient>();

        var imageManager = new ImageManager(kotobanSettings);
        builder.Services.AddSingleton<IImageManager>(imageManager);

        builder.Services.AddSingleton<IAiContentService, OpenAiContentService>();

        // ここで ActionDispatcher のインスタンスをつくり、action を登録し、AddSingleton することも可能だが、
        // ILogger でも serilogLogger でもなく ILogger<ActionDispatcher> を使いたいので、RunApplicationLoop 内で。
        // スコープを合わせたく、scopedServices を使いたいというのもある。
        builder.Services.AddSingleton<ActionDispatcher>();

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
            Console.WriteLine($"Data file: {repository.DataFile}");
            Console.WriteLine($"Backup directory: {repository.BackupDirectory}");
            Console.WriteLine($"Final image directory: {imageManager.FinalImageDirectory}");
            Console.WriteLine($"Temporary image directory: {imageManager.TempImageDirectory}");

            logger.LogInformation("Application starting.");

            // ここで host を丸ごと渡すのはベストプラクティスでないと。
            await RunApplicationLoopAsync(host.Services);

            // 一時画像を掃除するなら、ここが一番の場所。
            // finally で無防備にやると例外が飛んだときに困る。
            // かといって、finally に try/catch を入れると、万が一にも永続的な問題が起こり始めた場合に気づけない。
            // ここでやるもう一つの利点は、RunApplicationLoop が落ちたなら一時画像が残ってくれてデバッグに役立ちうること。
            await imageManager.CleanupTempImagesAsync(entryId: null);
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

    private static async Task RunApplicationLoopAsync(IServiceProvider services)
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

        // Build 後に ILogger<ActionDispatcher> を使いたいので、サービス登録のところでなく、ここで action を登録。
        var actionDispatcher = scopedServices.GetRequiredService<ActionDispatcher>();
        var actionLogger = scopedServices.GetRequiredService<ILogger<ActionDispatcher>>();
#pragma warning disable CS1998 // この非同期メソッドには 'await' 演算子がないため、同期的に実行されます
        actionDispatcher.Register("trace", async parameters =>
#pragma warning restore CS1998
        {
            // トレースは、パラメーターの書き方次第であり、そこにミスがなければランタイムで突然落ちることはない。
            // よって、parameters を厳しめに見ておく。

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters), "Parameters cannot be null.");
            }

            if (parameters.Length < 1)
            {
                throw new ArgumentException("Key must be provided as the first parameter.", nameof(parameters));
            }

            object? keyObj = parameters[0];
            if (keyObj == null)
            {
                throw new ArgumentNullException("Key cannot be null.", nameof(parameters));
            }
            if (keyObj is not string key)
            {
                throw new ArgumentException("Key must be a string.", nameof(parameters));
            }

            if (parameters.Length < 2)
            {
                throw new ArgumentException("Value must be provided as the second parameter.", nameof(parameters));
            }

            object? valueObj = parameters[1];
            if (valueObj == null)
            {
                actionLogger.LogTrace("{Key}: {Value}", key, null);
                return;
            }
            if (valueObj is not string value)
            {
                throw new ArgumentException("Value must be a string or null.", nameof(parameters));
            }

            actionLogger.LogTrace("{Key}: {Value}", key, value);
            return;
        });

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== メインメニュー ===");
            Console.WriteLine("1. 項目を追加する");
            Console.WriteLine("2. データを仕上げる"); // AIコンテンツの生成と項目の承認を流れ作業で。
            Console.WriteLine("3. 説明を一括生成する");
            Console.WriteLine("4. 項目のリストを表示する");
            Console.WriteLine("5. 項目を表示・更新する");
            Console.WriteLine("6. 項目を削除する");
            Console.WriteLine("7. 終了する");
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
                        await GenerateAllExplanationsAsync(scopedServices);
                        break;
                    case "4":
                        await ViewAllItemsAsync(scopedServices);
                        break;
                    case "5":
                        await UpdateItemAsync(scopedServices);
                        break;
                    case "6":
                        await DeleteItemAsync(scopedServices);
                        break;
                    case "7":
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
            var reading = ReadString("読みがな (キャンセルする場合は 'c'): ");
            if (string.Equals(reading, "c", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("項目の追加をキャンセルしました。");
                return;
            }
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
            PrintItemDetails(currentItem, showTimestamps: false);

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

    private static async Task GenerateAllExplanationsAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var aiContentService = services.GetRequiredService<IAiContentService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine();
        Console.WriteLine("=== 説明の一括生成 ===");

        // 説明がない項目を取得
        var allItems = await repository.GetAllAsync();
        var itemsWithoutExplanations = allItems
            .Where(i => !i.Explanations.Any())
            .OrderBy(i => i.CreatedAtUtc)
            .ToList();

        if (!itemsWithoutExplanations.Any())
        {
            Console.WriteLine("説明が必要な項目がありません。すべての項目に説明が生成済みです。");
            return;
        }

        Console.WriteLine($"説明が未生成の項目が{itemsWithoutExplanations.Count}件見つかりました。");
        Console.WriteLine("ESCキーを押すと処理を中断できます。");
        Console.WriteLine();

        var processedCount = 0;
        var totalCount = itemsWithoutExplanations.Count;

        foreach (var item in itemsWithoutExplanations)
        {
            // キャンセルチェック
            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    Console.WriteLine("処理が中断されました。");
                    Console.WriteLine($"結果: {processedCount}/{totalCount} 件完了");
                    return;
                }
            }

            // たまに突発的に API が遅くてタイムアウトになるなどがある。
            // 一度の失敗でループから抜けてしまうと「数時間放置したのに」になるので、ログを書き込んで続行。

            try
            {
                // 現在処理中の項目を表示
                Console.Write($"処理中: {GetDisplayText(item)} ({processedCount + 1}/{totalCount})...");

                // 説明を生成
                // 一括生成では追加のコンテキストを指定せず、とりあえず null で生成してみる。
                var generatedExplanationResult = await aiContentService.GenerateExplanationsAsync(item, newExplanationContext: null);

                // 生成された説明を項目に登録
                item.RegisterGeneratedExplanations(generatedExplanationResult.Context, generatedExplanationResult.Explanations);

                // データベースに保存
                await repository.UpdateAsync(item);

                processedCount++;
                Console.WriteLine(" 完了");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate explanations for entry {EntryId}", item.Id);

                // エラーが発生してもカウントは進める（スキップ扱い）
                processedCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"説明の一括生成が完了しました。");
        Console.WriteLine($"結果: {processedCount}/{totalCount} 件完了");
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
            PrintItemDetails(item, showTimestamps: false);
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
        Console.WriteLine("=== 既存のAIコンテンツ ===");
        if (item.Explanations.Any())
        {
            Console.WriteLine("説明:");
            foreach (var kvp in item.Explanations)
            {
                // ParseExplanations が生成した順に入っているので、今のところソートは不要。
                // 辞書は、厳密には順序が不定のはずだが、実装上は追加順に保持される。
                PrintExplanation(kvp.Key, kvp.Value);
            }
        }
        else
        {
            Console.WriteLine("説明: なし");
        }

        Console.WriteLine($"画像: {(string.IsNullOrWhiteSpace(item.ImageFileName) ? "なし" : item.ImageFileName)}");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== AIコンテンツ生成・更新 ===");

            var menuOptions = new Dictionary<string, string>();
            var menuIndex = 1;

            menuOptions.Add(menuIndex++.ToString(), item.Explanations.Any() ? "説明を再生成する" : "説明を生成する");
            menuOptions.Add(menuIndex++.ToString(), !string.IsNullOrWhiteSpace(item.ImageFileName) ? "画像を再生成する" : "画像を生成する");
            menuOptions.Add(menuIndex++.ToString(), "戻る");

            foreach (var kvp in menuOptions)
            {
                Console.WriteLine($"{kvp.Key}. {kvp.Value}");
            }

            var choice = ReadString("選択してください: ");

            switch (choice)
            {
                case "1":
                    await ManageExplanationsAsync(item, services);
                    break;
                case "2":
                    await ManageImageAsync(item, services);
                    break;
                case "3":
                    return;
                default:
                    Console.WriteLine("無効な選択です。");
                    break;
            }
        }
    }

    private static async Task ManageExplanationsAsync(Entry item, IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var aiContentService = services.GetRequiredService<IAiContentService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // たぶんないが、参照の中身が更新されることも想定し、データをコピー。
        var originalExplanations = new Dictionary<ExplanationLevel, string>(item.Explanations);
        var generatedExplanationResults = new List<GeneratedExplanationResult?>(); // 失敗した回は null になる。
        var previousExplanationContext = item.ExplanationContext;

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"=== 説明の生成 (試行回数: {generatedExplanationResults.Count + 1}) ===");

            // Entry に入っているものでなく、前回試行時のものを使う。
            // 出力が惜しく、プロンプトに問題はなさそうだから再試行したいケースは、
            // 出力がダメだから最初のものに立ち返りたいケースより頻度が高い。
            var newExplanationContext = ReadString($"新しい説明生成用のコンテキスト (変更しない場合はEnter): ", previousExplanationContext);
            previousExplanationContext = newExplanationContext;

            try
            {
                // 例外が飛ばなかったなら要素数は3になるのが保証されている。
                var generatedExplanationResult = await aiContentService.GenerateExplanationsAsync(item, newExplanationContext);
                generatedExplanationResults.Add(generatedExplanationResult);
                foreach (var kvp in generatedExplanationResult.Explanations)
                {
                    PrintExplanation(kvp.Key, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate AI explanations.");
                generatedExplanationResults.Add(null);
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("=== どうしまっか～？ ===");
                if (item.Explanations.Any())
                {
                    Console.WriteLine("0. オリジナルの説明を使用する");
                }
                for (int i = 0; i < generatedExplanationResults.Count; i++)
                {
                    if (generatedExplanationResults[i] != null)
                    {
                        Console.WriteLine($"{i + 1}. {i + 1}回目に生成した説明を使用する");
                    }
                }
                Console.WriteLine("r または Enter: もう一度生成する（リトライ）");
                Console.WriteLine("e: 終了する（キャンセル）");
                var choice = ReadString("選択してください: ");

                if (choice == "0" && originalExplanations.Any())
                {
                    Console.WriteLine("オリジナルの説明を保持します。");
                    return;
                }

                if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= generatedExplanationResults.Count && generatedExplanationResults[idx - 1] != null)
                {
                    // null でないと確認されるが、添え字が計算式だからか、null かもしれないと叱られる。
                    var selected = generatedExplanationResults[idx - 1]!;
                    item.RegisterGeneratedExplanations(selected.Context, selected.Explanations);
                    await repository.UpdateAsync(item);
                    Console.WriteLine($"{idx}回目の説明を保存しました。");
                    return;
                }

                if (string.IsNullOrWhiteSpace(choice) || choice.Equals("r", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (choice.Equals("e", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Console.WriteLine("無効な選択です。");
            }
        }
    }

    private static async Task ManageImageAsync(Entry item, IServiceProvider services)
    {
        var repository = services.GetRequiredService<IEntryRepository>();
        var aiContentService = services.GetRequiredService<IAiContentService>();
        var imageManager = services.GetRequiredService<IImageManager>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        SavedImage? originalImage = null;
        var savedImages = new List<SavedImage?>(); // 失敗した回は null になる。
        var previousImageContext = item.ImageContext;

        try
        {
            originalImage = await imageManager.StartImageEditingAsync(item);
        }
        catch (Exception ex)
        {
            // StartImageEditingAsync はデータの不整合でも投げてくるので、続行のために例外をキャッチ。
            // このメソッドがログだけ吐いて続行というのがデザイン上どうしても気に入らなかった。
            // よって、ライブラリーを厳密に動かせ、アプリ側を「ログして進む」のゆるさに。
            logger.LogError(ex, "Error preparing original image.");
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"=== 画像の生成 (試行回数: {savedImages.Count + 1}) ===");

            var newImageContext = ReadString($"新しい画像生成用のコンテキスト (変更しない場合はEnter): ", previousImageContext);
            previousImageContext = newImageContext;

            try
            {
                var generatedImageResult = await aiContentService.GenerateImageAsync(item, newImageContext);
                var savedImage = await imageManager.SaveGeneratedImageAsync(item, generatedImageResult.ImageBytes, generatedImageResult.Extension, savedImages.Count + 1, generatedImageResult.Context, DateTime.UtcNow, generatedImageResult.ImagePrompt);
                savedImages.Add(savedImage);
                Console.WriteLine($"画像を生成しました: {savedImage.FileName}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate or save AI image.");
                savedImages.Add(null);
            }

            async Task CleanupTempImagesAsync()
            {
                await imageManager.CleanupTempImagesAsync(item.Id);
                Console.WriteLine("一時ファイルをクリーンアップしました。");
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("=== どうしまっか～？ ===");
                if (originalImage != null)
                {
                    Console.WriteLine("0. オリジナルの画像を使用する");
                }
                for (int i = 0; i < savedImages.Count; i++)
                {
                    if (savedImages[i] != null)
                    {
                        Console.WriteLine($"{i + 1}. {i + 1}回目に生成した画像を使用する");
                    }
                }
                Console.WriteLine("r または Enter: もう一度生成する（リトライ）");
                Console.WriteLine("e: 終了する（キャンセル）");
                var choice = ReadString("選択してください: ");

                if (choice == "0" && originalImage != null)
                {
                    Console.WriteLine("オリジナルの画像を保持します。");
                    await CleanupTempImagesAsync();
                    return;
                }

                if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= savedImages.Count && savedImages[idx - 1] != null)
                {
                    // null でないと確認されるが、添え字が計算式だからか、null かもしれないと叱られる。
                    var selected = savedImages[idx - 1]!;
                    var imagePath = await imageManager.FinalizeImageAsync(item, selected);
                    item.RegisterGeneratedImage(selected.ImageContext, imagePath, selected.ImagePrompt);
                    await repository.UpdateAsync(item);
                    Console.WriteLine($"{idx}回目の画像を保存しました。");
                    await CleanupTempImagesAsync();
                    return;
                }

                if (string.IsNullOrWhiteSpace(choice) || choice.Equals("r", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (choice.Equals("e", StringComparison.OrdinalIgnoreCase))
                {
                    await CleanupTempImagesAsync();
                    return;
                }

                Console.WriteLine("無効な選択です。");
            }
        }
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
        // インターフェースでは、FinalImageDirectory をもらえない。
        var imageManager = services.GetRequiredService<IImageManager>() as ImageManager ?? throw new InvalidOperationException("ImageManager is not available.");

        // 画像ファイルの物理削除
        if (!string.IsNullOrWhiteSpace(item.ImageFileName))
        {
            var imagePath = Path.Combine(imageManager.FinalImageDirectory, item.ImageFileName);
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

    private static void PrintExplanation(ExplanationLevel level, string explanation)
    {
        Console.WriteLine();

        switch (level)
        {
            case ExplanationLevel.Easy:
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case ExplanationLevel.Moderate:
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
                break;
            case ExplanationLevel.Advanced:
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                break;
        }

        Console.Write($"[{level}]");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine(explanation);
    }

    private static void PrintItemDetails(Entry item, bool showTimestamps)
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

        if (showTimestamps)
        {
            Console.WriteLine();
            Console.WriteLine("=== タイムスタンプ ===");
            Console.WriteLine($"作成日時: {DateTimeUtils.FormatForDisplay(item.CreatedAtUtc)}");
            Console.WriteLine($"説明生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ExplanationGeneratedAtUtc)}");
            Console.WriteLine($"画像生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ImageGeneratedAtUtc)}");
            Console.WriteLine($"承認日時: {DateTimeUtils.FormatNullableForDisplay(item.ApprovedAtUtc)}");
        }

        Console.WriteLine();
        Console.WriteLine("=== 説明 ===");
        if (item.Explanations.Any())
        {
            foreach (var kvp in item.Explanations)
            {
                PrintExplanation(kvp.Key, kvp.Value);
            }
        }
        else
        {
            Console.WriteLine("なし");
        }

        Console.WriteLine();
        Console.WriteLine("=== 画像 ===");
        Console.WriteLine($"画像ファイル名: {item.ImageFileName ?? "なし"}");
        Console.WriteLine($"画像プロンプト: {item.ImagePrompt ?? "なし"}");
    }

    #endregion
}