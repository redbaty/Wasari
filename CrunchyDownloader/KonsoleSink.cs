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

        public void Emit(LogEvent logEvent)
        {
            var renderMessage = logEvent.RenderMessage(null);
            Console.WriteLine(renderMessage);
        }
    }
}