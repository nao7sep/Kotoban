using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kotoban.Core.Services.OpenAi.Models;

namespace Kotoban.Core.Services.OpenAi
{
    /// <summary>
    /// OpenAI API との通信を行うクライアントクラスです。
    /// </summary>
    public class OpenAiApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OpenAiNetworkSettings _networkSettings;

        /// <summary>
        /// 依存性注入（DI）によってインスタンスを生成するためのコンストラクタです。
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
        /// <param name="dispatcher">アクションディスパッチャー（トレース用）</param>
        /// <param name="cancellationToken">キャンセルトークン（省略時はデフォルトタイムアウト）</param>
        /// <returns>レスポンスモデル</returns>
        /// <exception cref="Exception">API 通信エラー時</exception>
        public async Task<OpenAiChatResponse> GetChatResponseAsync(
            OpenAiTransportContext transport,
            OpenAiChatRequest request,
            ActionDispatcher dispatcher,
            CancellationToken? cancellationToken = null)
        {
            using var client = _httpClientFactory.CreateClient();

            var url = (transport?.ApiBase?.TrimEnd('/') ?? "https://api.openai.com/v1") + "/chat/completions";

            var requestOptions = new JsonSerializerOptions(OpenAiApiJsonOptions.BaseRequestSerializationOptions);
            requestOptions.Converters.Add(new OpenAiChatRequestConverter());
            var json = JsonSerializer.Serialize(request, requestOptions);

            await dispatcher.InvokeAsync("trace", "request", json);

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // ApiKey が利用可能な場合のみ Authorization ヘッダーを追加
            if (!string.IsNullOrWhiteSpace(transport?.ApiKey))
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
            await dispatcher.InvokeAsync("trace", "response", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response, responseBody, dispatcher);
            }

            var result = JsonSerializer.Deserialize<OpenAiChatResponse>(responseBody, OpenAiApiJsonOptions.BaseResponseDeserializationOptions);
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
        /// <param name="dispatcher">アクションディスパッチャー（トレース用）</param>
        /// <param name="cancellationToken">キャンセルトークン（省略時はデフォルトタイムアウト）</param>
        /// <returns>レスポンスモデル</returns>
        /// <exception cref="Exception">API 通信エラー時</exception>
        public async Task<OpenAiImageResponse> GenerateImageAsync(
            OpenAiTransportContext transport,
            OpenAiImageRequest request,
            ActionDispatcher dispatcher,
            CancellationToken? cancellationToken = null)
        {
            using var client = _httpClientFactory.CreateClient();

            var url = (transport?.ApiBase?.TrimEnd('/') ?? "https://api.openai.com/v1") + "/images/generations";
            var requestOptions = new JsonSerializerOptions(OpenAiApiJsonOptions.BaseRequestSerializationOptions);
            requestOptions.Converters.Add(new OpenAiImageRequestConverter());
            var json = JsonSerializer.Serialize(request, requestOptions);
            await dispatcher.InvokeAsync("trace", "request", json);

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // ApiKey が利用可能な場合のみ Authorization ヘッダーを追加
            if (!string.IsNullOrWhiteSpace(transport?.ApiKey))
            {
                message.Headers.Add("Authorization", $"Bearer {transport.ApiKey}");
            }

            // 呼び出し元から渡されたキャンセルトークンと、このメソッド内部のタイムアウト用トークンをリンクさせます。
            // これにより、外部からのキャンセル要求、またはタイムアウトのいずれかが発生した時点で、
            // 即座にリクエストをキャンセルできます。
            using var cts = cancellationToken.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
                : new CancellationTokenSource(_networkSettings.Timeout);
            var token = cts.Token;

            using HttpResponseMessage response = await client.SendAsync(message, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);
            await dispatcher.InvokeAsync("trace", "response", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response, responseBody, dispatcher);
            }

            var result = JsonSerializer.Deserialize<OpenAiImageResponse>(responseBody, OpenAiApiJsonOptions.BaseResponseDeserializationOptions);
            if (result == null)
            {
                throw new OpenAiException("Failed to deserialize OpenAI image response.");
            }
            return result;
        }

        /// <summary>
        /// エラーレスポンスを処理し、適切な OpenAiException を投げます。
        /// </summary>
        /// <param name="response">HTTP レスポンス</param>
        /// <param name="responseBody">レスポンス本文</param>
        /// <param name="dispatcher">アクションディスパッチャー（トレース用）</param>
        /// <exception cref="OpenAiException">常に投げられる例外</exception>
        private static async Task HandleErrorResponse(HttpResponseMessage response, string responseBody, ActionDispatcher dispatcher)
        {
            await dispatcher.InvokeAsync("trace", "error", responseBody);
            OpenAiErrorResponse? error = null;
            try
            {
                error = JsonSerializer.Deserialize<OpenAiErrorResponse>(responseBody, OpenAiApiJsonOptions.BaseResponseDeserializationOptions);
                if (error != null)
                {
                    // 正常にエラーレスポンスをデシリアライズできた場合
                    var errorMsg = error.Error?.Message ?? $"OpenAI API returned error: {response.StatusCode}";
                    throw new OpenAiException(errorMsg, error);
                }
                else
                {
                    // デシリアライズは成功したが結果が null の場合
                    throw new OpenAiException($"OpenAI API returned error: {response.StatusCode}. Failed to parse error response.");
                }
            }
            catch (OpenAiException)
            {
                // OpenAiException は再スローする
                throw;
            }
            catch (Exception ex)
            {
                // JSON デシリアライゼーション中に例外が発生した場合
                throw new OpenAiException($"OpenAI API returned error: {response.StatusCode}. Failed to parse error response.", ex);
            }
        }
    }
}
