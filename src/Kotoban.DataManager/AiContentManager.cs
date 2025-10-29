using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Services;
using Kotoban.Core.Services.OpenAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kotoban.DataManager
{
    /// <summary>
    /// AIコンテンツの生成と管理を行います。
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
                ConsoleUserInterface.PrintItemDetails(currentItem, showTimestamps: false);

                Console.WriteLine();
                Console.WriteLine("=== 仕上げメニュー ===");
                Console.WriteLine("1. 項目データを更新する");
                Console.WriteLine("2. AIコンテンツを管理する");
                Console.WriteLine("3. 次の項目へ");
                Console.WriteLine("4. 仕上げプロセスを終了する");
                var choice = ConsoleUserInterface.ReadString("選択してください: ");

                switch (choice)
                {
                    case "1": // 項目データを更新する
                        // いきなり「新しい値を～」より、空行に続けて、どのモードに入ったのか明示した方が分かりやすい。
                        Console.WriteLine();
                        Console.WriteLine("=== 項目の更新 ===");
                        await ItemManager.UpdateItemCoreAsync(currentItem, services, showAiMenu: false);
                        break;
                    case "2": // AIコンテンツを管理する
                        await MenuSystem.ShowAiContentMenuAsync(currentItem, services, printItemDetails: false);
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

        /// <summary>
        /// 説明の一括生成を行います。
        /// </summary>
        public static async Task GenerateAllExplanationsAsync(IServiceProvider services)
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
                Console.WriteLine("説明が必要な項目がありません。");
                Console.WriteLine("すべての項目に説明が生成済みです。");
                return;
            }

            Console.WriteLine($"説明が未生成の項目が{itemsWithoutExplanations.Count}件見つかりました。");
            Console.WriteLine("ESCキーを押すと処理を中断できます。");

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
                    Console.Write($"処理中: {ConsoleUserInterface.GetDisplayText(item)} ({processedCount + 1}/{totalCount})...");

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

            Console.WriteLine($"説明の一括生成が完了しました。");
            Console.WriteLine($"結果: {processedCount}/{totalCount} 件完了");
        }

        /// <summary>
        /// AIコンテンツを生成または更新します。
        /// </summary>
        public static async Task GenerateOrUpdateAiContentAsync(Entry item, IServiceProvider services, AiContentAction action)
        {
            var repository = services.GetRequiredService<IEntryRepository>();

            Console.WriteLine();
            Console.WriteLine($"=== AIコンテンツの{(action == AiContentAction.Generate ? "生成" : "再生成")} ===");

            Console.WriteLine("1. 説明のみ生成する");
            Console.WriteLine("2. 画像のみ生成する");
            Console.WriteLine("3. 説明と画像の両方を生成する");
            var choice = ConsoleUserInterface.ReadString("選択してください: ");

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
                    Console.WriteLine("無効な選択です。");
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
                var newExplanationContext = ConsoleUserInterface.ReadString($"新しい説明生成用のコンテキスト (変更しない場合はEnter): ", previousExplanationContext);
                previousExplanationContext = newExplanationContext;

                try
                {
                    // 例外が飛ばなかったなら要素数は3になるのが保証されている。
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
                    var choice = ConsoleUserInterface.ReadString("選択してください: ");

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

                var newImageContext = ConsoleUserInterface.ReadString($"新しい画像生成用のコンテキスト (変更しない場合はEnter): ", previousImageContext);
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
                    var choice = ConsoleUserInterface.ReadString("選択してください: ");

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

        /// <summary>
        /// AIコンテンツを承認します。
        /// </summary>
        public static async Task ApproveAiContentAsync(Entry item, IServiceProvider services)
        {
            var repository = services.GetRequiredService<IEntryRepository>();
            item.Approve();
            await repository.UpdateAsync(item);
            Console.WriteLine("コンテンツが承認されました。");
        }

        /// <summary>
        /// AIコンテンツを削除します。
        /// ほかのメソッドと異なり、完了時のメッセージがオプションになっている。付近のものとかぶってうるさくなるなら、こちらを黙らせる。
        /// </summary>
        public static async Task DeleteAiContentAsync(Entry item, IServiceProvider services, string? completionMessage)
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
    }
}
