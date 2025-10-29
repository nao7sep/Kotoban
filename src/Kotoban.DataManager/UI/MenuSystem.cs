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
    /// </summary>
    internal static class MenuSystem
    {
        /// <summary>
        /// メインアプリケーションループを実行します。
        /// </summary>
        public static async Task RunApplicationLoopAsync(IServiceProvider services)
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

        /// <summary>
        /// AIコンテンツ管理メニューを表示します。
        /// </summary>
        public static async Task ShowAiContentMenuAsync(Entry item, IServiceProvider services, bool printItemDetails = true)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();

            if (printItemDetails)
            {
                // ループで毎回表示するとうるさいので、最初に一度だけ表示。
                ConsoleUserInterface.PrintItemDetails(item, showTimestamps: false);
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
                var choice = ConsoleUserInterface.ReadString("選択してください: ");

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
                            await AiContentManager.GenerateOrUpdateAiContentAsync(item, services, selectedAction);
                            break;
                        case AiContentAction.Approve:
                            await AiContentManager.ApproveAiContentAsync(item, services);
                            return; // 承認後はメニューを抜ける
                        case AiContentAction.Delete:
                            await AiContentManager.DeleteAiContentAsync(item, services, "AIコンテンツが削除されました。");
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
    }
}
