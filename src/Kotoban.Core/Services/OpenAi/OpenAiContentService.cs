using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Utils;
using Microsoft.Extensions.Logging;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// AIによるコンテンツ生成を調整するサービスの実装。
/// </summary>
public class OpenAiContentService : IAiContentService
{
    private readonly IPromptFormatProvider _promptFormatProvider;
    private readonly OpenAiTransportContext _transportContext;
    private readonly OpenAiRequestFactory _openAiRequestFactory;
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly WebClient _webClient;
    private readonly ActionDispatcher _actionDispatcher;

    /// <summary>
    /// コンストラクタ。依存サービスを注入する。
    /// </summary>
    /// <param name="promptFormatProvider">プロンプトフォーマットプロバイダー</param>
    /// <param name="transportContext">OpenAI通信コンテキスト</param>
    /// <param name="openAiRequestFactory">OpenAIリクエストファクトリ</param>
    /// <param name="openAiApiClient">OpenAI APIクライアント</param>
    /// <param name="webClient">Webクライアント</param>
    /// <param name="actionDispatcher">アクションディスパッチャー（トレース用）</param>
    public OpenAiContentService(
        IPromptFormatProvider promptFormatProvider,
        OpenAiTransportContext transportContext,
        OpenAiRequestFactory openAiRequestFactory,
        OpenAiApiClient openAiApiClient,
        WebClient webClient,
        ActionDispatcher actionDispatcher)
    {
        _promptFormatProvider = promptFormatProvider;
        _transportContext = transportContext;
        _openAiRequestFactory = openAiRequestFactory;
        _openAiApiClient = openAiApiClient;
        _webClient = webClient;
        _actionDispatcher = actionDispatcher;
    }

    /// <inheritdoc />
    public async Task<GeneratedExplanationResult> GenerateExplanationsAsync(Entry entry, string? newExplanationContext)
    {
        // プロンプトフォーマットをプロバイダーから取得
        var promptFormat = await _promptFormatProvider.GetExplanationPromptFormatAsync();

        var prompt = string.Format(
            promptFormat,
            entry.Reading,
            entry.Expression,
            entry.GeneralContext,
            newExplanationContext
        );

        await _actionDispatcher.InvokeAsync("trace", "prompt", prompt);

        // Structured Outputs 用の response_format を匿名型でセット
        // 次のページで Manual schema をクリックし、Step 2: Supply your schema in the API call を開いたところの例が分かりやすい。
        // https://platform.openai.com/docs/guides/structured-outputs?example=structured-data
        var responseFormat = new
        {
            type = "json_schema",
            json_schema = new
            {
                name = "explanations",
                strict = true,
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        easy = new { type = "string" },
                        moderate = new { type = "string" },
                        advanced = new { type = "string" }
                    },
                    required = new[] { "easy", "moderate", "advanced" },
                    // 久～しぶりに、小一時間、コーディングにつまった。
                    // これを送っていたのに、API から「additionalProperties がない」というエラーメッセージをもらった。
                    // これだけが _ により snake_case になってい「ない」ことになかなか気づけなかった。
                    // 解決策として、PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower の指定をやめて、
                    // OpenAI の API と関わりのある全てのドメインモデルに JsonPropertyName 属性をつけた。
                    additionalProperties = false
                }
            }
        };
        var additionalData = new Dictionary<string, object>
        {
            ["response_format"] = responseFormat
        };

        var request = _openAiRequestFactory.CreateSimpleChatRequest(prompt, additionalData: additionalData);
        var response = await _openAiApiClient.GetChatResponseAsync(_transportContext, request, _actionDispatcher);

        var content = response.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new OpenAiException("AI response content is empty.");
        }

        var newExplanations = ParseExplanations(content);
        if (newExplanations.Count != 3)
        {
            throw new OpenAiException("Failed to parse explanations from AI response.");
        }

        return new GeneratedExplanationResult
        {
            Context = newExplanationContext,
            Explanations = newExplanations
        };
    }

    /// <inheritdoc />
    public async Task<GeneratedImageResult> GenerateImageAsync(Entry entry, string? newImageContext)
    {
        // プロンプトフォーマットをプロバイダーから取得
        var promptFormat = await _promptFormatProvider.GetImagePromptFormatAsync();

        var prompt = string.Format(
            promptFormat,
            entry.Reading,
            entry.Expression,
            entry.GeneralContext,
            newImageContext
        );

        await _actionDispatcher.InvokeAsync("trace", "prompt", prompt);

        var request = _openAiRequestFactory.CreateImageRequest(prompt);
        var response = await _openAiApiClient.GenerateImageAsync(_transportContext, request, _actionDispatcher);

        var data = response.Data?.FirstOrDefault();
        if (data == null)
        {
            throw new OpenAiException("AI response data is empty.");
        }

        var url = data.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new OpenAiException("AI response URL is empty.");
        }

        var imagePrompt = data.RevisedPrompt;

        using var stream = new MemoryStream();
        var headers = await _webClient.DownloadToStreamAsync(url, stream);
        var imageBytes = stream.ToArray();
        headers.TryGetValue("Content-Type", out var contentTypeValues);
        var contentType = contentTypeValues?.FirstOrDefault();
        // 判別できなければ、.png にフォールバックする。
        var extension = WebUtils.GetImageFileExtensionFromContentType(contentType);

        return new GeneratedImageResult
        {
            Context = newImageContext,
            ImageBytes = imageBytes,
            Extension = extension,
            ImagePrompt = imagePrompt
        };
    }

    /// <summary>
    /// OpenAIから返されたJSON文字列をパースし、ExplanationLevelごとの解説文辞書に変換します。
    /// </summary>
    /// <param name="content">AIレスポンスのJSON文字列</param>
    /// <returns>ExplanationLevelごとの解説文辞書</returns>
    private static Dictionary<ExplanationLevel, string> ParseExplanations(string content)
    {
        // 期待するJSON: { "easy": "...", "moderate": "...", "advanced": "..." }
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var dict = new Dictionary<ExplanationLevel, string>();

        if (root.TryGetProperty("easy", out var easyProp) && easyProp.ValueKind == JsonValueKind.String)
        {
            var easy = easyProp.GetString();
            if (!string.IsNullOrWhiteSpace(easy))
            {
                dict[ExplanationLevel.Easy] = easy;
            }
        }

        if (root.TryGetProperty("moderate", out var moderateProp) && moderateProp.ValueKind == JsonValueKind.String)
        {
            var moderate = moderateProp.GetString();
            if (!string.IsNullOrWhiteSpace(moderate))
            {
                dict[ExplanationLevel.Moderate] = moderate;
            }
        }

        if (root.TryGetProperty("advanced", out var advancedProp) && advancedProp.ValueKind == JsonValueKind.String)
        {
            var advanced = advancedProp.GetString();
            if (!string.IsNullOrWhiteSpace(advanced))
            {
                dict[ExplanationLevel.Advanced] = advanced;
            }
        }

        return dict;
    }
}
