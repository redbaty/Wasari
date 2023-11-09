using FluentValidation;
using Wasari.Daemon.Models;

namespace Wasari.Daemon.Validators;

public class CheckDirectoryVideoIntegrityRequestValidator : AbstractValidator<CheckDirectoryVideoIntegrityRequest>
{
    public CheckDirectoryVideoIntegrityRequestValidator()
    {
        RuleFor(i => i.Directory)
            .NotEmpty();
    }
}