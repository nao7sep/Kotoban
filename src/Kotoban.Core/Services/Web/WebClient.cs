using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Services.Web;

/// <summary>
/// ファイルのダウンロードなど、Web関連の操作を行うユーティリティメソッドを提供します。
/// </summary>
public class WebClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// <see cref="WebClient"/> クラスの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="httpClientFactory">HttpClient インスタンスを生成するファクトリ。</param>
    public WebClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// 指定されたURLからファイルをダウンロードし、指定したパスに保存します。
    /// </summary>
    /// <param name="url">ダウンロードするファイルのURL。</param>
    /// <param name="destinationPath">保存先のローカルパス。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期ダウンロード操作を表すタスク。</returns>
    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        // ResponseHeadersRead: レスポンスヘッダー受信時点で処理を継続し、本文全体のダウンロードを待たずにストリームとして処理できるようにするオプション
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        DirectoryUtils.EnsureParentDirectoryExists(destinationPath);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }
}
