using System;
using System.IO;
using System.Reflection;

namespace Kotoban.Core.Utils;

/// <summary>
/// アプリケーションの実行パスに関連する情報を提供します。
/// </summary>
public static class AppPath
{
    /// <summary>
    /// 現在実行中のアセンブリを取得します。
    /// </summary>
    public static Assembly ExecutingAssembly { get; } = Assembly.GetExecutingAssembly();

    /// <summary>
    /// 実行可能ファイルの完全パスを取得します。
    /// </summary>
    public static string ExecutablePath { get; } = ExecutingAssembly.Location;

    /// <summary>
    /// 実行可能ファイルが存在するディレクトリの完全パスを取得します。
    /// </summary>
    public static string ExecutableDirectory { get; } = Path.GetDirectoryName(ExecutablePath) ?? Environment.CurrentDirectory;

    /// <summary>
    /// 実行可能ファイルのディレクトリからの相対パスを絶対パスに変換します。
    /// 入力が絶対パスの場合は例外をスローします。
    /// </summary>
    /// <param name="relativePath">変換する相対パス（絶対パスは不可）。</param>
    /// <returns>解決された絶対パス。</returns>
    /// <exception cref="ArgumentException">relativePath が絶対パスの場合</exception>
    public static string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
            throw new ArgumentException("Path must be relative.", nameof(relativePath));
        var combinedPath = Path.Combine(ExecutableDirectory, relativePath);
        return Path.GetFullPath(combinedPath);
    }
}
