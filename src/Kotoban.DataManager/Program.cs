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

namespace Kotoban.DataManager;

/// <summary>
/// Kotobanデータ管理用のコンソールアプリケーション。
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/kotoban_log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var services = ConfigureServices();
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("アプリケーションを開始します。");
            await RunApplicationLoop(services);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "アプリケーションの実行中に致命的なエラーが発生しました。");
        }
        finally
        {
            logger.LogInformation("アプリケーションを終了します。");
            await Log.CloseAndFlushAsync();
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.AddSingleton<IEntryRepository>(provider => 
        {
            return new JsonEntryRepository(
                "kotoban_data.json", 
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
            Console.WriteLine("\n--- Kotoban データマネージャー ---");
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
                        Console.WriteLine("無効な選択です。もう一度お試しください。");
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "操作中にエラーが発生しました。");
            }
        }
    }

    private static async Task AddItemAsync(IEntryRepository repository)
    {
        Console.WriteLine("\n--- 新しい項目を追加 ---");
        
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
        Console.WriteLine($"項目 '{newItem.Term}' が追加されました。ID: {newItem.Id}");
    }

    private static async Task ViewAllItemsAsync(IEntryRepository repository)
    {
        Console.WriteLine("\n--- すべての項目 ---");
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

        Console.Write("\n詳細を表示したい項目のIDを入力してください（スキップはEnter）: ");
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
        Console.WriteLine("\n--- 項目を更新 ---");
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
        item.ImagePrompt = ReadString($"画像プロンプト [{item.ImagePrompt ?? "なし"}]: ", item.ImagePrompt);
        item.ImageUrl = ReadString($"画像URL [{item.ImageUrl ?? "なし"}]: ", item.ImageUrl);
        item.Status = ReadEnum($"ステータス [{item.Status}]: ", item.Status);

        item.ContentGeneratedAtUtc = ReadDateTime($"コンテンツ生成日時 [{item.ContentGeneratedAtUtc}]: ", item.ContentGeneratedAtUtc);
        item.ImageGeneratedAtUtc = ReadDateTime($"画像生成日時 [{item.ImageGeneratedAtUtc}]: ", item.ImageGeneratedAtUtc);
        item.ApprovedAtUtc = ReadDateTime($"承認日時 [{item.ApprovedAtUtc}]: ", item.ApprovedAtUtc);
        
        Console.WriteLine("説明を更新しますか？ (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            item.Explanations.Clear();
            foreach (ExplanationLevel level in Enum.GetValues(typeof(ExplanationLevel)))
            {
                var explanation = ReadString($"{level}レベルの説明: ");
                if (!string.IsNullOrWhiteSpace(explanation))
                {
                    item.Explanations[level] = explanation;
                }
            }
        }

        await repository.UpdateAsync(item);
        Console.WriteLine("項目が更新されました。");
    }

    private static async Task DeleteItemAsync(IEntryRepository repository)
    {
        Console.WriteLine("\n--- 項目を削除 ---");
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

    #region Helper Methods

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
    
    private static T ReadEnum<T>(string prompt, T defaultValue) where T : struct, Enum
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }
        if (Enum.TryParse<T>(input, true, out var result) && Enum.IsDefined(typeof(T), result))
        {
            return result;
        }
        Console.WriteLine($"無効な値です。有効な値: {string.Join(", ", Enum.GetNames<T>())}");
        return defaultValue;
    }

    private static DateTime? ReadDateTime(string prompt, DateTime? defaultValue)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }
        if (input.ToLower() == "null" || input.ToLower() == "none")
        {
            return null;
        }
        if (DateTime.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out var result))
        {
            return result.ToUniversalTime();
        }
        Console.WriteLine("無効な日時形式です。もう一度お試しください。");
        return defaultValue;
    }

    private static void PrintItemDetails(Entry item)
    {
        Console.WriteLine("\n--- 項目の詳細 ---");
        Console.WriteLine($"ID: {item.Id}");
        Console.WriteLine($"用語: {item.Term}");
        Console.WriteLine($"ユーザーメモ: {item.UserNote ?? "なし"}");
        Console.WriteLine($"AI用コンテキスト: {item.ContextForAi ?? "なし"}");
        Console.WriteLine($"ステータス: {item.Status}");
        
        Console.WriteLine("\n--- 画像 ---");
        Console.WriteLine($"画像URL: {item.ImageUrl ?? "なし"}");
        Console.WriteLine($"画像プロンプト: {item.ImagePrompt ?? "なし"}");

        Console.WriteLine("\n--- 説明 ---");
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
        
        Console.WriteLine("\n--- タイムスタンプ ---");
        Console.WriteLine($"作成日時: {item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"コンテンツ生成日時: {(item.ContentGeneratedAtUtc.HasValue ? item.ContentGeneratedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "なし")}");
        Console.WriteLine($"画像生成日時: {(item.ImageGeneratedAtUtc.HasValue ? item.ImageGeneratedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "なし")}");
        Console.WriteLine($"承認日時: {(item.ApprovedAtUtc.HasValue ? item.ApprovedAtUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "なし")}");

        Console.WriteLine("-------------------- ");
    }

    #endregion
}
