using System;
using System.Collections.Generic;
using Kotoban.Core.Services.OpenAi.Models;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API のリクエストを生成するファクトリークラスです。
/// </summary>
public class OpenAiRequestFactory
{
    /// <summary>
    /// OpenAI API キー。
    /// </summary>
    public string ApiKey { get; }

    /// <summary>
    /// OpenAI API ベース URL。
    /// </summary>
    public string ApiBase { get; }

    /// <summary>
    /// チャットモデル名。
    /// </summary>
    public string ChatModel { get; }

    /// <summary>
    /// 画像生成モデル名。
    /// </summary>
    public string ImageModel { get; }

    /// <summary>
    /// DI された設定から新しいインスタンスを生成します。
    /// </summary>
    /// <param name="settings">OpenAI 設定</param>
    /// <exception cref="InvalidOperationException">必須設定が不足している場合</exception>
    public OpenAiRequestFactory(OpenAiSettings settings)
    {
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            throw new InvalidOperationException("ApiKey is required.");
        }
        if (string.IsNullOrEmpty(settings.ApiBase))
        {
            throw new InvalidOperationException("ApiBase is required.");
        }
        if (string.IsNullOrEmpty(settings.ChatModel))
        {
            throw new InvalidOperationException("ChatModel is required.");
        }
        if (string.IsNullOrEmpty(settings.ImageModel))
        {
            throw new InvalidOperationException("ImageModel is required.");
        }

        ApiKey = settings.ApiKey;
        ApiBase = settings.ApiBase;
        ChatModel = settings.ChatModel;
        ImageModel = settings.ImageModel;
    }

    /// <summary>
    /// チャットリクエストを作成します。
    /// </summary>
    /// <param name="messages">メッセージのリスト</param>
    /// <param name="additionalParameters">追加パラメータ（省略可能）</param>
    /// <returns>チャットリクエスト</returns>
    public OpenAiChatRequest CreateChatRequest(
        List<OpenAiChatMessage> messages,
        Dictionary<string, object>? additionalParameters = null)
    {
        return new OpenAiChatRequest
        {
            Model = ChatModel,
            Messages = messages,
            AdditionalParameters = additionalParameters
        };
    }

    /// <summary>
    /// シンプルなチャットリクエストを作成します。
    /// </summary>
    /// <param name="userMessage">ユーザーメッセージ</param>
    /// <param name="systemMessage">システムメッセージ（省略可能）</param>
    /// <param name="additionalParameters">追加パラメータ（省略可能）</param>
    /// <returns>チャットリクエスト</returns>
    public OpenAiChatRequest CreateSimpleChatRequest(
        string userMessage,
        string? systemMessage = null,
        Dictionary<string, object>? additionalParameters = null)
    {
        var messages = new List<OpenAiChatMessage>();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            messages.Add(new OpenAiChatMessage
            {
                Role = "system",
                Content = systemMessage
            });
        }

        messages.Add(new OpenAiChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        return CreateChatRequest(messages, additionalParameters);
    }

    /// <summary>
    /// 画像生成リクエストを作成します。
    /// </summary>
    /// <param name="prompt">画像生成プロンプト</param>
    /// <param name="n">生成する画像の枚数（デフォルト: 1）</param>
    /// <param name="size">画像サイズ（省略可能）</param>
    /// <param name="responseFormat">レスポンス形式（省略可能）</param>
    /// <returns>画像生成リクエスト</returns>
    public OpenAiImageRequest CreateImageRequest(
        string prompt,
        int n = 1,
        string? size = null,
        string? quality = null,
        string? responseFormat = null)
    {
        return new OpenAiImageRequest
        {
            Model = ImageModel,
            Prompt = prompt,
            N = n,
            Size = size,
            Quality = quality,
            ResponseFormat = responseFormat
        };
    }
}
