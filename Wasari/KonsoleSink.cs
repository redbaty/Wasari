using System;
using System.Collections.Generic;
using System.Linq;
using Konsole;
using Serilog.Core;
using Serilog.Events;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.App;

namespace Wasari
{
    internal class KonsoleSink : ILogEventSink
    {
        private IConsole Console { get; }

        private IConsole ProgressBox { get; }

        private Dictionary<string, ProgressBar> ProgressBars { get; } = new();

        private HashSet<ProgressBar> FinishedProgressBars { get; } = new();

        private int OriginalBoxHeight { get; }

        public static int AvailableHeight => System.Console.WindowHeight - System.Console.CursorTop;

        public KonsoleSink()
        {
            var width = System.Console.WindowWidth;
            var height = AvailableHeight / 2 - 1;
            var console = Window.Open();

            Console = console.OpenBox("Logs", width, height, new BoxStyle
            {
                ThickNess = LineThickNess.Single,
                Title = new Colors(ConsoleColor.White, ConsoleColor.Red)
            });

            ProgressBox = console.OpenBox("Downloads", width, height, new BoxStyle
            {
                ThickNess = LineThickNess.Single,
                Title = new Colors(ConsoleColor.White, ConsoleColor.Black)
            });

            OriginalBoxHeight = ProgressBox.WindowHeight - 1;
        }

        private static ConsoleColor GetColor(LogEventLevel level) =>
            level switch
            {
                LogEventLevel.Verbose => ConsoleColor.DarkGray,
                LogEventLevel.Debug => ConsoleColor.DarkGray,
                LogEventLevel.Information => ConsoleColor.Blue,
                LogEventLevel.Warning => ConsoleColor.Yellow,
                LogEventLevel.Error => ConsoleColor.Red,
                LogEventLevel.Fatal => ConsoleColor.Red,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
            };

        private static string GetShortLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => "VER",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FAT",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };

        private ProgressUpdate EmitProgressUpdate(LogEvent logEvent)
        {
            var episodeId = logEvent.Properties["Id"] is ScalarValue scalarValue ? scalarValue.Value.ToString() : null;

            if (!string.IsNullOrEmpty(episodeId))
            {
                if (!ProgressBars.ContainsKey(episodeId))
                {
                    if (ProgressBars.Count >= OriginalBoxHeight && FinishedProgressBars.Any())
                    {
                        var (key, finishedProgressBar) = ProgressBars.FirstOrDefault(i =>
                            i.Value.Max == 1 && FinishedProgressBars.Contains(i.Value));
                        ProgressBars.Remove(key);
                        FinishedProgressBars.Remove(finishedProgressBar);

                        var oldPosition = ProgressBox.CursorTop;
                        ProgressBox.CursorTop = finishedProgressBar.Y;
                        ProgressBars.Add(episodeId,
                            new ProgressBar(ProgressBox, PbStyle.SingleLine, 100, System.Console.WindowWidth / 2));
                        ProgressBox.CursorTop = oldPosition;
                    }
                    else
                    {
                        ProgressBars.Add(episodeId,
                            new ProgressBar(ProgressBox, PbStyle.SingleLine, 100, System.Console.WindowWidth / 2));
                    }
                }

                var progressBar = ProgressBars[episodeId];
                var progressUpdate = logEvent.ObjectFromLogEvent<ProgressUpdate>();

                if (progressUpdate.Type == ProgressUpdateTypes.Max)
                {
                    progressBar.Max = progressUpdate.Value;
                }

                if (progressUpdate.Type == ProgressUpdateTypes.Current)
                {
                    progressBar.Refresh(progressUpdate.Value, progressUpdate.Title);
                }

                if (progressUpdate.Type == ProgressUpdateTypes.Completed)
                {
                    progressBar.Refresh(0, progressUpdate.Title);
                    progressBar.Max = 1;
                    progressBar.Refresh(1, progressUpdate.Title);
                    FinishedProgressBars.Add(progressBar);
                }

                return progressUpdate;
            }

            return null;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level == LogEventLevel.Information)
            {
                if (logEvent.MessageTemplate.Text.StartsWith("[Progress Update]"))
                {
                    var update = EmitProgressUpdate(logEvent);

                    if (update != null && update.Type != ProgressUpdateTypes.Completed)
                        return;
                }
            }

            var level = GetShortLevel(logEvent.Level);
            var renderMessage = logEvent.RenderMessage();
            Console.Write("[");
            Console.Write(GetColor(logEvent.Level), level);
            Console.Write("] ");
            Console.Write(renderMessage);
            Console.Write(Environment.NewLine);
        }
    }
}