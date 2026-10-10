using System.Collections.ObjectModel;

namespace DriverRegistration.Domain;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Os dados do motorista são inválidos.")
    {
        Errors = new ReadOnlyDictionary<string, string[]>(
            errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()));
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
