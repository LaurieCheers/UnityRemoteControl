using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;

namespace Unity.RemoteControl.Editor
{
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> s_ActionQueue = new ConcurrentQueue<Action>();
        private static bool s_Initialized;

        public static void Initialize()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            EditorApplication.update += ProcessQueue;
        }

        public static void Shutdown()
        {
            if (!s_Initialized)
                return;

            s_Initialized = false;
            EditorApplication.update -= ProcessQueue;

            while (s_ActionQueue.TryDequeue(out _)) { }
        }

        private static void ProcessQueue()
        {
            while (s_ActionQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            s_ActionQueue.Enqueue(action);
        }

        public static Task<T> EnqueueAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();

            Enqueue(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public static Task EnqueueAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}
