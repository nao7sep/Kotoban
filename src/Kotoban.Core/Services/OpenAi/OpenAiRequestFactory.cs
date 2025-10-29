using System;
using System.Collections.Generic;
using Kotoban.Core.Models;
using Kotoban.Core.Services.OpenAi.Models;

namespace Kotoban.Core.Services.OpenAi
{
    /// <summary>
    /// OpenAI API のリクエストを生成するファクトリークラスです。
    /// </summary>
    public class OpenAiRequestFactory
    {
        private readonly OpenAiSettings _settings;

        /// <summary>
        /// チャットモデル名。
        /// </summary>
        public string ChatModel { get; }

        /// <summary>
        /// 画像生成モデル名。
        /// </summary>
        public string ImageModel { get; }

        // `appsettings.json` から `Dictionary<string, object>` 型のプロパティ（例: ChatRequestAdditionalData）をバインドする際、
        // `Microsoft.Extensions.Configuration.Binder` は数値や真偽値などのJSONプリミティブ型を `string` として読み込んでしまいます。
        // これにより、OpenAI API へのリクエスト時に型不一致のエラーが発生しました。
        // この問題を回避するには、対象のオブジェクトを個別にデシリアライズするなどの追加処理が必要になりますが、
        // その複雑さに見合うほどのメリットがないため、現在は `additionalData` を外部設定から読み込む機能は実装していません。

        /// <summary>
        /// DI された設定から新しいインスタンスを生成します。
        /// </summary>
        /// <param name="settings">OpenAI 設定</param>
        /// <exception cref="InvalidOperationException">必須設定が不足している場合</exception>
        public OpenAiRequestFactory(OpenAiSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ChatModel))
            {
                throw new InvalidOperationException("ChatModel is required.");
            }
            if (string.IsNullOrWhiteSpace(settings.ImageModel))
            {
                throw new InvalidOperationException("ImageModel is required.");
            }

            _settings = settings;
            ChatModel = _settings.ChatModel;
            ImageModel = _settings.ImageModel;
        }

        /// <summary>
        /// チャットリクエストを作成します。
        /// </summary>
        /// <param name="messages">メッセージのリスト</param>
        /// <param name="additionalData">追加パラメータ（省略可能）</param>
        /// <returns>チャットリクエスト</returns>
        public OpenAiChatRequest CreateChatRequest(
            List<OpenAiChatMessage> messages,
            Dictionary<string, object>? additionalData = null)
        {
            return new OpenAiChatRequest
            {
                Model = ChatModel,
                Messages = messages,
                AdditionalData = additionalData
            };
        }

        /// <summary>
        /// シンプルなチャットリクエストを作成します。
        /// </summary>
        /// <param name="userMessage">ユーザーメッセージ</param>
        /// <param name="systemMessage">システムメッセージ（省略可能）</param>
        /// <param name="additionalData">追加パラメータ（省略可能）</param>
        /// <returns>チャットリクエスト</returns>
        public OpenAiChatRequest CreateSimpleChatRequest(
            string userMessage,
            string? systemMessage = null,
            Dictionary<string, object>? additionalData = null)
        {
            var messages = new List<OpenAiChatMessage>();

            if (!string.IsNullOrWhiteSpace(systemMessage))
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

            return CreateChatRequest(messages, additionalData);
        }

        /// <summary>
        /// 画像生成リクエストを作成します。
        /// </summary>
        /// <param name="prompt">画像生成プロンプト</param>
        /// <param name="n">生成する画像の枚数（デフォルト: 1）</param>
        /// <param name="quality">画質（省略可能）</param>
        /// <param name="size">画像サイズ（省略可能）</param>
        /// <param name="style">画像のスタイル（省略可能）</param>
        /// <param name="responseFormat">レスポンス形式（省略可能）</param>
        /// <param name="additionalData">追加パラメータ（省略可能）</param>
        /// <returns>画像生成リクエスト</returns>
        public OpenAiImageRequest CreateImageRequest(
            string prompt,
            int? n = null,
            string? quality = null,
            string? size = null,
            string? style = null,
            string? responseFormat = null,
            Dictionary<string, object>? additionalData = null)
        {
            return new OpenAiImageRequest
            {
                Model = ImageModel,
                Prompt = prompt,
                N = n,
                Quality = quality,
                Size = size,
                Style = style,
                ResponseFormat = responseFormat,
                AdditionalData = additionalData
            };
        }
    }
}
