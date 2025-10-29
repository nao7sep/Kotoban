using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Services;
using Kotoban.Core.Services.OpenAi;
using Kotoban.Core.Utils;
using Kotoban.DataManager.Hosting;
using Kotoban.DataManager.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Kotoban.DataManager
{
    /// <summary>
    /// Kotobanデータ管理用のコンソールアプリケーション。
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = await ApplicationHost.CreateHostAsync(args);

            // ILogger<Program> と Serilog の生ロガーインスタンスの使い分けについて：
            //
            // 【ILogger<Program> の特徴】
            // - Microsoft.Extensions.Logging の抽象ロガーで、DI 経由で取得します
            // - .NET 標準のロギング API として、アプリケーション全体で統一的に利用することが推奨されます
            // - <Program> により「Kotoban.DataManager.Program」がカテゴリ名として自動付与され、
            //   ログのフィルタリングや出力フォーマットでカテゴリごとの制御が可能になります
            //
            // 【Serilog との連携】
            // - AddSerilog でロギングプロバイダーとして登録されているため、ILogger<T> の出力も
            //   最終的には Serilog によって処理され、Serilog の設定が適用されます
            //
            // 【ベストプラクティス】
            // - アプリケーションコードでは Serilog の生ロガーを直接使用せず、ILogger<T> を使用します
            // - これにより .NET 標準 API の恩恵（DI、カテゴリ分け、テスト容易性）と
            //   Serilog の高機能な出力機能を両立できます

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                // アセンブリ情報からアプリケーション名とバージョンを取得します。
                // 現在は他の箇所で使用されていないため、ここで直接取得しています。

                var assembly = Assembly.GetExecutingAssembly();
                var assemblyTitle = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
                if (string.IsNullOrWhiteSpace(assemblyTitle))
                {
                    throw new InvalidOperationException("Assembly title is not defined.");
                }

                var version = assembly.GetName().Version;
                if (version == null)
                {
                    throw new InvalidOperationException("Assembly version is not defined.");
                }
                var versionString = version.Build == 0 ? $"{version.Major}.{version.Minor}" : version.ToString(3);

                // サービスからリポジトリとイメージマネージャーを取得
                var repository = host.Services.GetRequiredService<IEntryRepository>() as JsonEntryRepository ?? throw new InvalidOperationException("JsonEntryRepository is not available.");
                var imageManager = host.Services.GetRequiredService<IImageManager>() as ImageManager ?? throw new InvalidOperationException("ImageManager is not available.");

                Console.WriteLine($"{assemblyTitle} v{versionString}");
                Console.WriteLine($"Data file: {repository.DataFile}");
                Console.WriteLine($"Backup directory: {repository.BackupDirectory}");
                Console.WriteLine($"Final image directory: {imageManager.FinalImageDirectory}");
                Console.WriteLine($"Temporary image directory: {imageManager.TempImageDirectory}");

                logger.LogInformation("Application starting.");

                // IServiceProvider を渡すことで、必要なサービスのみアクセス可能にします。
                await MenuSystem.RunApplicationLoopAsync(host.Services);

                // 一時画像のクリーンアップを正常終了時に実行します。
                // finally ブロックでの実行は例外発生時の処理が複雑になるため、ここで実行します。
                // また、アプリケーションループで例外が発生した場合、一時画像が保持されることで
                // デバッグ時の調査に役立ちます。
                await imageManager.CleanupTempImagesAsync(entryId: null);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "An unhandled exception occurred during application execution.");
            }
            finally
            {
                logger.LogInformation("Application shutting down.");

                // Serilog ロガーは AddSerilog(dispose: true) で登録されているため、
                // ホストの Dispose 時に自動的にフラッシュ・クローズされます。
                // そのため、ここで CloseAndFlushAsync を明示的に呼ぶ必要はありません。

                // 【静的ロガーとサービス登録の比較】
                // 静的プロパティを使用する場合：
                // - 初期化タイミングが Lazy に依存し、dispose タイミングが不明確
                // - プロセス終了時の自動クリーンアップに依存する設計になりがち
                //
                // サービス登録を使用する場合：
                // - DI コンテナがライフサイクルを管理し、適切なタイミングでリソース解放
                // - より制御された、予測可能なリソース管理が可能

                // await logger.CloseAndFlushAsync();

                // macOS 環境での Console.ReadKey() 問題により Console.ReadLine() を使用します。
                //
                // 【問題の詳細】
                // async Main メソッドの finally ブロック内で Console.ReadKey() を実行すると、
                // Windows では正常動作しますが、macOS では InvalidOperationException が発生します。
                //
                // 【原因分析】
                // Console.ReadKey() は raw モードで物理キーボードからの直接信号を要求しますが、
                // macOS の finally ブロック実行時に IsInputRedirected が true となり、
                // セキュリティ上の理由で物理キーボード以外の入力が拒否されます。
                //
                // 【解決策】
                // Console.ReadLine() は cooked/canonical mode（行入力モード）で動作し、
                // アプリケーション終了処理中でも安定して動作します。
                // 低レベルな入力処理が不要な場面では ReadLine() の使用を推奨します。
                Console.Write("Enterキーを押して終了します...");
                Console.ReadLine();
            }
        }
    }
}
