using FluentValidation;
using Wasari.Daemon.Models;
using Wasari.FFmpeg;

// ReSharper disable UnusedType.Global

namespace Wasari.Daemon.Validators;

public class DownloadRequestValidator : AbstractValidator<DownloadRequest>
{
    public DownloadRequestValidator()
    {
        RuleFor(i => i.Url)
            .NotEmpty()
            .Must(i => i.IsAbsoluteUri)
            .WithMessage("Url must be an absolute uri");
        RuleFor(i => i.EpisodeNumber).GreaterThanOrEqualTo(0);
        RuleFor(i => i.SeasonNumber).GreaterThan(0);
        RuleFor(i => i.HevcOptions)
            .Must(i => i is null or
                { Profile: HevcProfile.High, Qmax: null, Qmin: null }
                or { Profile: HevcProfile.Medium, Qmax: null, Qmin: null }
                or { Profile: HevcProfile.Low, Qmax: null, Qmin: null }
                or { Profile: HevcProfile.Custom, Qmax: >= 1 and <= 51, Qmin: >= 1 and <= 51 }
            )
            .WithMessage("Invalid HEVC options");
    }
}