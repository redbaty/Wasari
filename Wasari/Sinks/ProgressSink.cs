using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Events;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;

namespace Wasari.Sinks;

public class ProgressSink : ILogEventSink
{
    private object ProgressBarLock { get; } = new();

    private Dictionary<string, ProgressBar> ProgressBarsById { get; } = new();

    private List<ProgressBar> ProgressBars { get; } = new();

    public void Emit(LogEvent logEvent)
    {
        lock (ProgressBars)
        {
            if (logEvent.Level == LogEventLevel.Information)
                if (logEvent.MessageTemplate.Text.StartsWith("[Progress Update]"))
                {
                    var update = EmitProgressUpdate(logEvent);
                    DrawProgressBars(true);

                    if (update != null && update.Type != ProgressUpdateTypes.Completed)
                        return;
                }

            ClearProgressBars();

            var level = GetShortLevel(logEvent.Level);
            var renderMessage = logEvent.RenderMessage();
            Console.Write("[");
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = GetColor(logEvent.Level);
            Console.Write(level);
            Console.ForegroundColor = oldColor;
            Console.Write("] ");
            Console.Write(renderMessage);
            Console.Write(Environment.NewLine);
            Console.Write('\r');

            DrawProgressBars();
        }
    }

    private static ConsoleColor GetColor(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => ConsoleColor.DarkGray,
            LogEventLevel.Debug => ConsoleColor.DarkGray,
            LogEventLevel.Information => ConsoleColor.Blue,
            LogEventLevel.Warning => ConsoleColor.Yellow,
            LogEventLevel.Error => ConsoleColor.Red,
            LogEventLevel.Fatal => ConsoleColor.Red,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }

    private static string GetShortLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "VER",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FAT",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }

    private static void ClearCurrentConsoleLine(int? size = null)
    {
        Console.Write('\r');
        Console.Write(new string(' ', size ?? Console.WindowWidth - 1));
        Console.Write('\r');
    }

    public ProgressBar AddProgressBar()
    {
        lock (ProgressBars)
        {
            var progressBar = new ProgressBar
            {
                Max = 100,
                CurrentValue = 0,
                CurrentContainer = GetPosition()
            };

            lock (ProgressBarLock)
            {
                ProgressBars.Add(progressBar);
            }

            return progressBar;
        }
    }

    public void Refresh()
    {
        lock (ProgressBars)
        {
            ClearProgressBars();
            DrawProgressBars();
        }
    }

    private void ClearProgressBars()
    {
        if (ProgressBars.Any(i => i.CurrentContainer.HasValue))
        {
            foreach (var progressBar in ProgressBars.Where(i => i.CurrentContainer.HasValue))
            {
                var container = progressBar.CurrentContainer!.Value;
                Console.SetCursorPosition(0, container.Y);
                Console.Write('\r');
                ClearCurrentConsoleLine();
                Console.Write('\r');
            }

            var min = ProgressBars.Min(i => i.CurrentContainer?.Y ?? 0);
            Console.SetCursorPosition(0, min);
        }
    }

    private void DrawProgressBars(bool resetPosition = false)
    {
        if (resetPosition)
        {
            var min = ProgressBars.Min(i => i.CurrentContainer?.Y ?? 0);
            Console.SetCursorPosition(0, min);
        }

        foreach (var progressBar in ProgressBars)
        {
            var content = progressBar.GenerateContent();

            progressBar.CurrentContainer = GetPosition();

            foreach (var c in content) Console.Write(c);

            var isLast = ProgressBars.IndexOf(progressBar) == ProgressBars.Count - 1;

            if (!isLast) Console.Write(Environment.NewLine);
        }
    }

    private static ProgressBar.Position GetPosition()
    {
        return new ProgressBar.Position
        {
            X = Console.CursorLeft,
            Y = Console.CursorTop
        };
    }

    private void RemoveProgressBar(ProgressBar progressBar)
    {
        lock (ProgressBarLock)
        {
            ClearProgressBars();
            ProgressBars.Remove(progressBar);
            DrawProgressBars();
        }
    }

    private ProgressUpdate EmitProgressUpdate(LogEvent logEvent)
    {
        var episodeId = logEvent.Properties["Id"] is ScalarValue scalarValue ? scalarValue.Value.ToString() : null;

        if (!string.IsNullOrEmpty(episodeId))
        {
            if (!ProgressBarsById.ContainsKey(episodeId)) ProgressBarsById.Add(episodeId, AddProgressBar());

            var progressBar = ProgressBarsById[episodeId];
            var progressUpdate = logEvent.ObjectFromLogEvent<ProgressUpdate>();

            if (progressUpdate.Type == ProgressUpdateTypes.Max) progressBar.Max = progressUpdate.Value;

            if (progressUpdate.Type == ProgressUpdateTypes.Current)
            {
                progressBar.CurrentValue = progressUpdate.Value;
                progressBar.Message = progressUpdate.Title;
            }

            if (progressUpdate.Type == ProgressUpdateTypes.Completed)
            {
                progressBar.CurrentValue = 1;
                progressBar.Max = 1;
                progressBar.Message = progressUpdate.Title;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    RemoveProgressBar(progressBar);
                });
            }

            return progressUpdate;
        }

        return null;
    }
}