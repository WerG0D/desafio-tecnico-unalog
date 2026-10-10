using DriverRegistration.Api;
using DriverRegistration.Application;
using DriverRegistration.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddSchemaTransformer<RegisterDriverSchemaTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRegistrationDateProvider, RegistrationDateProvider>();
builder.Services.AddScoped<RegisterDriver>();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Drivers")
    ?? throw new InvalidOperationException("A conexão Drivers deve ser configurada."));

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    await ApiProblems.ForStatus(context.Response.StatusCode, context.TraceIdentifier)
        .ExecuteAsync(context);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DriverDbContext>().Database.MigrateAsync();
    }
}

app.MapDriverRegistration();
app.Run();

public partial class Program;
