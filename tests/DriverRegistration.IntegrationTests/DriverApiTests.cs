using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DriverRegistration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DriverRegistration.IntegrationTests;

public sealed class DriverApiTests
{
    private const string Route = "/api/v1/drivers";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    internal static Dictionary<string, object?> ValidPayload() => new()
    {
        ["name"] = "  Ana   D'Ávila  ", ["cpf"] = "529.982.247-25", ["phone"] = "+55 (31) 99999-9999",
        ["email"] = "  ANA@EXAMPLE.COM  ", ["cnhNumber"] = "12345678901", ["cnhCategory"] = " d ",
        ["cnhExpirationDate"] = "2026-10-11"
    };

    [Fact]
    public async Task Post_Valid_ReturnsMinimalResponseAndPersistsNormalizedData()
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(Route, ValidPayload(), Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Equal(2, body.EnumerateObject().Count());
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DriverDbContext>();
        var driver = await context.Drivers.AsNoTracking().SingleAsync(Token);
        Assert.Equal(id, driver.Id);
        Assert.Equal("Ana D'Ávila", driver.Name);
        Assert.Equal("52998224725", driver.Cpf);
        Assert.Equal("31999999999", driver.Phone);
        Assert.Equal("ana@example.com", driver.Email);
        Assert.Equal("12345678901", driver.CnhNumber);
        Assert.Equal("D", driver.CnhCategory);
        Assert.Equal(new DateOnly(2026, 10, 11), driver.CnhExpirationDate);
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(Token));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Token));
    }

    [Theory]
    [InlineData("cpf", "52998224725")]
    [InlineData("email", "ana@example.com")]
    [InlineData("cnhNumber", "12345678901")]
    public async Task Post_IndependentDuplicate_ReturnsGenericConflict(string field, string duplicate)
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync(Route, ValidPayload(), Token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var payload = ValidPayload();
        payload["cpf"] = "11144477735";
        payload["email"] = "outra@example.com";
        payload["cnhNumber"] = "98765432100";
        payload[field] = duplicate;

        using var response = await client.PostAsJsonAsync(Route, payload, Token);

        await AssertProblem(response, HttpStatusCode.Conflict, "driver_conflict");
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.CountAsync(Token));
    }

    [Theory]
    [InlineData("name", " ")]
    [InlineData("name", "\u0000a")]
    [InlineData("name", "𠮷")]
    [InlineData("cpf", "11111111111")]
    [InlineData("phone", "00999999999")]
    [InlineData("email", "invalido")]
    [InlineData("cnhNumber", "1")]
    [InlineData("cnhCategory", "F")]
    [InlineData("cnhExpirationDate", "2026-10-10")]
    [InlineData("cnhExpirationDate", "2026-10-09")]
    public async Task Post_InvalidField_ReturnsValidationWithoutPersistence(string field, string value)
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        var payload = ValidPayload();
        payload[field] = value;

        using var response = await client.PostAsJsonAsync(Route, payload, Token);

        var body = await AssertProblem(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.ToListAsync(Token));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"cnhExpirationDate\":\"2026-02-30\"}")]
    [InlineData("{\"cnhExpirationDate\":\"2026-10-11T00:00:00Z\"}")]
    [InlineData("{\"cnhExpirationDate\":null}")]
    public async Task Post_InvalidJsonOrMissingFields_ReturnsSafeValidation(string json)
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Route, content, Token);

        var body = await AssertProblem(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Post_MissingBody_ReturnsValidation()
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(Route, null, Token);
        await AssertProblem(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("cpf")]
    [InlineData("phone")]
    [InlineData("email")]
    [InlineData("cnhNumber")]
    [InlineData("cnhCategory")]
    [InlineData("cnhExpirationDate")]
    public async Task Post_MissingRequiredField_ReturnsValidation(string field)
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        var payload = ValidPayload();
        payload.Remove(field);

        using var response = await client.PostAsJsonAsync(Route, payload, Token);

        var body = await AssertProblem(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Post_UnsupportedMediaType_Returns415()
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("texto");
        using var response = await client.PostAsync(Route, content, Token);
        await AssertProblem(response, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
    }

    [Fact]
    public async Task Post_DatabaseUnavailable_ReturnsSafeInternalError()
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(Route, ValidPayload(), Token);

        await AssertProblem(response, HttpStatusCode.InternalServerError, "internal_error");
    }

    [Fact]
    public async Task Post_NameWithSupplementaryUnicode_Persists()
    {
        await using var factory = new DriverApiFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        var payload = ValidPayload();
        payload["name"] = "𠮷田";

        using var response = await client.PostAsJsonAsync(Route, payload, Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var driver = await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Drivers.SingleAsync(Token);
        Assert.Equal("𠮷田", driver.Name);
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("529", text);
        Assert.DoesNotContain("12345678901", text);
        Assert.DoesNotContain("example.com", text);
        Assert.DoesNotContain("SQLite", text);
        Assert.DoesNotContain("Exception", text);
        Assert.DoesNotContain("stackTrace", text);
        var body = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        return body;
    }
}
