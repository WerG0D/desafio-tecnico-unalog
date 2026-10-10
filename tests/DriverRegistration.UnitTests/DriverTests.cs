using DriverRegistration.Domain;

namespace DriverRegistration.UnitTests;

public sealed class DriverTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    [Fact]
    public void Create_ValidFormattedData_NormalizesFields()
    {
        var driver = Driver.Create("  Ana   D'Ávila  ", "529.982.247-25", "+55 (31) 99999-9999",
            "  ANA@EXAMPLE.COM  ", "12345678901", " ab ", Today.AddDays(1), Today);

        Assert.NotEqual(Guid.Empty, driver.Id);
        Assert.Equal("Ana D'Ávila", driver.Name);
        Assert.Equal("52998224725", driver.Cpf);
        Assert.Equal("31999999999", driver.Phone);
        Assert.Equal("ana@example.com", driver.Email);
        Assert.Equal("AB", driver.CnhCategory);
        Assert.Equal(Today.AddDays(1), driver.CnhExpirationDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("11111111111")]
    [InlineData("00000000000")]
    [InlineData("52998224724")]
    [InlineData("5299822472")]
    [InlineData("529982247255")]
    [InlineData("529a98224725")]
    public void Create_InvalidCpf_Rejects(string? cpf) => AssertInvalid("cpf", cpf: cpf);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000a")]
    [InlineData("𠮷")]
    public void Create_EmptyName_Rejects(string? name) => AssertInvalid("name", name: name);

    [Theory]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("ana @example.com")]
    public void Create_InvalidEmail_Rejects(string email) => AssertInvalid("email", email: email);

    [Theory]
    [InlineData("")]
    [InlineData("F")]
    [InlineData("BA")]
    [InlineData("ABC")]
    public void Create_InvalidCategory_Rejects(string category) => AssertInvalid("cnhCategory", category: category);

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    [InlineData("AB")]
    [InlineData("AC")]
    [InlineData("AD")]
    [InlineData("AE")]
    public void Create_AllowedCategory_Accepts(string category)
    {
        var driver = Create(category: category);
        Assert.Equal(category, driver.CnhCategory);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Create_ExpirationTodayOrEarlier_Rejects(int offset)
    {
        var exception = Assert.Throws<DomainValidationException>(() => Create(expiration: Today.AddDays(offset)));
        Assert.Contains("cnhExpirationDate", exception.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00999999999")]
    [InlineData("319999")]
    [InlineData("319999999999")]
    [InlineData("31a999999999")]
    public void Create_InvalidPhone_Rejects(string phone) => AssertInvalid("phone", phone: phone);

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("1234567890a")]
    public void Create_InvalidCnhNumber_Rejects(string number)
    {
        var exception = Assert.Throws<DomainValidationException>(() => Create(cnhNumber: number));
        Assert.Contains("cnhNumber", exception.Errors.Keys);
    }

    [Fact]
    public void Create_MultipleInvalidFields_ReportsAllWithoutInputValues()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Driver.Create(null, null, null, null, null, null, default, Today));
        Assert.Equal(7, exception.Errors.Count);
        Assert.All(exception.Errors.Values, errors => Assert.NotEmpty(errors));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(201)]
    public void Create_NameOutsideLimits_Rejects(int length) => AssertInvalid("name", name: new string('a', length));

    [Theory]
    [InlineData(2)]
    [InlineData(200)]
    public void Create_NameAtLimits_Accepts(int length) => Assert.Equal(length, Create(name: new string('a', length)).Name.Length);

    [Fact]
    public void Create_LandlinePhone_Accepts() => Assert.Equal("3133334444", Create(phone: "(31) 3333-4444").Phone);

    [Fact]
    public void Create_NameWithSupplementaryUnicode_AcceptsTwoCharacters() => Assert.Equal("𠮷田", Create(name: "𠮷田").Name);

    private static void AssertInvalid(string field, string? name = "Ana Silva", string? cpf = "52998224725",
        string email = "ana@example.com", string category = "D", string phone = "31999999999")
    {
        var exception = Assert.Throws<DomainValidationException>(() => Create(name, cpf, phone, email, category));
        Assert.Contains(field, exception.Errors.Keys);
    }

    private static Driver Create(string? name = "Ana Silva", string? cpf = "52998224725", string phone = "31999999999",
        string email = "ana@example.com", string category = "D", DateOnly? expiration = null, string cnhNumber = "12345678901") =>
        Driver.Create(name, cpf, phone, email, cnhNumber, category, expiration ?? Today.AddDays(1), Today);
}
