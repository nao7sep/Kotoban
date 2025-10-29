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

namespace Kotoban.DataManager
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
            // Windows では、VSC での起動でも Explorer でのダブルクリックでの起動でも、カレントディレクトリーが実行ファイルのあるディレクトリーになる。
            // Mac では、前者は大丈夫だが、Finder でのダブルクリックや dotnet コマンドでの起動では、カレントディレクトリーがユーザーのホームディレクトリーになり、
            // builder.Configuration での相対パスの解決がうまくいかず、appsettings.json が見つからないことがある。
            //
            // builder.Configuration.SetBasePath(AppPath.ExecutableDirectory) では、うまくいかない。
            // おそらく、内部で IConfigurationBuilder.AddJsonFile が呼ばれた時点で、すでにパスのマッピングが終わっている。
            // builder.Configuration.Sources.Clear しての再構築という方法もあるが、今のところは ContentRootPath で足りている。
            // Host.CreateApplicationBuilder を使わない方法もあるが、その場合、「だいたいいつもこういう感じ」という組み立てを自分でやることになる。
            //
            // Environment.CurrentDirectory を最初に更新するのが一番シンプルだが、
            // args として与えられる全ての相対パスの意味が変わるため、一番やってはいけないことだろう。
            // あえて別のディレクトリーからフルパスでこのアプリが実行されて、そこに相対パスでファイルが指定されたとするなら、
            // ユーザーの意図は、その別のディレクトリーをカレントディレクトリーとしてのファイルの指定だ。

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppPath.ExecutableDirectory
            });

#if DEBUG
            // 型 Program は、.csproj ファイルを特定し、UserSecretsId を探すことに使われる。
            // 同じアセンブリーに含まれる型ならなんでもよいとのこと。
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

            // 蛇足コメント: Serilog についてよく知らず、Log.Logger にインスタンスをあてがっては、それをサービス登録していた。
            // コードの後半では Log.Error などを使っていて、たぶん動作は今と同じだったが、DI の徹底により派生開発耐性をつける今の手法とは違った。

            var serilogLogger = new LoggerConfiguration()

#if DEBUG
                // やりとりした JSON が LogTrace により出力される。
                // Serilog には Verbose が、Microsoft の LogLevel には Trace がある。
                // Trace and Verbose are already treated as synonyms とのこと。
                // https://github.com/serilog/serilog-extensions-logging/issues/57
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
            // 単一 UI に単一セットのデータが関連づけられる設計なので、そのデータもサービス登録してしまう。
            // AI がやりだしたときには、「Program.メンバー」または「ほかのクラス.静的メンバー」でよいのではと思った。
            // しかし、Scoped, Singleton, Transient の違いを学び、サービス登録による拡張性を理解した。

            // しばらくしての追記: クラスのインスタンスを生成して Add* するもの、
            // <インターフェース, クラス> と書くもの、<インターフェース>(インスタンス) と書くものが混在するように。
            //
            // まず <I, C> は、型指定が I でも C でも取れるインスタンスを DI により生成する。
            // コンストラクターに必要なサービスがその時点で見つからないと、「解決できない」を旨とする例外が飛ぶ。
            // <Int>(Ins) は、すぐに Ins を使いたいときや、その初期化を自分で行いたいときに適する。
            // (I) だけのものは、インターフェースのあるクラスには適さない。
            //
            // インターフェースのあるクラスに（インターフェースにはない）固有の機能を入れて、
            // クラスの型によりインスタンスを取得してそれらを使うのは、たぶんどこかに改善の余地がある。
            // DataFile などが欲しくて Main メソッドではそれに近いことをしているが、
            // どうせ DataManager では SQL データベースに対応しないだろうから、
            // もうちょっとシンプルにつくってもよかったかもしれない。
            //
            // サービス管理をやってみての教訓は、次の通り。
            // - 設定のセクションごとにドメインモデルをつくり、そこには生データをそのまま入れておく。
            // - 実装を切り替えうるサービスクラスなら、インターフェースをつくり、少なくともロジックは切り替えられるのを保証する。
            // - 基本的にあらゆるサービスクラスを DI ベースでつくり、外部で加工したパラメーターを受け付けない。
            // - それぞれのサービスクラスで加工したデータを、共益性があるなら、すぐには不要でもプロパティーとして公開する。
            //
            // 今回のコードは、入念にリファクタリングしたので、それなりにちゃんとした設計のはず。
            // ただ、永続的ストレージに関するところのみ、「どうせ JSON しか対応しない」との前提で妥協している。
            //
            // いろいろなデータソースに対応するプロジェクトなら、たとえばバックアップ方法はそれぞれ大きく異なるだろうから、
            // appsettings.json などには PersistentStorageSettings などをつくり、その中に Json, Sql などをつくり、
            // IPersistentStorageSettings をつくり、JsonPersistentStorageSettings, SqlPersistentStorageSettings などを実装するのが一つの方法。
            // Json, Sql などの上位にある共通的な項目も入れておきながらも、下位でそれぞれの値を上書きできれば、シンプルな実装で多くのことができそう。

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

            // ここで ActionDispatcher のインスタンスをつくり、action を登録し、AddSingleton することも可能だが、
            // ILogger でも serilogLogger でもなく ILogger<ActionDispatcher> を使いたいので、RunApplicationLoop 内で。
            // スコープを合わせたく、scopedServices を使いたいというのもある。
            builder.Services.AddSingleton<ActionDispatcher>();
        }
    }
}
