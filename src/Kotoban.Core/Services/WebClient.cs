using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Services;

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
    /// 指定されたURLからデータをダウンロードし、指定したストリームに書き込みます。
    /// </summary>
    /// <param name="url">ダウンロードするファイルのURL。</param>
    /// <param name="destinationStream">書き込み先のストリーム。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期ダウンロード操作を表すタスク。</returns>
    public async Task DownloadToStreamAsync(string url, Stream destinationStream, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        // ResponseHeadersRead: レスポンスヘッダー受信時点で処理を継続し、本文全体のダウンロードを待たずにストリームとして処理できるようにするオプション
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(destinationStream, cancellationToken);
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
        DirectoryUtils.EnsureParentDirectoryExists(destinationPath);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await DownloadToStreamAsync(url, fileStream, cancellationToken);
    }
}
