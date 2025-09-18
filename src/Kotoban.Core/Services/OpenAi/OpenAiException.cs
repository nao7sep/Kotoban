using System;

namespace Kotoban.Core.Services.OpenAi
{
    /// <summary>
    /// OpenAI API 関連の例外を表します。
    /// </summary>
    public class OpenAiException : Exception
    {
        /// <summary>
        /// OpenAI のエラーレスポンス（存在する場合）。
        /// </summary>
        /// <remarks>
        /// 現状は error プロパティのみだが、将来的に OpenAI 側でルートに他の情報が追加される可能性があるため、
        /// レスポンス全体を保持する。必要に応じて error のみを参照できる。
        /// </remarks>
        public Models.OpenAiErrorResponse? ErrorResponse { get; }

        public OpenAiException() { }

        /// <summary>
        /// メッセージを指定して OpenAI 例外を生成します。
        /// </summary>
        /// <param name="message">例外メッセージ</param>
        public OpenAiException(string message) : base(message) { }

        /// <summary>
        /// メッセージと内部例外を指定して OpenAI 例外を生成します。
        /// </summary>
        /// <param name="message">例外メッセージ</param>
        /// <param name="innerException">内部例外</param>
        public OpenAiException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// メッセージと OpenAI のエラーレスポンスを指定して例外を生成します。
        /// </summary>
        /// <param name="message">例外メッセージ</param>
        /// <param name="errorResponse">OpenAI のエラーレスポンス</param>
        public OpenAiException(string message, Models.OpenAiErrorResponse? errorResponse) : base(message)
        {
            ErrorResponse = errorResponse;
        }

        /// <summary>
        /// メッセージ、OpenAI のエラーレスポンス、内部例外を指定して例外を生成します。
        /// </summary>
        /// <param name="message">例外メッセージ</param>
        /// <param name="errorResponse">OpenAI のエラーレスポンス</param>
        /// <param name="innerException">内部例外</param>
        public OpenAiException(string message, Models.OpenAiErrorResponse? errorResponse, Exception innerException) : base(message, innerException)
        {
            ErrorResponse = errorResponse;
        }
    }
}
