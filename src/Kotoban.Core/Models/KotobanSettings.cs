using System;
using Kotoban.Core.Persistence;

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
        public string JsonDataFile { get; set; } = "Kotoban.json";

        /// <summary>
        /// ファイルのバックアップ用ディレクトリパス。
        /// "%TEMP%"を指定するとユーザーのテンポラリフォルダに"Kotoban/Backups"が連結されたものが使われる。
        /// </summary>
        public string BackupDirectory { get; set; } = "%TEMP%";

        /// <summary>
        /// このリポジトリが使用するバックアップ戦略。
        /// </summary>
        public JsonRepositoryBackupMode BackupMode { get; set; } = JsonRepositoryBackupMode.CreateCopy;

        /// <summary>
        /// 保持するバックアップファイルの最大数。
        /// </summary>
        public int MaxBackupFiles { get; set; } = 100;

        /// <summary>
        /// 最終画像ファイルのディレクトリパス。
        /// 相対パスの場合はアプリケーションディレクトリを基準とし、絶対パスの場合はそのまま使用します。
        /// </summary>
        public string FinalImageDirectory { get; set; } = "Images";

        /// <summary>
        /// 画像編集時の一時ファイル用ディレクトリパス。
        /// "%TEMP%"を指定するとユーザーのテンポラリフォルダに"Kotoban/Temp/Images"が連結されたものが使われる。
        /// </summary>
        public string TempImageDirectory { get; set; } = "%TEMP%";

        /// <summary>
        /// 最終画像ファイルの命名パターン。{0}=エントリID、{1}=拡張子
        /// </summary>
        public string FinalImageFileNamePattern { get; set; } = "{0}{1}";

        /// <summary>
        /// 一時画像ファイルの命名パターン。{0}=エントリID、{1}=試行回数または「0」（既存画像）、{2}=拡張子
        /// </summary>
        public string TempImageFileNamePattern { get; set; } = "{0}-{1}{2}";

        /// <summary>
        /// 解説文生成のためのプロンプトフォーマットファイルへのパス。
        /// </summary>
        public string ExplanationPromptFormatFile { get; set; } = "ExplanationPromptFormat.txt";

        /// <summary>
        /// 画像生成のためのプロンプトフォーマットファイルへのパス。
        /// </summary>
        public string ImagePromptFormatFile { get; set; } = "ImagePromptFormat.txt";
    }
}
