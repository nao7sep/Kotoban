using System;
using System.Collections.Generic;

namespace Kotoban.Core.Services.OpenAi;

/// <summary>
/// OpenAI API との通信でリクエストやレスポンス、エラーなどの情報を記録するための辞書型クラスです。
/// ストリーミング対応やデバッグ用途のため、各キーに複数の値（チャンクなど）を格納できます。
/// </summary>
public class OpenAiTraceDictionary : Dictionary<string, List<string>>
{
    /// <summary>
    /// 指定したキーの最初の値を安全に取得します。
    /// 値が存在しない場合は null を返します。
    /// </summary>
    /// <param name="key">取得するキー</param>
    /// <returns>最初の値、または null</returns>
    public string? GetString(string key)
    {
        if (TryGetValue(key, out var list) && list != null && list.Count > 0)
        {
            return list[0];
        }
        return null;
    }

    /// <summary>
    /// 指定したキーに対応するリストを取得します。
    /// キーが存在しない場合は null を返します。
    /// </summary>
    /// <param name="key">取得するキー</param>
    /// <returns>対応するリスト、または null</returns>
    public List<string>? GetList(string key)
    {
        if (TryGetValue(key, out var list) && list != null)
        {
            return list;
        }
        return null;
    }

    /// <summary>
    /// 指定したキーに対して値を1件だけセットします。既存リストがなければ作成し、先頭に値を設定します。
    /// </summary>
    /// <param name="key">キー</param>
    /// <param name="value">セットする値</param>
    public void SetString(string key, string value)
    {
        if (!TryGetValue(key, out var list) || list == null)
        {
            this[key] = new List<string> { value };
        }
        else
        {
            if (list.Count == 0)
            {
                list.Add(value);
            }
            else
            {
                list[0] = value;
            }
        }
    }

    /// <summary>
    /// 指定したキーにチャンクを追加します。
    /// </summary>
    /// <param name="key">追加するキー</param>
    /// <param name="chunk">追加する文字列</param>
    public void AddChunk(string key, string chunk)
    {
        if (!TryGetValue(key, out var list) || list == null)
        {
            list = new List<string>();
            this[key] = list;
        }
        list.Add(chunk);
    }
}
