using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Kotoban.Core.Services
{
    /// <summary>
    /// 任意のキーで非同期アクション（Func<object?[], Task>）を登録・呼び出しできる汎用ディスパッチャクラス。
    /// </summary>
    public class ActionDispatcher
    {
        // 複数スレッドからの同時アクセスや登録・削除に安全に対応するため、ConcurrentDictionaryを使用しています。
        private readonly ConcurrentDictionary<string, Func<object?[], Task>> _actions = new();

        /// <summary>
        /// 指定したキーに非同期アクション（object?[] パラメータ）を登録します。同じキーで再登録した場合は上書きされます。
        /// </summary>
        /// <param name="key">アクションのキー</param>
        /// <param name="action">登録する非同期アクション（object?[] パラメータ）</param>
        public void Register(string key, Func<object?[], Task> action)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions[key] = action;
        }

        /// <summary>
        /// 指定したキーの非同期アクションを呼び出します。アクションが登録されていない場合は例外をスローします。
        /// </summary>
        /// <param name="key">呼び出すアクションのキー</param>
        /// <param name="parameters">アクションに渡すパラメータ（object?[]）</param>
        /// <exception cref="KeyNotFoundException">指定したキーのアクションが登録されていない場合</exception>
        public async Task InvokeAsync(string key, params object?[] parameters)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!_actions.TryGetValue(key, out var action))
                throw new KeyNotFoundException($"Action for key '{key}' is not registered.");
            await action(parameters);
        }

        /// <summary>
        /// 指定したキーの非同期アクションを呼び出します。アクションが登録されていない場合は何もせず false を返します。
        /// </summary>
        /// <param name="key">呼び出すアクションのキー</param>
        /// <param name="parameters">アクションに渡すパラメータ（object?[]）</param>
        /// <returns>アクションが呼び出された場合は true、登録されていない場合は false</returns>
        public async Task<bool> TryInvokeAsync(string key, params object?[] parameters)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (_actions.TryGetValue(key, out var action))
            {
                await action(parameters);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定したキーのアクションを削除します。
        /// </summary>
        /// <param name="key">削除するアクションのキー</param>
        /// <returns>削除に成功した場合は true</returns>
        public bool Unregister(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return _actions.TryRemove(key, out _);
        }
    }
}
