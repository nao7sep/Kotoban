using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
    /// <summary>
    /// アプリケーションのエントリポイント。
    /// </summary>
    public static async Task Main(string[] args)
    {
        // Serilogロガーの設定
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/kotoban_log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // DIコンテナの設定
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

    /// <summary>
    /// 依存性注入サービスを設定します。
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.AddSingleton<ILearningItemRepository>(provider =>
        {
            var repoLogger = provider.GetRequiredService<ILogger<JsonLearningItemRepository>>();
            const string dataFileName = "kotoban_data.json";
            return new JsonLearningItemRepository(dataFileName, repoLogger);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// メインの対話型アプリケーションループを実行します。
    /// </summary>
    private static async Task RunApplicationLoop(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILearningItemRepository>();

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
                    return; // ループを終了
                default:
                    Console.WriteLine("無効な選択です。もう一度お試しください。");
                    break;
            }
        }
    }

    /// <summary>
    /// 新しい学習項目を対話的に追加します。
    /// </summary>
    private static async Task AddItemAsync(ILearningItemRepository repository)
    {
        Console.WriteLine("\n--- 新しい項目を追加 ---");
        Console.Write("項目の種類を選択してください (1: Vocabulary, 2: Concept): ");
        var typeChoice = Console.ReadLine();

        LearningItem newItem;

        switch (typeChoice)
        {
            case "1":
                newItem = new VocabularyItem();
                break;
            case "2":
                newItem = new ConceptItem();
                break;
            default:
                Console.WriteLine("無効な種類です。");
                return;
        }

        newItem.Term = ReadString("用語: ");
        newItem.ContextForAi = ReadString("AI用のコンテキスト: ");
        newItem.Status = EntryStatus.PendingAiGeneration;

        if (newItem is VocabularyItem vocabItem)
        {
            Console.WriteLine("使用例を入力してください（入力が終わったら空行でEnter）:");
            while (true)
            {
                var example = ReadString(" > ");
                if (string.IsNullOrWhiteSpace(example))
                {
                    break;
                }
                vocabItem.UsageExamples.Add(example);
            }
        }

        await repository.AddAsync(newItem);
        Console.WriteLine($"項目 '{newItem.Term}' が追加されました。ID: {newItem.Id}");
    }

    /// <summary>
    /// すべての学習項目を表示し、詳細表示のオプションを提供します。
    /// </summary>
    private static async Task ViewAllItemsAsync(ILearningItemRepository repository)
    {
        Console.WriteLine("\n--- すべての項目 ---");
        var items = (await repository.GetAllAsync()).ToList();

        if (!items.Any())
        {
            Console.WriteLine("表示する項目がありません。");
            return;
        }

        // Display summary
        foreach (var item in items)
        {
            var itemType = item is VocabularyItem ? "語彙" : "概念";
            Console.WriteLine($"ID: {item.Id} | タイプ: {itemType} | 用語: {item.Term} | ステータス: {item.Status}");
        }

        // Option to view details
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

    /// <summary>
    /// 既存の学習項目を対話的に更新します。
    /// </summary>
    private static async Task UpdateItemAsync(ILearningItemRepository repository)
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

        item.Term = ReadString($"用語 [{item.Term}]: ", item.Term);
        item.ContextForAi = ReadString($"AI用のコンテキスト [{item.ContextForAi}]: ", item.ContextForAi);
        item.Status = ReadEnum($"ステータス [{item.Status}]: ", item.Status);
        item.ImageUrl = ReadString($"画像URL [{item.ImageUrl ?? "なし"}]: ", item.ImageUrl);

        if (item is VocabularyItem vocabItem)
        {
            Console.WriteLine("現在の使用例:");
            vocabItem.UsageExamples.ForEach(e => Console.WriteLine($" - {e}"));
            Console.Write("使用例を更新しますか？ (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                vocabItem.UsageExamples.Clear();
                Console.WriteLine("新しい使用例を入力してください（入力が終わったら空行でEnter）:");
                while (true)
                {
                    var example = ReadString(" > ");
                    if (string.IsNullOrWhiteSpace(example)) break;
                    vocabItem.UsageExamples.Add(example);
                }
            }
        }
        
        // Update Explanations
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

    /// <summary>
    /// 学習項目を対話的に削除します。
    /// </summary>
    private static async Task DeleteItemAsync(ILearningItemRepository repository)
    {
        Console.WriteLine("\n--- 項目を削除 ---");
        var id = ReadGuid("削除する項目のID: ");
        if (id == Guid.Empty) return;

        var item = await repository.GetByIdAsync(id);
        if (item == null)
        {
            Console.WriteLine("指定されたIDの項目が見つかりません。");
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

    /// <summary>
    /// コンソールから文字列を読み取ります。
    /// </summary>
    private static string ReadString(string prompt, string? defaultValue = null)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue ?? "" : input!;
    }

    /// <summary>
    /// コンソールからGUIDを読み取ります。
    /// </summary>
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
    
    /// <summary>
    /// コンソールからEnum値を読み取ります。
    /// </summary>
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

    /// <summary>
    /// 学習項目の詳細をコンソールに表示します。
    /// </summary>
    private static void PrintItemDetails(LearningItem item)
    {
        Console.WriteLine("\n--- 項目の詳細 ---");
        Console.WriteLine($"ID: {item.Id}");
        Console.WriteLine($"種類: {(item is VocabularyItem ? "語彙" : "概念")}");
        Console.WriteLine($"用語: {item.Term}");
        Console.WriteLine($"AI用コンテキスト: {item.ContextForAi}");
        Console.WriteLine($"ステータス: {item.Status}");
        Console.WriteLine($"画像URL: {item.ImageUrl ?? "なし"}");

        Console.WriteLine("説明:");
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

        if (item is VocabularyItem vocabItem)
        {
            Console.WriteLine("使用例:");
            if (vocabItem.UsageExamples.Any())
            {
                foreach (var example in vocabItem.UsageExamples)
                {
                    Console.WriteLine($"  - {example}");
                }
            }
            else
            {
                Console.WriteLine("  (なし)");
            }
        }
        Console.WriteLine("--------------------");
    }

    #endregion
}
