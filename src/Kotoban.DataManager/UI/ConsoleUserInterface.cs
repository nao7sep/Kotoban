using System;
using System.Linq;
using Kotoban.Core.Models;
using Kotoban.Core.Utils;

namespace Kotoban.DataManager.UI
{
    /// <summary>
    /// コンソールでのユーザーインターフェース操作を管理します。
    /// </summary>
    internal static class ConsoleUserInterface
    {
        /// <summary>
        /// エントリーの表示用テキストを取得します。
        /// </summary>
        public static string GetDisplayText(Entry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.Expression))
            {
                return $"{entry.Reading} ({entry.Expression})";
            }
            return entry.Reading;
        }

        /// <summary>
        /// トリミングなしで返すので、読み取り側で適宜。
        /// 読み取るメソッドがトリミングも行うと呼び出し側の選択肢が減る。
        /// </summary>
        public static string? ReadString(string prompt, string? defaultValue = null)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }

        /// <summary>
        /// 説明レベルに応じた色付きで説明を表示します。
        /// </summary>
        public static void PrintExplanation(ExplanationLevel level, string explanation)
        {
            Console.WriteLine();

            switch (level)
            {
                case ExplanationLevel.Easy:
                    Console.BackgroundColor = ConsoleColor.Blue;
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case ExplanationLevel.Moderate:
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    Console.ForegroundColor = ConsoleColor.Black;
                    break;
                case ExplanationLevel.Advanced:
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.Write($"[{level}]");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine(explanation);
        }

        /// <summary>
        /// エントリーの詳細情報を表示します。
        /// </summary>
        public static void PrintItemDetails(Entry item, bool showTimestamps)
        {
            Console.WriteLine();
            Console.WriteLine("=== 項目の詳細 ===");
            Console.WriteLine($"ID: {item.Id}");
            Console.WriteLine($"用語: {GetDisplayText(item)}");
            Console.WriteLine($"一般的なコンテキスト: {item.GeneralContext ?? "なし"}");
            Console.WriteLine($"説明生成用のコンテキスト: {item.ExplanationContext ?? "なし"}");
            Console.WriteLine($"画像生成用のコンテキスト: {item.ImageContext ?? "なし"}");
            Console.WriteLine($"ユーザーメモ: {item.UserNote ?? "なし"}");
            Console.WriteLine($"ステータス: {item.Status}");

            if (showTimestamps)
            {
                Console.WriteLine();
                Console.WriteLine("=== タイムスタンプ ===");
                Console.WriteLine($"作成日時: {DateTimeUtils.FormatForDisplay(item.CreatedAtUtc)}");
                Console.WriteLine($"説明生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ExplanationGeneratedAtUtc)}");
                Console.WriteLine($"画像生成日時: {DateTimeUtils.FormatNullableForDisplay(item.ImageGeneratedAtUtc)}");
                Console.WriteLine($"承認日時: {DateTimeUtils.FormatNullableForDisplay(item.ApprovedAtUtc)}");
            }

            Console.WriteLine();
            Console.WriteLine("=== 説明 ===");
            if (item.Explanations.Any())
            {
                foreach (var kvp in item.Explanations)
                {
                    PrintExplanation(kvp.Key, kvp.Value);
                }
            }
            else
            {
                Console.WriteLine("なし");
            }

            Console.WriteLine();
            Console.WriteLine("=== 画像 ===");
            Console.WriteLine($"画像ファイル名: {item.ImageFileName ?? "なし"}");
            Console.WriteLine($"画像プロンプト: {item.ImagePrompt ?? "なし"}");
        }
    }
}
