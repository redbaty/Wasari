using System;
using Konsole;
using Serilog.Core;
using Serilog.Events;

namespace CrunchyDownloader
{
    internal class KonsoleSink : ILogEventSink
    {
        private IConsole Console { get; }

        public KonsoleSink()
        {
            var width = System.Console.WindowWidth;
            var height = System.Console.WindowHeight / 2 - 1;

            Console = Window.OpenBox("Logs", width, height, new BoxStyle()
            {
                ThickNess = LineThickNess.Single,
                Title = new Colors(ConsoleColor.White, ConsoleColor.Red)
            });
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

        public void Emit(LogEvent logEvent)
        {
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