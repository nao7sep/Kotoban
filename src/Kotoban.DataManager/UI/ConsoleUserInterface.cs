using System;
using System.Linq;
using Kotoban.Core.Models;
using Kotoban.Core.Utils;

namespace Kotoban.DataManager.UI
{
    /// <summary>
    /// コンソールでのユーザーインターフェース操作を管理します。
    ///
    /// このクラスは、元々 Program.cs に実装されていたメソッド群を、責務に基づいて分割・整理したものです。
    /// そのため、一部の設計は典型的なクラス設計とは異なる場合がありますが、
    /// コンソールアプリケーションのUIロジックと機能フローを管理するという目的を達成するために、
    /// このような静的クラスの構成が採用されています。
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
        /// ユーザー入力を読み取ります。トリミング処理は行わず、呼び出し側で適切に処理してください。
        /// これにより呼び出し側での柔軟な処理が可能になります。
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
            Console.WriteLine("=== Item Details ===");
            Console.WriteLine($"ID: {item.Id}");
            Console.WriteLine($"Term: {GetDisplayText(item)}");
            Console.WriteLine($"General Context: {item.GeneralContext ?? "none"}");
            Console.WriteLine($"Context for Explanation Gen: {item.ExplanationContext ?? "none"}");
            Console.WriteLine($"Context for Image Gen: {item.ImageContext ?? "none"}");
            Console.WriteLine($"User Note: {item.UserNote ?? "none"}");
            Console.WriteLine($"Status: {item.Status}");

            if (showTimestamps)
            {
                Console.WriteLine();
                Console.WriteLine("=== Timestamps ===");
                Console.WriteLine($"Created: {DateTimeUtils.FormatForDisplay(item.CreatedAtUtc)}");
                Console.WriteLine($"Explanation Generated: {DateTimeUtils.FormatNullableForDisplay(item.ExplanationGeneratedAtUtc)}");
                Console.WriteLine($"Image Generated: {DateTimeUtils.FormatNullableForDisplay(item.ImageGeneratedAtUtc)}");
                Console.WriteLine($"Approved: {DateTimeUtils.FormatNullableForDisplay(item.ApprovedAtUtc)}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Explanations ===");
            if (item.Explanations.Any())
            {
                foreach (var kvp in item.Explanations)
                {
                    PrintExplanation(kvp.Key, kvp.Value);
                }
            }
            else
            {
                Console.WriteLine("None");
            }

            Console.WriteLine();
            Console.WriteLine("=== Image ===");
            Console.WriteLine($"Image File Name: {item.ImageFileName ?? "none"}");
            Console.WriteLine($"Image Prompt: {item.ImagePrompt ?? "none"}");
        }
    }
}
