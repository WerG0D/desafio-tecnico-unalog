using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DriverRegistration.Application;
using DriverRegistration.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DriverRegistration.IntegrationTests;

public sealed class ProtocolAndLoggingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenApi_DescribesOnlyRegistrationAndRequiredNonNullableFields()
    {
        await using var factory = new DriverApiFactory();
        await using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Token);

        var paths = document.GetProperty("paths");
        Assert.Single(paths.EnumerateObject());
        var route = paths.GetProperty("/api/v1/drivers");
        Assert.Single(route.EnumerateObject());
        var responses = route.GetProperty("post").GetProperty("responses");
        foreach (var status in new[] { "201", "400", "409", "500" })
            Assert.True(responses.TryGetProperty(status, out _));
        var schema = document.GetProperty("components").GetProperty("schemas").GetProperty("RegisterDriverRequest");
        Assert.Equal(7, schema.GetProperty("required").GetArrayLength());
        foreach (var field in schema.GetProperty("properties").EnumerateObject())
            Assert.Equal("string", field.Value.GetProperty("type").GetString());
        Assert.Equal("date", schema.GetProperty("properties").GetProperty("cnhExpirationDate").GetProperty("format").GetString());
        var properties = schema.GetProperty("properties");
        Assert.Contains("200", properties.GetProperty("name").GetProperty("description").GetString()!);
        Assert.Contains("254", properties.GetProperty("email").GetProperty("description").GetString()!);
        Assert.Contains("America/Sao_Paulo", properties.GetProperty("cnhExpirationDate").GetProperty("description").GetString()!);
        foreach (var field in new[] { "cpf", "phone", "cnhNumber", "cnhCategory" })
            Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty(field).GetProperty("pattern").GetString()));
        var categoryPattern = properties.GetProperty("cnhCategory").GetProperty("pattern").GetString()!;
        Assert.Matches(categoryPattern, " ab ");
        Assert.DoesNotMatch(categoryPattern, "F");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Registration_OtherMethodsAreNotExposed(string method)
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/drivers");
        using var response = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("cpf")]
    [InlineData("phone")]
    [InlineData("email")]
    [InlineData("cnhNumber")]
    [InlineData("cnhCategory")]
    public async Task Post_NullStringField_ReturnsValidation(string field)
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        var payload = DriverApiTests.ValidPayload();
        payload[field] = null;
        using var response = await client.PostAsJsonAsync("/api/v1/drivers", payload, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task OpenApi_IsNotExposedOutsideDevelopment()
    {
        await using var factory = new DriverApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Errors_DoNotLogPayloadOrExceptionDetails()
    {
        const string privateValue = "private-value-test-sentinel";
        using var logs = new CapturingLoggerProvider();
        await using var factory = new DriverApiFactory();
        await using var failing = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDriverRepository>();
                services.AddSingleton<IDriverRepository>(new FailingRepository(privateValue));
            });
        });
        using var client = failing.CreateClient();
        var payload = DriverApiTests.ValidPayload();
        payload["name"] = privateValue;

        using var response = await client.PostAsJsonAsync("/api/v1/drivers", payload, Token);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(privateValue, await response.Content.ReadAsStringAsync(Token));
        using var malformed = new StringContent("{\"cpf\":\"" + privateValue + "\",", Encoding.UTF8, "application/json");
        using var badResponse = await client.PostAsync("/api/v1/drivers", malformed, Token);
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
        Assert.Contains(logs.Messages, item => item.Contains("Falha no cadastro.", StringComparison.Ordinal));
        Assert.All(logs.Messages, item =>
        {
            Assert.DoesNotContain(privateValue, item);
            Assert.DoesNotContain("52998224725", item);
            Assert.DoesNotContain("12345678901", item);
        });
    }

    private sealed class FailingRepository(string privateValue) : IDriverRepository
    {
        public Task<bool> AddAsync(Driver driver, CancellationToken cancellationToken) =>
            Task.FromException<bool>(new InvalidOperationException(privateValue));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + exception);
        }
    }
}
