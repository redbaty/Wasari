using FluentValidation;
using Wasari.Daemon.Models;

// ReSharper disable UnusedType.Global

namespace Wasari.Daemon.Validators;

public class CheckVideoIntegrityRequestValidator : AbstractValidator<CheckVideoIntegrityRequest>
{
    public CheckVideoIntegrityRequestValidator()
    {
        RuleFor(i => i.Path)
            .NotEmpty();
    }
}