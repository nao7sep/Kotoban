using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kotoban.Core.Models;
using Kotoban.Core.Services.Web;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// AIによるコンテンツ生成を調整するサービスの実装。
/// </summary>
public class OpenAiContentService : IAiContentService
{
    private readonly KotobanSettings _settings;
    private readonly OpenAiTransportContext _transportContext;
    private readonly OpenAiRequestFactory _openAiRequestFactory;
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly WebClient _webClient;

    public OpenAiContentService(
        KotobanSettings settings,
        OpenAiTransportContext transportContext,
        OpenAiRequestFactory openAiRequestFactory,
        OpenAiApiClient openAiApiClient,
        WebClient webClient)
    {
        _settings = settings;
        _transportContext = transportContext;
        _openAiRequestFactory = openAiRequestFactory;
        _openAiApiClient = openAiApiClient;
        _webClient = webClient;
    }

    /// <inheritdoc />
    public async Task<Dictionary<ExplanationLevel, string>> GenerateExplanationsAsync(Entry entry, string? newExplanationContext)
    {
        if (string.IsNullOrWhiteSpace(_settings.ExplanationPromptFormat))
        {
            throw new InvalidOperationException("ExplanationPromptFormat is not configured.");
        }

        var prompt = string.Format(
            _settings.ExplanationPromptFormat,
            entry.Reading,
            entry.Expression,
            entry.GeneralContext,
            newExplanationContext
        );

        var request = _openAiRequestFactory.CreateSimpleChatRequest(prompt);
        var tracer = new OpenAiTraceDictionary();
        var response = await _openAiApiClient.GetChatResponseAsync(_transportContext, request, tracer);
        var content = response.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new OpenAiException("AI response content is empty.");
        }

        var newExplanations = ParseExplanations(content);
        if (newExplanations == null || !newExplanations.Any())
        {
            throw new OpenAiException("Failed to parse explanations from AI response.");
        }

        return newExplanations;
    }

    /// <inheritdoc />
    public async Task<(byte[] ImageBytes, string Extension, string? ImagePrompt)> GenerateImageAsync(Entry entry, string? newImageContext)
    {
        if (string.IsNullOrWhiteSpace(_settings.ImagePromptFormat))
        {
            throw new InvalidOperationException("ImagePromptFormat is not configured.");
        }

        var prompt = string.Format(
            _settings.ImagePromptFormat,
            entry.Reading,
            entry.Expression,
            entry.GeneralContext,
            newImageContext
        );

        var request = _openAiRequestFactory.CreateImageRequest(prompt);
        var tracer = new OpenAiTraceDictionary();
        var response = await _openAiApiClient.GenerateImageAsync(_transportContext, request, tracer);
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
        await _webClient.DownloadToStreamAsync(url, stream);
        var imageBytes = stream.ToArray();
        // 決め打ちで PNG を返してくる AI をまずは想定。
        var extension = ".png";

        return (imageBytes, extension, imagePrompt);
    }

    private static Dictionary<ExplanationLevel, string>? ParseExplanations(string content)
    {
        // TODO: あとで JSON モードで実装。
        return null;
    }
}
