using System;
using System.IO;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Persistence;
using Kotoban.Core.Services;
using Kotoban.Core.Services.OpenAi;
using Kotoban.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Kotoban.DataManager.Hosting
{
    /// <summary>
    /// アプリケーションのホスト設定とサービス登録を管理します。
    /// </summary>
    internal static class ApplicationHost
    {
        /// <summary>
        /// アプリケーションホストを作成し、すべてのサービスを設定します。
        /// </summary>
        public static async Task<IHost> CreateHostAsync(string[] args)
        {
            // プラットフォーム間でのカレントディレクトリの動作差異に対応するため、
            // ContentRootPath を実行ファイルのディレクトリに明示的に設定します。
            //
            // 【プラットフォーム別の動作】
            // - Windows: VSCode 起動・Explorer ダブルクリック共に実行ファイルディレクトリがカレント
            // - macOS: VSCode 起動は正常だが、Finder ダブルクリック・dotnet コマンド起動では
            //   ユーザーホームディレクトリがカレントとなり、appsettings.json の相対パス解決が失敗
            //
            // 【採用した解決策】
            // ContentRootPath を AppPath.ExecutableDirectory に設定することで、
            // 設定ファイルの相対パス解決を確実に実行ファイルディレクトリ基準で行います。
            //
            // 【検討した他の方法】
            // - SetBasePath(): IConfigurationBuilder.AddJsonFile 実行後では効果なし
            // - Sources.Clear() での再構築: 可能だが複雑
            // - Environment.CurrentDirectory 変更: コマンドライン引数の相対パス解釈に影響するため不適切

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppPath.ExecutableDirectory
            });

#if DEBUG
            // Program 型を使用して .csproj ファイルから UserSecretsId を特定します。
            // 同一アセンブリ内の任意の型を使用可能です。
            builder.Configuration.AddUserSecrets<Program>();
#endif

            ConfigureLogging(builder);
            await ConfigureServicesAsync(builder);

            return builder.Build();
        }

        /// <summary>
        /// Serilogロギングを設定します。
        /// </summary>
        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            // Serilogの手動セットアップ（ファイルロギング用）
            var timestamp = DateTimeUtils.UtcNowTimestamp();
            var logFilePath = AppPath.GetAbsolutePath(Path.Combine("Logs", $"Kotoban-{timestamp}.log"));
            DirectoryUtils.EnsureParentDirectoryExists(logFilePath);

            // Serilog ロガーインスタンスを直接作成し、DI コンテナに登録します。
            // 静的な Log.Logger を使用する方法と比較して、DI による管理により
            // テスト容易性と拡張性が向上します。

            var serilogLogger = new LoggerConfiguration()

#if DEBUG
                // デバッグ時に API との JSON やりとりを LogTrace で出力するため、
                // Verbose レベルを設定します。Serilog の Verbose と Microsoft の Trace は
                // 同義として扱われます。
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Information()
#endif

                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
                .WriteTo.File(logFilePath)
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(serilogLogger, dispose: true);
        }

        /// <summary>
        /// アプリケーションサービスを設定します。
        /// </summary>
        private static async Task ConfigureServicesAsync(HostApplicationBuilder builder)
        {
            // アプリケーションで使用するサービスを DI コンテナに登録します。
            // 単一 UI・単一データセットの設計のため、データもサービスとして登録しています。
            //
            // 【DI 登録パターンの使い分け】
            // - AddSingleton<I, C>(): インターフェースと実装クラスを指定、DI が自動生成
            // - AddSingleton<I>(instance): 事前に作成したインスタンスを登録
            // - AddSingleton<C>(): 具象クラスのみを登録（インターフェースなし）
            //
            // 【サービス設計の原則】
            // - 設定セクションごとにドメインモデルを作成し、生データを保持
            // - 実装切り替え可能なサービスにはインターフェースを定義
            // - 全サービスを DI ベースで構築し、外部での加工済みパラメータは受け付けない
            // - 共通性のあるデータは、即座に不要でもプロパティとして公開
            //
            // 【永続化ストレージの設計】
            // 現在は JSON のみ対応の前提で簡素化していますが、複数データソース対応の場合は
            // IPersistentStorageSettings インターフェースと、Json/Sql 等の具象実装クラスによる
            // 階層化設計が適切です。

            var kotobanSettings = new KotobanSettings();
            builder.Configuration.GetSection("Kotoban").Bind(kotobanSettings);
            builder.Services.AddSingleton(kotobanSettings);

            var openAiSettings = new OpenAiSettings();
            builder.Configuration.GetSection("OpenAi").Bind(openAiSettings);
            builder.Services.AddSingleton(openAiSettings);

            var repository = new JsonEntryRepository(kotobanSettings);
            await repository.LoadDataAsync();
            builder.Services.AddSingleton<IEntryRepository>(repository);

            builder.Services.AddSingleton<OpenAiNetworkSettings>();

            // OpenAiTransportContext は「トランスポート層の責務」を分離するため個別クラス化しています。
            // 認証やエンドポイントなど「リクエストモデル」や「ネットワーク設定モデル」に収まらない情報をまとめる用途です。
            // このアプリではインスタンスが複数必要になる場面はないため、シングルトンで登録しています（シンプルさ優先）。
            builder.Services.AddSingleton<OpenAiTransportContext>();

            builder.Services.AddSingleton<IPromptFormatProvider, PromptFormatProvider>();

            builder.Services.AddSingleton<OpenAiRequestFactory>();

            builder.Services.AddSingleton<OpenAiApiClient>();

            // AddHttpClient() は IHttpClientFactory をDIコンテナに登録し、HttpClientのライフサイクル管理や拡張機能を有効にします。
            builder.Services.AddHttpClient();

            builder.Services.AddSingleton<WebClient>();

            var imageManager = new ImageManager(kotobanSettings);
            builder.Services.AddSingleton<IImageManager>(imageManager);

            builder.Services.AddSingleton<IAiContentService, OpenAiContentService>();

            // ActionDispatcher は ILogger<ActionDispatcher> を使用するため、
            // アクション登録は RunApplicationLoop 内で行います。
            // これにより適切なロガーカテゴリとサービススコープを確保できます。
            builder.Services.AddSingleton<ActionDispatcher>();
        }
    }
}
