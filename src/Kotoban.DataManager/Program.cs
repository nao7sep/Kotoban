using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Kotoban.DataManager;

/// <summary>
/// Kotobanデータ管理用のコンソールアプリケーション。
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var logFilePath = $"Logs/Kotoban-{timestamp}.log";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
                .WriteTo.File(logFilePath)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing logger: {ex}");

            Console.Write("何かキーを押して終了します...");
            Console.ReadKey(intercept: true);
            Console.WriteLine();
            return;
        }

        var services = ConfigureServices();
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Application starting.");
            await RunApplicationLoop(services);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "An unhandled exception occurred during application execution.");
        }
        finally
        {
            logger.LogInformation("Application shutting down.");
            await Log.CloseAndFlushAsync();

            Console.Write("何かキーを押して終了します...");
            Console.ReadKey(intercept: true);
            Console.WriteLine();
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.AddSingleton<IEntryRepository>(provider =>
        {
            return new JsonEntryRepository(
                "Kotoban-Data.json",
                JsonRepositoryBackupMode.CreateCopyInTemp
            );
        });

        return services.BuildServiceProvider();
    }

    private static async Task RunApplicationLoop(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEntryRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== Kotoban データマネージャー ===");
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
                        await AddItemAsync(repository);
                        break;
                    case "2":
                        await ViewAllItemsAsync(repository);
                        break;
                    case "3":
                        await UpdateItemAsync(repository);
                        break;
                    case "4":
                        await DeleteItemAsync(repository);
                        break;
                    case "5":
                        return;
                    default:
                        Console.WriteLine("無効な選択です。");
                        Console.WriteLine("もう一度お試しください。");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during the operation.");
            }
        }
    }

    private static async Task AddItemAsync(IEntryRepository repository)
    {
        Console.WriteLine();
        Console.WriteLine("=== 新しい項目を追加 ===");

        var newItem = new Entry();

        while(true)
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
        Console.WriteLine($"項目 '{newItem.Term}' が追加されました。");
        Console.WriteLine($"ID: {newItem.Id}");
    }

    private static async Task ViewAllItemsAsync(IEntryRepository repository)
    {
        Console.WriteLine();
        Console.WriteLine("=== すべての項目 ===");
        var items = await repository.GetAllAsync();

        var entryList = items.ToList();
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

    private static async Task UpdateItemAsync(IEntryRepository repository)
    {
        Console.WriteLine();
        Console.WriteLine("=== 項目を更新 ===");
        var id = ReadGuid("更新する項目のID: ");
        if (id == Guid.Empty) return;

        var item = await repository.GetByIdAsync(id);
        if (item == null)
        {
            Console.WriteLine("指定されたIDの項目が見つかりません。");
            return;
        }

        Console.WriteLine("新しい値を入力してください（変更しない場合はEnter）:");

        item.Term = ReadString($"用語 [{item.Term}]: ", item.Term) ?? item.Term;
        item.ContextForAi = ReadString($"AI用のコンテキスト [{item.ContextForAi ?? "なし"}]: ", item.ContextForAi);
        item.UserNote = ReadString($"ユーザーメモ [{item.UserNote ?? "なし"}]: ", item.UserNote);

        await repository.UpdateAsync(item);
        Console.WriteLine("項目が更新されました。");
    }

    private static async Task DeleteItemAsync(IEntryRepository repository)
    {
        Console.WriteLine();
        Console.WriteLine("=== 項目を削除 ===");
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
            await repository.DeleteAsync(id);
            Console.WriteLine("項目が削除されました。");
        }
        else
        {
            Console.WriteLine("削除はキャンセルされました。");
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
            Console.WriteLine("無効なGUID形式です。");
            Console.WriteLine("もう一度お試しください。");
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
        Console.WriteLine($"作成日時: {item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"コンテンツ生成日時: {(item.ContentGeneratedAtUtc.HasValue ? item.ContentGeneratedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "なし")}");
        Console.WriteLine($"画像生成日時: {(item.ImageGeneratedAtUtc.HasValue ? item.ImageGeneratedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "なし")}");
        Console.WriteLine($"承認日時: {(item.ApprovedAtUtc.HasValue ? item.ApprovedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "なし")}");

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
