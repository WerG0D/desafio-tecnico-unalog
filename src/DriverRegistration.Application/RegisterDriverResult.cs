namespace DriverRegistration.Application;

public sealed record RegisterDriverResult(
    Guid? DriverId,
    IReadOnlyDictionary<string, string[]>? Errors,
    bool IsConflict);
