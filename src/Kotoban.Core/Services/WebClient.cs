using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Kotoban.Core.Utils;

namespace Kotoban.Core.Services
{
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
        /// レスポンスヘッダー情報も返します。
        /// </summary>
        /// <param name="url">ダウンロードするファイルのURL。</param>
        /// <param name="destinationStream">書き込み先のストリーム。</param>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>レスポンスヘッダー情報（Dictionary<string, IEnumerable<string>>）を含むタスク。</returns>
        public async Task<Dictionary<string, IEnumerable<string>>> DownloadToStreamAsync(string url, Stream destinationStream, CancellationToken cancellationToken = default)
        {
            using var client = _httpClientFactory.CreateClient();
            // ResponseHeadersRead: レスポンスヘッダー受信時点で処理を継続し、本文全体のダウンロードを待たずにストリームとして処理できるようにするオプション
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await stream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);

            // --- HTTPレスポンスで取得できる主な情報 ---
            // ・ステータスコード（例: 200, 404, 500）
            // ・レスポンスヘッダー（Content-Type, Content-Length, Content-Disposition, Set-Cookie, ETag, など）
            // ・レスポンスボディ（ファイルやJSONなどの実データ）
            // ・トレーラー（HTTP/2以降で稀に利用）
            // ・カスタムヘッダー（X-Request-Idなど）
            //
            // --- なぜ今はヘッダーのみ返すのか ---
            // 現状はContent-Typeのみ取得できれば十分な要件のため、
            // 必要な情報を柔軟に拡張できるようDictionary<string, IEnumerable<string>>で全ヘッダーを返す設計とした。
            // 今後Content-DispositionやContent-Length等が必要になった場合も、呼び出し側で柔軟に対応できる。
            //
            // ヘッダー情報をDictionary<string, IEnumerable<string>>として返す
            // 先にresponse.Headersを追加し、次にresponse.Content.Headersを追加する。
            // Content.Headersの値で重複キーがあれば上書きする。
            // 理由: Content-TypeやContent-Lengthなど、実際のコンテンツに関する情報はContent.Headersの方が正確な場合が多いため。

            // 補足: response.Content.Headersの内容はレスポンス受信時にメモリ上に保持されるため、
            // ストリーム（ReadAsStreamAsyncで取得したもの）をDisposeした後でも安全にアクセスできる。
            // Disposeの影響を受けるのは本文ストリームのみであり、ヘッダー情報は影響を受けない。

            var headers = new Dictionary<string, IEnumerable<string>>(response.Headers, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in response.Content.Headers)
            {
                headers[kvp.Key] = kvp.Value;
            }
            return headers;
        }
    }
}
