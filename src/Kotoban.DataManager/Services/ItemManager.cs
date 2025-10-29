using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.DataManager.UI;
using Microsoft.Extensions.DependencyInjection;

namespace Kotoban.DataManager.Services
{
    /// <summary>
    /// 項目のCRUD操作を管理します。
    /// </summary>
    internal static class ItemManager
    {
        /// <summary>
        /// 重複する読みがなをチェックします。
        /// </summary>
        public static async Task<bool> CheckForDuplicatesAsync(IServiceProvider services, string reading, Guid? excludeId = null)
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
                    Console.WriteLine($"ID: {dup.Id} | 用語: {ConsoleUserInterface.GetDisplayText(dup)} | ステータス: {dup.Status}");
                }

                var proceed = ConsoleUserInterface.ReadString("このまま続行しますか？ (y/n): ", "n");
                if (!string.Equals(proceed, "y", StringComparison.OrdinalIgnoreCase))
                {
                    return false; // キャンセル
                }
            }

            return true; // 続行
        }

        /// <summary>
        /// 新しい項目を追加します。
        /// </summary>
        public static async Task AddItemAsync(IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            Console.WriteLine();
            Console.WriteLine("=== 項目の追加 ===");

            var newItem = new Entry();

            while (true)
            {
                var reading = ConsoleUserInterface.ReadString("読みがな (キャンセルする場合は 'c'): ");
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

            newItem.Expression = ConsoleUserInterface.ReadString("表記 (オプション): ");
            newItem.GeneralContext = ConsoleUserInterface.ReadString("一般的なコンテキスト (オプション): ");
            newItem.UserNote = ConsoleUserInterface.ReadString("ユーザーメモ (オプション): ");
            newItem.Status = EntryStatus.PendingAiGeneration;
            newItem.CreatedAtUtc = DateTime.UtcNow;

            await repository.AddAsync(newItem);
            Console.WriteLine($"項目 '{ConsoleUserInterface.GetDisplayText(newItem)}' が追加されました。");
            Console.WriteLine($"ID: {newItem.Id}");
            await MenuSystem.ShowAiContentMenuAsync(newItem, services);
        }

        /// <summary>
        /// すべての項目を表示します。
        /// </summary>
        public static async Task ViewAllItemsAsync(IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            Console.WriteLine();
            Console.WriteLine("=== 項目一覧 ===");
            Console.WriteLine("どのステータスの項目を表示しますか？");
            Console.WriteLine("1. すべて");
            Console.WriteLine($"2. AI生成待ち ({EntryStatus.PendingAiGeneration})");
            Console.WriteLine($"3. 承認待ち ({EntryStatus.PendingApproval})");
            Console.WriteLine($"4. 承認済み ({EntryStatus.Approved})");
            var choice = ConsoleUserInterface.ReadString("選択してください [1]: ", "1");

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

            // リスト表示の可読性向上のため、表示前に空行を挿入します。
            Console.WriteLine();

            foreach (var item in entryList)
            {
                Console.WriteLine($"ID: {item.Id} | 用語: {ConsoleUserInterface.GetDisplayText(item)} | ステータス: {item.Status}");
            }
        }

        /// <summary>
        /// 項目を選択します。
        /// </summary>
        public static async Task<Entry?> SelectItemAsync(IServiceProvider services, string prompt)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            var input = ConsoleUserInterface.ReadString(prompt);
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
                        Console.WriteLine($"{i + 1}. {ConsoleUserInterface.GetDisplayText(matchingItems[i])} (ID: {matchingItems[i].Id})");
                    }
                    var choiceStr = ConsoleUserInterface.ReadString("項目を選択してください: ");
                    if (int.TryParse(choiceStr, out var choiceInt) && choiceInt > 0 && choiceInt <= matchingItems.Count)
                    {
                        return matchingItems[choiceInt - 1];
                    }
                    else
                    {
                        Console.WriteLine("無効な選択です。");
                        Console.WriteLine("操作をキャンセルしました。");
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// 項目を更新します。
        /// </summary>
        public static async Task UpdateItemAsync(IServiceProvider services)
        {
            Console.WriteLine();
            Console.WriteLine("=== 項目の表示・更新 ===");

            var itemToUpdate = await SelectItemAsync(services, "表示・更新する項目のIDまたは読みがな: ");
            if (itemToUpdate == null)
            {
                // SelectItemAsync でエラーメッセージが表示されるため、ここでは追加処理不要です。
                return;
            }

            await UpdateItemCoreAsync(itemToUpdate, services, showAiMenu: true);
        }

        /// <summary>
        /// 項目の更新処理の中核部分です。
        /// </summary>
        public static async Task UpdateItemCoreAsync(Entry item, IServiceProvider services, bool showAiMenu)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            var originalReading = item.Reading;
            var originalExpression = item.Expression;
            var originalGeneralContext = item.GeneralContext;
            var originalUserNote = item.UserNote;

            Console.WriteLine("新しい値を入力してください（変更しない場合はEnter）:");

            var newReading = ConsoleUserInterface.ReadString($"読みがな [{item.Reading}]: ", item.Reading);

            // 読みがなはソートキーとして使用されるため、前後の空白を除去します。
            // 他のフィールドは空白による影響が軽微なため、過度なトリム処理は避けます。
            // 必須フィールドのため、無効な値の場合は元の値を保持します。
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

            var newExpression = ConsoleUserInterface.ReadString($"表記 [{item.Expression ?? "なし"}]: ", item.Expression);
            var newGeneralContext = ConsoleUserInterface.ReadString($"一般的なコンテキスト [{item.GeneralContext ?? "なし"}]: ", item.GeneralContext);
            var newUserNote = ConsoleUserInterface.ReadString($"ユーザーメモ [{item.UserNote ?? "なし"}]: ", item.UserNote);

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

                // テキスト変更を先に保存します。
                // DeleteAiContentAsync も保存処理を行うため重複しますが、これは意図的な設計です。
                //
                // UpdateAsync は単純な保存処理ではなく、永続化ストレージを意識しない
                // 「項目の更新」操作として定義されています。論理的な正しさを優先し、
                // 低頻度な更新処理での軽微なコスト増加は許容します。

                await repository.UpdateAsync(item);
                Console.WriteLine("項目を更新しました。");

                if (hadAiContent)
                {
                    await AiContentManager.DeleteAiContentAsync(item, services, "項目データが変更されたため、既存のAIコンテンツはクリアされました。");
                }
            }

            if (showAiMenu)
            {
                await MenuSystem.ShowAiContentMenuAsync(item, services);
            }
        }

        /// <summary>
        /// 項目を削除します。
        /// </summary>
        public static async Task DeleteItemAsync(IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            Console.WriteLine();
            Console.WriteLine("=== 項目の削除 ===");

            var itemToDelete = await SelectItemAsync(services, "削除する項目のIDまたは読みがな: ");
            if (itemToDelete == null)
            {
                // SelectItemAsync でエラーメッセージが表示されるため、ここでは追加処理不要です。
                return;
            }

            Console.Write($"本当に '{ConsoleUserInterface.GetDisplayText(itemToDelete)}' を削除しますか？ (y/n): ");
            var confirmation = Console.ReadLine();

            if (confirmation?.ToLower() == "y")
            {
                // データベース削除前に関連ファイルをクリーンアップします。
                // 項目全体の削除時は AI コンテンツ削除の個別メッセージは不要なため、
                // completionMessage を null に設定します。
                await AiContentManager.DeleteAiContentAsync(itemToDelete, services, completionMessage: null);
                await repository.DeleteAsync(itemToDelete.Id);
                Console.WriteLine("項目が削除されました。");
            }
            else
            {
                Console.WriteLine("削除はキャンセルされました。");
            }
        }
    }
}
