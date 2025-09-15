using System.IO;

namespace Kotoban.Core.Utils;

/// <summary>
/// ディレクトリ操作に関連するユーティリティメソッドを提供します。
/// </summary>
public static class DirectoryUtils
{
    /// <summary>
    /// 指定されたファイルパスの親ディレクトリが存在することを確認します。
    /// ディレクトリが存在しない場合は作成します。
    /// </summary>
    /// <param name="path">ファイルパスまたはディレクトリパス。</param>
    public static void EnsureParentDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
