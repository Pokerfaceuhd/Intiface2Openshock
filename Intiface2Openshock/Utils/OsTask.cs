﻿using System.Runtime.CompilerServices;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Intiface2Openshock.Utils;

public static class OsTask
{
    private static readonly ILogger Logger = Log.ForContext(typeof(OsTask));

    public static Task Run(Func<Task?> function, CancellationToken token = default, [CallerFilePath] string file = "",
        [CallerMemberName] string member = "", [CallerLineNumber] int line = -1)
    {
        var task = Task.Run(function, token);
        task.ContinueWith(
            t =>
            {
                if (!t.IsFaulted) return;
                var index = file.LastIndexOf('\\');
                if (index == -1) index = file.LastIndexOf('/');
                Logger.Error(t.Exception,
                    "Error during task execution. {File}::{Member}:{Line}",
                    file.Substring(index + 1, file.Length - index - 1), member, line);
            }, TaskContinuationOptions.OnlyOnFaulted);
        return task;
    }
}