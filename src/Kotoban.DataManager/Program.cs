using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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
        try
        {
            var timestamp = DateTimeUtils.UtcNowTimestamp();
            var logFilePath = AppPath.GetAbsolutePath(Path.Combine("Logs", $"Kotoban-{timestamp}.log"));
            DirectoryUtils.EnsureParentDirectoryExists(logFilePath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
                .WriteTo.File(logFilePath)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing logger: {ex}");

            Console.Write("Enterキーを押して終了します...");
            Console.ReadLine();
            return;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var dataFilePath = configuration["DataFilePath"] ?? "Kotoban-Data.json";
        if (!Path.IsPathFullyQualified(dataFilePath))
        {
            dataFilePath = AppPath.GetAbsolutePath(dataFilePath);
        }

        var maxBackupFiles = configuration.GetValue("MaxBackupFiles", 100);

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(builder => builder.AddSerilog(dispose: true))
            .AddSingleton<IEntryRepository>(provider =>
            {
                return new JsonEntryRepository(
                    dataFilePath,
                    JsonRepositoryBackupMode.CreateCopyInTemp,
                    maxBackupFiles
                );
            })
            .BuildServiceProvider();

        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
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

            // Mac で ReadKey が例外を投げたので、ReadLine に変更した。
            // https://github.com/nao7sep/coding-notes/blob/main/understanding-the-console-readkey-exception-in-a-dotnet-async-finally-block-on-macos.md
            Console.Write("Enterキーを押して終了します...");
            Console.ReadLine();
        }
    }

    private static async Task RunApplicationLoop(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEntryRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

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
                logger.LogError(ex, "An error occurred during the operation.");
            }
        }
    }

    private static async Task AddItemAsync(IEntryRepository repository)
    {
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

        await ShowAiContentMenuAsync(newItem, repository);
    }

    private static async Task ViewAllItemsAsync(IEntryRepository repository)
    {
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

    private static async Task UpdateItemAsync(IEntryRepository repository)
    {
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

            // まずテキストの変更を保存
            await repository.UpdateAsync(item);
            Console.WriteLine("テキスト項目を更新しました。");

            if (hadAiContent)
            {
                await DeleteAiContentAsync(item, repository, "項目データが変更されたため、既存のAIコンテンツはクリアされました。");
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("項目データに変更はありませんでした。");
        }

        await ShowAiContentMenuAsync(item, repository);
        Console.WriteLine("項目の更新が完了しました。");
    }

    private static async Task DeleteItemAsync(IEntryRepository repository)
    {
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
            // データベースから削除する前に、関連ファイルをクリーンアップ
            await DeleteAiContentAsync(item, repository, null);
            await repository.DeleteAsync(id);
            Console.WriteLine("項目が削除されました。");
        }
        else
        {
            Console.WriteLine("削除はキャンセルされました。");
        }
    }

    private static async Task ShowAiContentMenuAsync(Entry item, IEntryRepository repository)
    {
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
                        await GenerateOrUpdateAiContentAsync(item, repository, selectedAction);
                        break;

                    case AiContentAction.Approve:
                        await ApproveAiContentAsync(item, repository);
                        return; // 承認後はメニューを抜ける

                    case AiContentAction.Delete:
                        await DeleteAiContentAsync(item, repository, "AIコンテンツが削除されました。");
                        break;

                    case AiContentAction.Exit:
                        return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred in the AI Content Menu.");
                Console.WriteLine("エラーが発生しました。もう一度お試しください。");
            }
        }
    }

    private static async Task GenerateOrUpdateAiContentAsync(Entry item, IEntryRepository repository, AiContentAction action)
    {
        Console.WriteLine();
        var actionText = action == AiContentAction.Generate ? "生成" : "再生成";
        Console.WriteLine($"AIコンテンツの{actionText}機能は現在実装されていません。");
        await Task.CompletedTask;
    }

    private static async Task ApproveAiContentAsync(Entry item, IEntryRepository repository)
    {
        item.Approve();
        await repository.UpdateAsync(item);
        Console.WriteLine("コンテンツが承認されました。");
    }

    private static async Task DeleteAiContentAsync(Entry item, IEntryRepository repository, string? completionMessage)
    {
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
                // ロギングするが、処理は続行
                Log.Error(ex, $"画像ファイルの削除に失敗しました: {item.RelativeImagePath}");
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

    /// <summary>
    /// ユーザーに 'y' または 'n' の入力を求め、bool値を返します。
    /// </summary>
    /// <param name="prompt">表示するプロンプト</param>
    /// <param name="defaultValue">ユーザーがEnterキーのみを押した場合のデフォルト値</param>
    /// <returns>ユーザーの選択に対応するbool値</returns>
    private static bool ReadBool(string prompt, bool defaultValue)
    {
        var defaultValueString = defaultValue ? "y" : "n";
        while (true)
        {
            var input = ReadString($"{prompt} (y/n) [{defaultValueString}]: ");
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            if (input.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (input.Equals("n", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Console.WriteLine("無効な入力です。'y' または 'n' を入力してください。");
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