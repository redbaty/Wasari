namespace Wasari.App.Abstractions;

public interface IWasariBasicInfo
{
    string? Title { get; }

    int? SeasonNumber { get; }

    int? AbsoluteNumber { get; }
}