using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DriverRegistration.IntegrationTests;

public sealed class InputBoundaryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(254, HttpStatusCode.Created)]
    [InlineData(255, HttpStatusCode.BadRequest)]
    public async Task EmailLength_UsesLimitAfterTrimming(int length, HttpStatusCode expectedStatus)
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        var email = "a@" + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 63)
            + "." + new string('e', length - 194);
        var payload = DriverApiTests.ValidPayload();
        payload["email"] = "  " + email + "  ";
        using var response = await client.PostAsJsonAsync("/api/v1/drivers", payload, Token);
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    public async Task RepeatedCnhDigits_ReturnValidation(string number)
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        var payload = DriverApiTests.ValidPayload();
        payload["cnhNumber"] = number;
        using var response = await client.PostAsJsonAsync("/api/v1/drivers", payload, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.True(body.GetProperty("errors").TryGetProperty("cnhNumber", out _));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("cpf")]
    [InlineData("phone")]
    [InlineData("email")]
    [InlineData("cnhNumber")]
    [InlineData("cnhCategory")]
    [InlineData("cnhExpirationDate")]
    public async Task NumericInsteadOfExpectedString_ReturnsSafeBindingError(string field)
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        var payload = DriverApiTests.ValidPayload();
        payload[field] = 123;
        using var response = await client.PostAsJsonAsync("/api/v1/drivers", payload, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Equal("validation_error", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
    }
}
