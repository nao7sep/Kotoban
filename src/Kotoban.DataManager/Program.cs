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

            // ここで logger と先ほどの serilogLogger の違いをちゃんと理解しておくことは非常に重要。
            // （マイクを GPT-4.1 に）。

            // serilogLogger は Serilog の生のロガーインスタンスであり、Serilog 独自の API（Write, Information, Error など）を直接使ってログ出力できます。
            // 一方、logger（ILogger<Program>）は Microsoft.Extensions.Logging の抽象ロガーで、DI（依存性注入）経由で取得し、アプリ全体で統一的に利用することが推奨されます。
            //
            // AddSerilog で Serilog をロギングプロバイダーとして登録しているため、ILogger<T> で出力したログも最終的には serilogLogger によって処理され、
            // Serilog の設定（出力先・フォーマット・フィルタなど）が適用されます。
            //
            // ここで <Program> となっているのは「ロガーのカテゴリ名」として型名（この場合は "Kotoban.DataManager.Program"）が自動的に付与されるためです。
            // これにより、ログ出力時に「どのクラスから出たログか」を Serilog 側で判別でき、ログのフィルタリングや出力フォーマットでカテゴリごとの制御が可能になります。
            //
            // まとめ：
            //   - アプリケーションコードでは serilogLogger を直接使わず、ILogger<T>（ここでは ILogger<Program>）を使うのがベストプラクティス。
            //   - ILogger<T> を使うことで、.NET 標準のロギングAPIの恩恵（DI, カテゴリ分け, テスト容易性など）と Serilog の高機能な出力を両立できる。
            //   - <T> には通常「現在のクラス名」を指定し、カテゴリごとにログを分けることで、運用・保守・分析がしやすくなる。

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                // 長々と書いたが、このブロックのほとんどはアプリ名とバージョンの取得。
                // 今のところほかで必要でない情報なので、ここにベタ書き。

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

                // ここで host を丸ごと渡すのはベストプラクティスでないと。
                await MenuSystem.RunApplicationLoopAsync(host.Services);

                // 一時画像を掃除するなら、ここが一番の場所。
                // finally で無防備にやると例外が飛んだときに困る。
                // かといって、finally に try/catch を入れると、万が一にも永続的な問題が起こり始めた場合に気づけない。
                // ここでやるもう一つの利点は、RunApplicationLoop が落ちたなら一時画像が残ってくれてデバッグに役立ちうること。
                await imageManager.CleanupTempImagesAsync(entryId: null);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "An unhandled exception occurred during application execution.");
            }
            finally
            {
                logger.LogInformation("Application shutting down.");

                // SerilogのLoggerはAddSerilog(dispose: true)で登録しているため、
                // ホストのDispose時に自動的にフラッシュ・クローズされる。
                // そのため、ここでCloseAndFlushAsyncを明示的に呼ぶ必要はありません。

                // AI コメントに追記: Log を使っていたときには必要だったこと。
                // 静的プロパティーなので、閉じてフラッシュするタイミングが自分では分からない。
                // しかし、サービス登録すれば、施設管理者が「そろそろ片づけろ」と言う。
                //
                // 静的プロパティーにデータを放り込んでいく構成は、初期化のタイミングが Lazy 頼みになったり、
                // dispose のことを「プロセスが消えるときにどうせ」と開き直ったりになりがち。
                // そのあたりもスマートにできそうで、今後の開発ではデフォルトでこのデザインパターンを採用できそう。

                // await logger.CloseAndFlushAsync();

                // Mac で ReadKey が例外を投げたので、ReadLine に変更した。
                // https://github.com/nao7sep/coding-notes/blob/main/understanding-the-console-readkey-exception-in-a-dotnet-async-finally-block-on-macos.md
                Console.Write("Enterキーを押して終了します...");
                Console.ReadLine();
            }
        }
    }
}
