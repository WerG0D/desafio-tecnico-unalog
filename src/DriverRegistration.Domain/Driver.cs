using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace DriverRegistration.Domain;

public sealed class Driver
{
    private static readonly HashSet<string> ValidAreaCodes =
    [
        "11", "12", "13", "14", "15", "16", "17", "18", "19",
        "21", "22", "24", "27", "28", "31", "32", "33", "34", "35", "37", "38",
        "41", "42", "43", "44", "45", "46", "47", "48", "49", "51", "53", "54", "55",
        "61", "62", "63", "64", "65", "66", "67", "68", "69",
        "71", "73", "74", "75", "77", "79", "81", "82", "83", "84", "85", "86", "87", "88", "89",
        "91", "92", "93", "94", "95", "96", "97", "98", "99"
    ];

    private static readonly HashSet<string> ValidCategories =
        ["A", "B", "C", "D", "E", "AB", "AC", "AD", "AE"];

    private Driver() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Cpf { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string CnhNumber { get; private set; } = string.Empty;
    public string CnhCategory { get; private set; } = string.Empty;
    public DateOnly CnhExpirationDate { get; private set; }

    public static Driver Create(
        string? name,
        string? cpf,
        string? phone,
        string? email,
        string? cnhNumber,
        string? cnhCategory,
        DateOnly cnhExpirationDate,
        DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = string.Join(' ', (name ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalizedName.EnumerateRunes().Count() is < 2 or > 200)
        {
            errors["name"] = ["O nome deve conter entre 2 e 200 caracteres."];
        }
        else if (normalizedName.Any(char.IsControl))
        {
            errors["name"] = ["O nome não pode conter caracteres de controle."];
        }

        var normalizedCpf = (cpf ?? string.Empty).Trim();
        if (!Matches(normalizedCpf, @"\A(?:[0-9]{11}|[0-9]{3}\.[0-9]{3}\.[0-9]{3}-[0-9]{2})\z"))
        {
            errors["cpf"] = ["Informe um CPF válido, com 11 dígitos e pontuação opcional."];
        }
        else
        {
            normalizedCpf = normalizedCpf.Replace(".", string.Empty).Replace("-", string.Empty);
            if (!IsValidCpf(normalizedCpf))
            {
                errors["cpf"] = ["Informe um CPF válido."];
            }
        }

        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone is null)
        {
            errors["phone"] = ["Informe um telefone brasileiro válido com DDD, com código +55 opcional."];
        }

        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedEmail.Length is 0 or > 254
            || normalizedEmail.Any(char.IsWhiteSpace)
            || !MailAddress.TryCreate(normalizedEmail, out var parsedEmail)
            || parsedEmail.DisplayName.Length != 0
            || !string.Equals(parsedEmail.Address, normalizedEmail, StringComparison.Ordinal))
        {
            errors["email"] = ["Informe um endereço de e-mail válido com até 254 caracteres."];
        }

        var normalizedCnh = (cnhNumber ?? string.Empty).Trim();
        if (!Matches(normalizedCnh, @"\A[0-9]{11}\z") || HasRepeatedDigits(normalizedCnh))
        {
            errors["cnhNumber"] = ["A CNH deve conter 11 dígitos e não pode ser uma sequência de dígitos iguais."];
        }

        var normalizedCategory = (cnhCategory ?? string.Empty).Trim().ToUpperInvariant();
        if (!ValidCategories.Contains(normalizedCategory))
        {
            errors["cnhCategory"] = ["A categoria da CNH deve ser A, B, C, D, E, AB, AC, AD ou AE."];
        }

        if (cnhExpirationDate <= today)
        {
            errors["cnhExpirationDate"] = ["A validade da CNH deve ser posterior à data atual."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new Driver
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Cpf = normalizedCpf,
            Phone = normalizedPhone!,
            Email = normalizedEmail,
            CnhNumber = normalizedCnh,
            CnhCategory = normalizedCategory,
            CnhExpirationDate = cnhExpirationDate
        };
    }

    private static bool Matches(string value, string pattern) =>
        Regex.IsMatch(value, pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static bool HasRepeatedDigits(string value) =>
        value.Length > 0 && value.All(character => character == value[0]);

    private static bool IsValidCpf(string cpf)
    {
        if (HasRepeatedDigits(cpf))
        {
            return false;
        }

        for (var digitIndex = 9; digitIndex <= 10; digitIndex++)
        {
            var sum = 0;
            for (var index = 0; index < digitIndex; index++)
            {
                sum += (cpf[index] - '0') * (digitIndex + 1 - index);
            }

            var remainder = sum % 11;
            var expected = remainder < 2 ? 0 : 11 - remainder;
            if (cpf[digitIndex] - '0' != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizePhone(string? phone)
    {
        var value = (phone ?? string.Empty).Trim();
        if (!Matches(value, @"\A(?:\+55 *)?(?:[0-9]{2}|\([0-9]{2}\)) *[0-9]{4,5}-?[0-9]{4}\z"))
        {
            return null;
        }

        if (value.StartsWith("+55", StringComparison.Ordinal))
        {
            value = value[3..];
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (!ValidAreaCodes.Contains(digits[..2]))
        {
            return null;
        }

        return (digits.Length == 10 && digits[2] is >= '2' and <= '5')
            || (digits.Length == 11 && digits[2] == '9')
            ? digits
            : null;
    }
}
