namespace DriverRegistration.Application;

public sealed record RegisterDriverCommand(
    string? Name,
    string? Cpf,
    string? Phone,
    string? Email,
    string? CnhNumber,
    string? CnhCategory,
    DateOnly CnhExpirationDate);
