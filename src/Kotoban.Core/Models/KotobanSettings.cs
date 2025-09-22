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
    }
}