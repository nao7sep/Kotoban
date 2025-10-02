using System.IO;
using System.Threading.Tasks;

namespace Kotoban.Core.Utils
{
    /// <summary>
    /// ファイル操作用のユーティリティクラス。
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// ファイルを非同期でコピーします。
        /// </summary>
        /// <param name="sourceFileName">コピー元のファイルパス。</param>
        /// <param name="destFileName">コピー先のファイルパス。</param>
        /// <param name="overwrite">コピー先に既存ファイルがある場合に上書きするかどうか。</param>
        public static async Task CopyAsync(string sourceFileName, string destFileName, bool overwrite = false)
        {
            // FileStreamを使って非同期でコピーを実行
            // 4096バイトは多くのシステムで一般的なメモリページやディスクセクタのサイズであり、パフォーマンスとメモリ効率のバランスが良いとされています。
            using (var sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (var destStream = new FileStream(destFileName, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await sourceStream.CopyToAsync(destStream);
            }
        }
    }
}
