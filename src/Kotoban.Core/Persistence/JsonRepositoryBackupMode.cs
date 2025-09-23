namespace Kotoban.Core.Persistence;

/// <summary>
/// リポジトリのバックアップ動作を指定します。
/// </summary>
public enum JsonRepositoryBackupMode
{
    /// <summary>
    /// バックアップは作成されません。
    /// </summary>
    None,

    /// <summary>
    /// 保存する前に、指定されたバックアップディレクトリにデータファイルのコピーが作成されます。
    /// </summary>
    CreateCopy
}
