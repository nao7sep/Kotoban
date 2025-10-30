using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Services;
using Kotoban.DataManager.Models;
using Kotoban.DataManager.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kotoban.DataManager.UI
{
    /// <summary>
    /// メニューシステムとナビゲーションロジックを管理します。
    ///
    /// このクラスは、元々 Program.cs に実装されていたメソッド群を、責務に基づいて分割・整理したものです。
    /// そのため、一部の設計は典型的なクラス設計とは異なる場合がありますが、
    /// コンソールアプリケーションのUIロジックと機能フローを管理するという目的を達成するために、
    /// このような静的クラスの構成が採用されています。
    /// </summary>
    internal static class MenuSystem
    {
        /// <summary>
        /// メインアプリケーションループを実行します。
        /// </summary>
        public static async Task RunApplicationLoopAsync(IServiceProvider services)
        {
            // DI スコープを作成してアプリケーションループ専用のサービスプロバイダを取得します。
            // スコープはアプリケーション全体ではなく、特定のライフサイクル（この場合はメニューループ）に
            // 限定されたサービスインスタンスを提供します。
            //
            // これにより、RunApplicationLoop の複数回実行時でも、アプリケーション全体の
            // サービスに影響を与えることなく、独立したサービススコープで動作できます。

            using var scope = services.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var logger = scopedServices.GetRequiredService<ILogger<Program>>();

            // ILogger<ActionDispatcher> を使用するため、ホスト構築後にアクションを登録します。
            var actionDispatcher = scopedServices.GetRequiredService<ActionDispatcher>();
            var actionLogger = scopedServices.GetRequiredService<ILogger<ActionDispatcher>>();

#pragma warning disable CS1998 // この非同期メソッドには 'await' 演算子がないため、同期的に実行されます
            actionDispatcher.Register("trace", async parameters =>
#pragma warning restore CS1998
            {
                // トレース処理はパラメータの正確性に依存するため、厳密な検証を行います。

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
                Console.WriteLine("=== Main Menu ===");
                Console.WriteLine("1. Add Item");
                Console.WriteLine("2. Finalize Data"); // AI コンテンツの生成と項目の承認を流れ作業で実行
                Console.WriteLine("3. Bulk Generate Explanations");
                Console.WriteLine("4. View Item List");
                Console.WriteLine("5. View/Update Item");
                Console.WriteLine("6. Delete Item");
                Console.WriteLine("7. Exit");
                Console.Write("Enter choice: ");

                var choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await ItemManager.AddItemAsync(scopedServices);
                            break;
                        case "2":
                            await AiContentManager.FinalizeAllItemsAsync(scopedServices);
                            break;
                        case "3":
                            await AiContentManager.GenerateAllExplanationsAsync(scopedServices);
                            break;
                        case "4":
                            await ItemManager.ViewAllItemsAsync(scopedServices);
                            break;
                        case "5":
                            await ItemManager.UpdateItemAsync(scopedServices);
                            break;
                        case "6":
                            await ItemManager.DeleteItemAsync(scopedServices);
                            break;
                        case "7":
                            return;
                        default:
                            Console.WriteLine("Invalid choice.");
                            Console.WriteLine("Please try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred during the operation.");
                }
            }
        }

        /// <summary>
        /// AIコンテンツ管理メニューを表示します。
        /// </summary>
        public static async Task ShowAiContentMenuAsync(Entry item, IServiceProvider services, bool printItemDetails = true)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();

            if (printItemDetails)
            {
                // ユーザビリティ向上のため、項目詳細は最初に一度だけ表示します。
                ConsoleUserInterface.PrintItemDetails(item, showTimestamps: false);
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("=== AI Content Management ===");

                var options = new Dictionary<string, (AiContentAction Action, string DisplayText)>();
                var optionIndex = 1;

                if (item.Status == EntryStatus.PendingAiGeneration)
                {
                    options.Add(optionIndex++.ToString(), (AiContentAction.Generate, "Generate AI Content"));
                }
                else
                {
                    options.Add(optionIndex++.ToString(), (AiContentAction.Regenerate, "Regenerate AI Content"));
                    if (item.Status == EntryStatus.PendingApproval)
                    {
                        options.Add(optionIndex++.ToString(), (AiContentAction.Approve, "Approve AI Content"));
                    }
                    options.Add(optionIndex++.ToString(), (AiContentAction.Delete, "Delete AI Content"));
                }
                options.Add(optionIndex.ToString(), (AiContentAction.Exit, "Return to Main Menu"));

                foreach (var opt in options)
                {
                    Console.WriteLine($"{opt.Key}. {opt.Value.DisplayText}");
                }
                var choice = ConsoleUserInterface.ReadString("Enter choice: ");

                if (choice == null || !options.TryGetValue(choice, out var selectedOption))
                {
                    Console.WriteLine("Invalid choice.");
                    continue;
                }

                try
                {
                    switch (selectedOption.Action)
                    {
                        case AiContentAction.Generate:
                        case AiContentAction.Regenerate:
                            await AiContentManager.GenerateOrUpdateAiContentAsync(item, services, selectedOption.Action);
                            break;
                        case AiContentAction.Approve:
                            await AiContentManager.ApproveAiContentAsync(item, services);
                            return; // 承認完了後はメニューを終了します
                        case AiContentAction.Delete:
                            await AiContentManager.DeleteAiContentAsync(item, services, "AI content deleted.");
                            break;
                        case AiContentAction.Exit:
                            return;
                        default:
                            throw new InvalidOperationException($"Unsupported action: {selectedOption.Action}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred in the AI Content Menu.");
                }
            }
        }
    }
}
