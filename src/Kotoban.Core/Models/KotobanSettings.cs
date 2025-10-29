using System;
using Kotoban.Core.Persistence;

namespace Kotoban.Core.Models
{
    /// <summary>
    /// アプリケーションのパスとディレクトリ設定を定義します。
    /// </summary>
    public class KotobanSettings
    {
        /// <summary>
        /// データストアとして使用されるJSONファイルのパス。
        /// </summary>
        public string JsonDataFile { get; set; } = "Kotoban.json";

        /// <summary>
        /// バックアップファイルを保存するディレクトリのパス。
        /// </summary>
        public string BackupDirectory { get; set; } = "%TEMP%";

        /// <summary>
        /// リポジトリが使用するバックアップ戦略。
        /// </summary>
        public JsonRepositoryBackupMode BackupMode { get; set; } = JsonRepositoryBackupMode.CreateCopy;

        /// <summary>
        /// 保持するバックアップファイルの最大数。
        /// </summary>
        public int MaxBackupFiles { get; set; } = 100;

        /// <summary>
        /// 最終的な画像を保存するディレクトリのパス。
        /// </summary>
        public string FinalImageDirectory { get; set; } = "Images";

        /// <summary>
        /// 画像編集時に使用する一時ディレクトリのパス。
        /// </summary>
        public string TempImageDirectory { get; set; } = "%TEMP%";

        /// <summary>
        /// 最終的な画像ファイル名の命名規則。
        /// </summary>
        public string FinalImageFileNamePattern { get; set; } = "{0}{1}";

        /// <summary>
        /// 一時画像ファイル名の命名規則。
        /// </summary>
        public string TempImageFileNamePattern { get; set; } = "{0}-{1}{2}";

        /// <summary>
        /// 説明文生成に使用するプロンプトフォーマットファイルのパス。
        /// </summary>
        public string ExplanationPromptFormatFile { get; set; } = "ExplanationPromptFormat.txt";

        /// <summary>
        /// 画像生成に使用するプロンプトフォーマットファイルのパス。
        /// </summary>
        public string ImagePromptFormatFile { get; set; } = "ImagePromptFormat.txt";
    }
}
