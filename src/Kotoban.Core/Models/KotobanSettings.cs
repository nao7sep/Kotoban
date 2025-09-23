using System;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// Kotobanアプリケーションのパスとディレクトリの設定。
    /// </summary>
    public class KotobanSettings
    {
        /// <summary>
        /// JSONデータファイルのパス。相対パスまたは絶対パスを指定可能。
        /// </summary>
        public string JsonDataFilePath { get; set; } = "Kotoban-Data.json";

        /// <summary>
        /// JSONファイルのバックアップ用ディレクトリパス。"%TEMP%"を指定するとユーザーのテンポラリフォルダを指す。
        /// アプリケーションは通常、このパスに"Kotoban/Backups"などのサブディレクトリを追加することが推奨される。
        /// そういうのもライブラリーでやってしまえるが、柔軟性を残すため、
        /// JsonEntryRepository には KotobanSettings でなくパスを注入するようにしている。
        /// </summary>
        public string JsonBackupDirectory { get; set; } = "%TEMP%";

        /// <summary>
        /// 保持するバックアップファイルの最大数。
        /// </summary>
        public int MaxBackupFiles { get; set; } = 100;

        /// <summary>
        /// データファイルの場所からの画像ディレクトリへの相対パス。
        /// </summary>
        public string RelativeImageDirectory { get; set; } = "Kotoban-Files/Images";

        /// <summary>
        /// 画像編集時の一時ファイル用ディレクトリパス。"%TEMP%"を指定するとユーザーのテンポラリフォルダを指す。
        /// アプリケーションは通常、このパスに"Kotoban/Temp/Images"などのサブディレクトリを追加することが推奨される。
        /// JsonBackupDirectory と同様の理由で、ImageManager には KotobanSettings でなくパスを注入するようにしている。
        /// </summary>
        public string ImageTempDirectory { get; set; } = "%TEMP%";

        /// <summary>
        /// 最終画像ファイルの命名パターン。{0}=エントリID、{1}=拡張子
        /// </summary>
        public string FinalImageFileNamePattern { get; set; } = "{0}{1}";

        /// <summary>
        /// 一時画像ファイルの命名パターン。{0}=エントリID、{1}=タイムスタンプ、{2}=拡張子
        /// </summary>
        public string TempImageFileNamePattern { get; set; } = "{0}-{1}{2}";

        /// <summary>
        /// 解説文生成のためのプロンプトフォーマット。
        /// </summary>
        public string ExplanationPromptFormat { get; set; } = string.Empty;

        /// <summary>
        /// 画像生成のためのプロンプトフォーマット。
        /// </summary>
        public string ImagePromptFormat { get; set; } = string.Empty;
    }
}