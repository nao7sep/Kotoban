using System;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// Kotobanアプリケーションのパスとディレクトリの設定。
    /// </summary>
    public class KotobanSettings
    {
        /// <summary>
        /// データファイルのパス。相対パスまたは絶対パスを指定可能。
        /// </summary>
        public string DataFilePath { get; set; } = "Kotoban-Data.json";

        /// <summary>
        /// バックアップファイル用のディレクトリパス。"%TEMP%"を指定するとユーザーのテンポラリフォルダを指す。
        /// </summary>
        public string BackupDirectory { get; set; } = "%TEMP%";

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