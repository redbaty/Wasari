using System;
using Konsole;
using Microsoft.Extensions.Options;

namespace CrunchyDownloader.App
{
    internal class DownloadProgressManager
    {
        private IConsole Console { get; }

        public DownloadProgressManager(IOptions<ProgressBarOptions> options)
        {
            if (options.Value.Enabled)
            {
                var width = System.Console.WindowWidth;
                var height = System.Console.WindowHeight / 2 - 1;
                Console = Window.OpenBox("Downloads", width, height, new BoxStyle
                {
                    ThickNess = LineThickNess.Single,
                    Title = new Colors(ConsoleColor.White, ConsoleColor.Black)
                });
            }
        }

        public ProgressBar CreateProgressTracker()
        {
            return Console == null
                ? null
                : new ProgressBar(Console, PbStyle.SingleLine, 100, System.Console.WindowWidth / 2);
        }
    }
}