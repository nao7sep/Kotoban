using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Services;
using Kotoban.Core.Services.OpenAi;
using Kotoban.DataManager.Models;
using Kotoban.DataManager.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kotoban.DataManager.Services
{
    /// <summary>
    /// AIコンテンツの生成と管理を行います。
    ///
    /// このクラスは、元々 Program.cs に実装されていたメソッド群を、責務に基づいて分割・整理したものです。
    /// そのため、一部の設計は典型的なクラス設計とは異なる場合がありますが、
    /// コンソールアプリケーションのUIロジックと機能フローを管理するという目的を達成するために、
    /// このような静的クラスの構成が採用されています。
    /// </summary>
    internal static class AiContentManager
    {
        /// <summary>
        /// 未承認項目の仕上げ処理を行います。
        /// </summary>
        public static async Task FinalizeAllItemsAsync(IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            Console.WriteLine();
            Console.WriteLine("=== Finalize Unapproved Items ===");

            var allItems = await repository.GetAllAsync();
            var itemsToFinalize = allItems
                .Where(i => i.Status != EntryStatus.Approved)
                .OrderBy(i => i.CreatedAtUtc)
                .ToList();

            if (!itemsToFinalize.Any())
            {
                Console.WriteLine("All items are already approved.");
                return;
            }

            Console.WriteLine($"Found {itemsToFinalize.Count} unapproved item(s).");

            var currentItemIndex = 0;
            while (currentItemIndex < itemsToFinalize.Count)
            {
                // リポジトリパターンを尊重し、キャッシュされた項目ではなく最新の状態を取得します。
                var currentItem = await repository.GetByIdAsync(itemsToFinalize[currentItemIndex].Id);
                if (currentItem == null || currentItem.Status == EntryStatus.Approved)
                {
                    currentItemIndex++;
                    continue;
                }

                // 仕上げプロセスでは更新後の確認が重要なため、毎回項目詳細を表示します。
                ConsoleUserInterface.PrintItemDetails(currentItem, showTimestamps: false);

                Console.WriteLine();
                Console.WriteLine("=== Finalize Menu ===");
                Console.WriteLine("1. Update Item Data");
                Console.WriteLine("2. Manage AI Content");
                Console.WriteLine("3. Next Item");
                Console.WriteLine("4. Exit Finalization Process");
                var choice = ConsoleUserInterface.ReadString("Enter choice: ");

                switch (choice)
                {
                    case "1": // 項目データを更新する
                        // ユーザビリティ向上のため、モード切り替えを明示的に表示します。
                        Console.WriteLine();
                        Console.WriteLine("=== Update Item ===");
                        await ItemManager.UpdateItemCoreAsync(currentItem, services, showAiMenu: false);
                        break;
                    case "2": // AIコンテンツを管理する
                        await MenuSystem.ShowAiContentMenuAsync(currentItem, services, printItemDetails: false);
                        break;
                    case "3": // 次の項目へ
                        // ユーザーの明示的な選択による移動のため、追加の確認表示は不要です。
                        currentItemIndex++;
                        break;
                    case "4": // 仕上げプロセスを終了する
                        Console.WriteLine("Exiting finalization process.");
                        return;
                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished processing all unapproved items.");
        }

        /// <summary>
        /// 説明の一括生成を行います。
        /// </summary>
        public static async Task GenerateAllExplanationsAsync(IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            var aiContentService = services.GetRequiredService<IAiContentService>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            Console.WriteLine();
            Console.WriteLine("=== Bulk Generate Explanations ===");

            // 説明がない項目を取得
            var allItems = await repository.GetAllAsync();
            var itemsWithoutExplanations = allItems
                .Where(i => !i.Explanations.Any())
                .OrderBy(i => i.CreatedAtUtc)
                .ToList();

            if (!itemsWithoutExplanations.Any())
            {
                Console.WriteLine("No items require explanations.");
                Console.WriteLine("All items already have explanations.");
                return;
            }

            Console.WriteLine($"Found {itemsWithoutExplanations.Count} item(s) without explanations.");
            Console.WriteLine("Press ESC to interrupt.");

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
                        Console.WriteLine("Processing was interrupted.");
                        Console.WriteLine($"Result: {processedCount}/{totalCount} completed.");
                        return;
                    }
                }

                // API の一時的な障害やタイムアウトによる処理中断を防ぐため、
                // 個別の失敗はログに記録して処理を継続します。

                try
                {
                    // 現在処理中の項目を表示
                    Console.Write($"Processing: {ConsoleUserInterface.GetDisplayText(item)} ({processedCount + 1}/{totalCount})...");

                    // 説明を生成
                    // 一括生成では標準設定を使用するため、追加コンテキストは null で実行します。
                    var generatedExplanationResult = await aiContentService.GenerateExplanationsAsync(item, newExplanationContext: null);

                    // 生成された説明を項目に登録
                    item.RegisterGeneratedExplanations(generatedExplanationResult.Context, generatedExplanationResult.Explanations);

                    // データベースに保存
                    await repository.UpdateAsync(item);

                    processedCount++;
                    Console.WriteLine(" Done");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate explanations for entry {EntryId}", item.Id);

                    // エラー発生時もカウントを進めて処理を継続します（スキップ扱い）。
                    processedCount++;
                }
            }

            Console.WriteLine($"Bulk explanation generation complete.");
            Console.WriteLine($"Result: {processedCount}/{totalCount} completed.");
        }

        /// <summary>
        /// AIコンテンツを生成または更新します。
        /// </summary>
        public static async Task GenerateOrUpdateAiContentAsync(Entry item, IServiceProvider services, AiContentAction action)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            Console.WriteLine();
            Console.WriteLine($"=== {(action == AiContentAction.Generate ? "Generate" : "Regenerate")} AI Content ===");

            Console.WriteLine("1. Generate Explanations Only");
            Console.WriteLine("2. Generate Image Only");
            Console.WriteLine("3. Generate Both Explanations and Image");
            var choice = ConsoleUserInterface.ReadString("Enter choice: ");

            switch (choice)
            {
                case "1":
                    await ManageExplanationsAsync(item, services);
                    break;
                case "2":
                    await ManageImageAsync(item, services);
                    break;
                case "3":
                    await ManageExplanationsAsync(item, services);
                    await ManageImageAsync(item, services);
                    break;
                default:
                    Console.WriteLine("Invalid choice.");
                    return;
            }

            // 生成が完了したら、ステータスを更新
            if (item.Explanations.Any() || !string.IsNullOrWhiteSpace(item.ImageFileName))
            {
                item.Status = EntryStatus.PendingApproval;
                await repository.UpdateAsync(item);
            }
        }

        /// <summary>
        /// 説明の管理を行います。
        /// </summary>
        public static async Task ManageExplanationsAsync(Entry item, IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            var aiContentService = services.GetRequiredService<IAiContentService>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            // 参照データの意図しない変更を防ぐため、元の説明データをコピーして保持します。
            var originalExplanations = new Dictionary<ExplanationLevel, string>(item.Explanations);
            var generatedExplanationResults = new List<GeneratedExplanationResult?>(); // 生成失敗時は null を格納
            var previousExplanationContext = item.ExplanationContext;

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Generate Explanations (Attempt: {generatedExplanationResults.Count + 1}) ===");

                // 前回試行時のコンテキストを初期値として使用します。
                // 微調整による再試行の方が、完全なやり直しより頻度が高いためです。
                var newExplanationContext = ConsoleUserInterface.ReadString($"New context for explanation generation (press Enter to leave unchanged): ", previousExplanationContext);
                previousExplanationContext = newExplanationContext;

                try
                {
                    // 正常終了時は3つの説明レベル全てが生成されることが保証されています。
                    var generatedExplanationResult = await aiContentService.GenerateExplanationsAsync(item, newExplanationContext);
                    generatedExplanationResults.Add(generatedExplanationResult);
                    foreach (var kvp in generatedExplanationResult.Explanations)
                    {
                        ConsoleUserInterface.PrintExplanation(kvp.Key, kvp.Value);
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
                    Console.WriteLine("=== Please make a selection ===");
                    if (item.Explanations.Any())
                    {
                        Console.WriteLine("0. Use original explanations");
                    }
                    for (int i = 0; i < generatedExplanationResults.Count; i++)
                    {
                        if (generatedExplanationResults[i] != null)
                        {
                            Console.WriteLine($"{(i + 1)}. Use explanations from attempt {(i + 1)}");
                        }
                    }
                    Console.WriteLine("r or Enter: Regenerate (retry)");
                    Console.WriteLine("e: Exit (cancel)");
                    var choice = ConsoleUserInterface.ReadString("Enter choice: ");

                    if (choice == "0" && originalExplanations.Any())
                    {
                        Console.WriteLine("Keeping original explanations.");
                        return;
                    }

                    if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= generatedExplanationResults.Count && generatedExplanationResults[idx - 1] != null)
                    {
                        // null チェック済みですが、計算式による添え字のため null 許容演算子を使用します。
                        var selected = generatedExplanationResults[idx - 1]!;
                        item.RegisterGeneratedExplanations(selected.Context, selected.Explanations);
                        await repository.UpdateAsync(item);
                        Console.WriteLine($"Saved explanations from attempt {idx}.");
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

                    Console.WriteLine("Invalid choice.");
                }
            }
        }

        /// <summary>
        /// 画像の管理を行います。
        /// </summary>
        public static async Task ManageImageAsync(Entry item, IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            var aiContentService = services.GetRequiredService<IAiContentService>();
            var imageManager = services.GetRequiredService<IImageManager>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            SavedImage? originalImage = null;
            var savedImages = new List<SavedImage?>(); // 生成失敗時は null を格納
            var previousImageContext = item.ImageContext;

            try
            {
                originalImage = await imageManager.StartImageEditingAsync(item);
            }
            catch (Exception ex)
            {
                // StartImageEditingAsync はデータ不整合時に例外を発生させるため、
                // 処理継続のために例外をキャッチしてログに記録します。
                // ライブラリは厳密に動作し、アプリケーション側で柔軟に対応する設計です。
                logger.LogError(ex, "Error preparing original image.");
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Generate Image (Attempt: {savedImages.Count + 1}) ===");

                var newImageContext = ConsoleUserInterface.ReadString($"New context for image generation (press Enter to leave unchanged): ", previousImageContext);
                previousImageContext = newImageContext;

                try
                {
                    var generatedImageResult = await aiContentService.GenerateImageAsync(item, newImageContext);
                    var savedImage = await imageManager.SaveGeneratedImageAsync(item, generatedImageResult.ImageBytes, generatedImageResult.Extension, savedImages.Count + 1, generatedImageResult.Context, DateTime.UtcNow, generatedImageResult.ImagePrompt);
                    savedImages.Add(savedImage);
                    Console.WriteLine($"Generated image: {savedImage.FileName}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate or save AI image.");
                    savedImages.Add(null);
                }

                async Task CleanupTempImagesAsync()
                {
                    await imageManager.CleanupTempImagesAsync(item.Id);
                    Console.WriteLine("Cleaned up temporary files.");
                }

                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== Please make a selection ===");
                    if (originalImage != null)
                    {
                        Console.WriteLine("0. Use original image");
                    }
                    for (int i = 0; i < savedImages.Count; i++)
                    {
                        if (savedImages[i] != null)
                        {
                            Console.WriteLine($"{(i + 1)}. Use image from attempt {(i + 1)}");
                        }
                    }
                    Console.WriteLine("r or Enter: Regenerate (retry)");
                    Console.WriteLine("e: Exit (cancel)");
                    var choice = ConsoleUserInterface.ReadString("Enter choice: ");

                    if (choice == "0" && originalImage != null)
                    {
                        Console.WriteLine("Keeping original image.");
                        await CleanupTempImagesAsync();
                        return;
                    }

                    if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= savedImages.Count && savedImages[idx - 1] != null)
                    {
                        // null チェック済みですが、計算式による添え字のため null 許容演算子を使用します。
                        var selected = savedImages[idx - 1]!;
                        var imagePath = await imageManager.FinalizeImageAsync(item, selected);
                        item.RegisterGeneratedImage(selected.ImageContext, imagePath, selected.ImagePrompt);
                        await repository.UpdateAsync(item);
                        Console.WriteLine($"Saved image from attempt {idx}.");
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

                    Console.WriteLine("Invalid choice.");
                }
            }
        }

        /// <summary>
        /// AIコンテンツを承認します。
        /// </summary>
        public static async Task ApproveAiContentAsync(Entry item, IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            item.Approve();
            await repository.UpdateAsync(item);
            Console.WriteLine("Content approved.");
        }

        /// <summary>
        /// AIコンテンツを削除します。
        /// 他のメソッドと異なり、完了メッセージはオプションです。重複表示を避けるため制御可能にしています。
        /// </summary>
        /// <param name="item">AIコンテンツを削除する対象のエントリ。</param>
        /// <param name="services">必要なサービスを取得するためのサービスプロバイダー。</param>
        /// <param name="completionMessage">削除完了時に表示するメッセージ。nullの場合は何も表示しない。</param>
        public static async Task DeleteAiContentAsync(Entry item, IServiceProvider services, string? completionMessage)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            // インターフェースでは FinalImageDirectory にアクセスできないため、具体的なクラスにキャストします。
            var imageManager = services.GetRequiredService<IImageManager>() as ImageManager ?? throw new InvalidOperationException("ImageManager is not available.");

            // 画像ファイルの物理削除
            if (!string.IsNullOrWhiteSpace(item.ImageFileName))
            {
                var imagePath = Path.Combine(imageManager.FinalImageDirectory, item.ImageFileName);
                if (File.Exists(imagePath))
                {
                    // ファイル削除に失敗した場合、エントリの削除も行わない設計です。
                    // これにより orphan file の発生を防ぎ、再起動等による問題解決を可能にします。
                    //
                    // アプリケーションの例外処理は上位レベル（メインメニュー・AI メニュー）で
                    // 包括的に行われるため、個別のコマンドはシンプルに実装されています。

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
    }
}
