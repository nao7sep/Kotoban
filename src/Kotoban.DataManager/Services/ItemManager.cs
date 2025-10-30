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
    ///
    /// このクラスは、元々 Program.cs に実装されていたメソッド群を、責務に基づいて分割・整理したものです。
    /// そのため、一部の設計は典型的なクラス設計とは異なる場合がありますが、
    /// コンソールアプリケーションのUIロジックと機能フローを管理するという目的を達成するために、
    /// このような静的クラスの構成が採用されています。
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
                Console.WriteLine("Found existing item(s) with the same reading.");
                foreach (var dup in duplicates)
                {
                    Console.WriteLine($"ID: {dup.Id} | Term: {ConsoleUserInterface.GetDisplayText(dup)} | Status: {dup.Status}");
                }

                var proceed = ConsoleUserInterface.ReadString("Do you want to continue anyway? (y/n): ", "n");
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
            Console.WriteLine("=== Add Item ===");

            var newItem = new Entry();

            while (true)
            {
                var reading = ConsoleUserInterface.ReadString("Reading (enter 'c' to cancel): ");
                if (string.Equals(reading, "c", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Item creation cancelled.");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(reading))
                {
                    newItem.Reading = reading.Trim();
                    break;
                }
                Console.WriteLine("Reading is required.");
            }

            if (!await CheckForDuplicatesAsync(services, newItem.Reading))
            {
                Console.WriteLine("Item creation cancelled.");
                return;
            }

            newItem.Expression = ConsoleUserInterface.ReadString("Expression (optional): ");
            newItem.GeneralContext = ConsoleUserInterface.ReadString("General Context (optional): ");
            newItem.UserNote = ConsoleUserInterface.ReadString("User Note (optional): ");
            newItem.Status = EntryStatus.PendingAiGeneration;
            newItem.CreatedAtUtc = DateTime.UtcNow;

            await repository.AddAsync(newItem);
            Console.WriteLine($"Item '{ConsoleUserInterface.GetDisplayText(newItem)}' was added.");
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
            Console.WriteLine("=== Item List ===");
            Console.WriteLine("Which item status would you like to display?");
            Console.WriteLine("1. All");
            Console.WriteLine($"2. Pending AI Generation ({EntryStatus.PendingAiGeneration})");
            Console.WriteLine($"3. Pending Approval ({EntryStatus.PendingApproval})");
            Console.WriteLine($"4. Approved ({EntryStatus.Approved})");
            var choice = ConsoleUserInterface.ReadString("Enter choice [1]: ", "1");

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
                Console.WriteLine("There are no items to display.");
                return;
            }

            // リスト表示の可読性向上のため、表示前に空行を挿入します。
            Console.WriteLine();

            foreach (var item in entryList)
            {
                Console.WriteLine($"ID: {item.Id} | Term: {ConsoleUserInterface.GetDisplayText(item)} | Status: {item.Status}");
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
                Console.WriteLine("Operation cancelled.");
                return null;
            }

            if (Guid.TryParse(input, out var id))
            {
                var item = await repository.GetByIdAsync(id);
                if (item == null)
                {
                    Console.WriteLine($"Item with ID '{id}' not found.");
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
                    Console.WriteLine($"No items found with reading '{input}'.");
                    return null;
                }
                else if (matchingItems.Count == 1)
                {
                    return matchingItems[0];
                }
                else
                {
                    Console.WriteLine($"Multiple items found with reading '{input}'.");
                    for (int i = 0; i < matchingItems.Count; i++)
                    {
                        Console.WriteLine($"{(i + 1)}. {ConsoleUserInterface.GetDisplayText(matchingItems[i])} (ID: {matchingItems[i].Id})");
                    }
                    var choiceStr = ConsoleUserInterface.ReadString("Select an item: ");
                    if (int.TryParse(choiceStr, out var choiceInt) && choiceInt > 0 && choiceInt <= matchingItems.Count)
                    {
                        return matchingItems[choiceInt - 1];
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection.");
                        Console.WriteLine("Operation cancelled.");
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
            Console.WriteLine("=== View/Update Item ===");

            var itemToUpdate = await SelectItemAsync(services, "Enter ID or reading of item to view/update: ");
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

            Console.WriteLine("Enter new values (press Enter to keep current value):");

            var newReading = ConsoleUserInterface.ReadString($"Reading [{item.Reading}]: ", item.Reading);

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
                    Console.WriteLine("Item update cancelled.");
                    return;
                }
            }

            var newExpression = ConsoleUserInterface.ReadString($"Expression [{item.Expression ?? "none"}]: ", item.Expression);
            var newGeneralContext = ConsoleUserInterface.ReadString($"General Context [{item.GeneralContext ?? "none"}]: ", item.GeneralContext);
            var newUserNote = ConsoleUserInterface.ReadString($"User Note [{item.UserNote ?? "none"}]: ", item.UserNote);

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
                Console.WriteLine("Item updated.");

                if (hadAiContent)
                {
                    await AiContentManager.DeleteAiContentAsync(item, services, "Item data was changed, so existing AI content has been cleared.");
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
            Console.WriteLine("=== Delete Item ===");

            var itemToDelete = await SelectItemAsync(services, "Enter ID or reading of item to delete: ");
            if (itemToDelete == null)
            {
                // SelectItemAsync でエラーメッセージが表示されるため、ここでは追加処理不要です。
                return;
            }

            Console.Write($"Really delete '{ConsoleUserInterface.GetDisplayText(itemToDelete)}'? (y/n): ");
            var confirmation = Console.ReadLine();

            if (confirmation?.ToLower() == "y")
            {
                // データベース削除前に関連ファイルをクリーンアップします。
                // 項目全体の削除時は AI コンテンツ削除の個別メッセージは不要なため、
                // completionMessage を null に設定します。
                await AiContentManager.DeleteAiContentAsync(itemToDelete, services, completionMessage: null);
                await repository.DeleteAsync(itemToDelete.Id);
                Console.WriteLine("Item deleted.");
            }
            else
            {
                Console.WriteLine("Deletion cancelled.");
            }
        }
    }
}
