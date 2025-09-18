using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kotoban.Core.Services.OpenAi.Models;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API との通信を行うクライアントクラスです。
/// </summary>
public class OpenAiApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiNetworkSettings _networkSettings;

    /// <summary>
    /// DI 用コンストラクタ。
    /// </summary>
    /// <param name="httpClientFactory">HttpClient ファクトリ</param>
    /// <param name="networkSettings">ネットワーク設定</param>
    public OpenAiApiClient(IHttpClientFactory httpClientFactory, OpenAiNetworkSettings networkSettings)
    {
        _httpClientFactory = httpClientFactory;
        _networkSettings = networkSettings;
    }

    /// <summary>
    /// OpenAI Chat API へリクエストを送信し、レスポンスを取得します。
    /// </summary>
    /// <param name="transport">リクエストごとの OpenAI API 認証・エンドポイント情報</param>
    /// <param name="request">リクエストモデル</param>
    /// <param name="trace">トレース用ディクショナリ</param>
    /// <param name="cancellationToken">キャンセルトークン（省略時はデフォルトタイムアウト）</param>
    /// <returns>レスポンスモデル</returns>
    /// <exception cref="Exception">API 通信エラー時</exception>
    public async Task<OpenAiChatResponse> GetChatResponseAsync(
        OpenAiTransportContext transport,
        OpenAiChatRequest request,
        OpenAiTraceDictionary trace,
        CancellationToken? cancellationToken = null)
    {
        using var client = _httpClientFactory.CreateClient();

        var url = (transport?.ApiBase?.TrimEnd('/') ?? "https://api.openai.com/v1") + "/chat/completions";
        var json = JsonSerializer.Serialize(request);
        trace.SetString("request", json);

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // ApiKey が利用可能な場合のみ Authorization ヘッダーを追加
        if (!string.IsNullOrEmpty(transport?.ApiKey))
        {
            message.Headers.Add("Authorization", $"Bearer {transport.ApiKey}");
        }

        // 呼び出し元がキャンセルトークンを指定した場合、タイムアウト用トークンとリンクさせることで、
        // どちらか一方がキャンセルされた時点でリクエスト全体を中断できるようにする。
        // これにより、呼び出し元のキャンセル要求またはタイムアウトのいずれか早い方で確実にキャンセルされる。
        using var cts = cancellationToken.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
            : new CancellationTokenSource(_networkSettings.Timeout);
        var token = cts.Token;

        using HttpResponseMessage response = await client.SendAsync(message, token);
        var responseBody = await response.Content.ReadAsStringAsync(token);
        trace.SetString("response", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            trace.SetString("error", responseBody);
            OpenAiErrorResponse? error = null;
            try
            {
                error = JsonSerializer.Deserialize<OpenAiErrorResponse>(responseBody);
            }
            catch { }
            var errorMsg = error?.Error?.Message ?? $"OpenAI API returned error: {response.StatusCode}";
            throw new OpenAiException(errorMsg, error);
        }

        var result = JsonSerializer.Deserialize<OpenAiChatResponse>(responseBody);
        if (result == null)
        {
            throw new OpenAiException("Failed to deserialize OpenAI chat response.");
        }
        return result;
    }

    /// <summary>
    /// OpenAI 画像生成 API へリクエストを送信し、レスポンスを取得します。
    /// </summary>
    /// <param name="transport">リクエストごとの OpenAI API 認証・エンドポイント情報</param>
    /// <param name="request">リクエストモデル</param>
    /// <param name="trace">トレース用ディクショナリ</param>
    /// <param name="cancellationToken">キャンセルトークン（省略時はデフォルトタイムアウト）</param>
    /// <returns>レスポンスモデル</returns>
    /// <exception cref="Exception">API 通信エラー時</exception>
    public async Task<OpenAiImageResponse> GenerateImageAsync(
        OpenAiTransportContext transport,
        OpenAiImageRequest request,
        OpenAiTraceDictionary trace,
        CancellationToken? cancellationToken = null)
    {
        using var client = _httpClientFactory.CreateClient();

        var url = (transport?.ApiBase?.TrimEnd('/') ?? "https://api.openai.com/v1") + "/images/generations";
        var json = JsonSerializer.Serialize(request);
        trace.SetString("request", json);

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // ApiKey が利用可能な場合のみ Authorization ヘッダーを追加
        if (!string.IsNullOrEmpty(transport?.ApiKey))
        {
            message.Headers.Add("Authorization", $"Bearer {transport.ApiKey}");
        }

        using var cts = cancellationToken.HasValue
            // CreateLinkedTokenSource については、GetChatResponseAsync のところに。
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
            : new CancellationTokenSource(_networkSettings.Timeout);
        var token = cts.Token;

        using HttpResponseMessage response = await client.SendAsync(message, token);
        var responseBody = await response.Content.ReadAsStringAsync(token);
        trace.SetString("response", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            trace.SetString("error", responseBody);
            OpenAiErrorResponse? error = null;
            try
            {
                error = JsonSerializer.Deserialize<OpenAiErrorResponse>(responseBody);
            }
            catch { }
            var errorMsg = error?.Error?.Message ?? $"OpenAI API returned error: {response.StatusCode}";
            throw new OpenAiException(errorMsg, error);
        }

        var result = JsonSerializer.Deserialize<OpenAiImageResponse>(responseBody);
        if (result == null)
        {
            throw new OpenAiException("Failed to deserialize OpenAI image response.");
        }
        return result;
    }
}
