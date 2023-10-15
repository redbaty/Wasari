using FluentValidation;
using Wasari.Daemon.Models;

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
    }
}