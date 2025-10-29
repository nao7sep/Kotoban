using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kotoban.Core.Services.OpenAi.Models
{
    /// <summary>
    /// OpenAI API のリクエスト・レスポンス双方のモデルの基底クラスです。
    /// </summary>
    public abstract class OpenAiApiObjectBase
    {
        /// <summary>
        /// デシリアライズ時に、明示的に定義されていないプロパティをすべてキャプチャします。
        /// シリアライズ時には、このプロパティは無視されます。
        /// 必要に応じて、カスタムコンバーターがこのディクショナリの内容をトップレベルのプロパティとしてシリアライズする必要があります。
        /// </summary>
        [JsonExtensionData]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
}
