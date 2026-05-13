using System;
using System.Collections.Concurrent;

namespace client.Utils;

public static class MainThreadDispatcher
{
    private static readonly ConcurrentQueue<Action> Queue = new();

    public static void Enqueue(Action action)
    {
        if (action != null) Queue.Enqueue(action);
    }

    public static void Drain()
    {
        while (Queue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Plugin.BepinLogger.LogError($"MainThreadDispatcher: {e}"); }
        }
    }
}
