using DriverRegistration.Application;

namespace DriverRegistration.Api;

public static class DriverEndpoints
{
    public static void MapDriverRegistration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/drivers", RegisterAsync)
            .WithName("RegisterDriver")
            .WithTags("Motoristas")
            .WithSummary("Realiza o autocadastro de um motorista.")
            .WithDescription("CPF, e-mail e CNH devem ser únicos. A validade deve ser posterior " +
                "à data civil atual de São Paulo. Retorna apenas identificador e mensagem, sem Location.")
            .Accepts<RegisterDriverRequest>("application/json")
            .Produces<RegisterDriverResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterDriverRequest request, RegisterDriver useCase, HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new RegisterDriverCommand(
            request.Name, request.Cpf, request.Phone, request.Email,
            request.CnhNumber, request.CnhCategory, request.CnhExpirationDate), cancellationToken);

        if (result.Errors is not null)
        {
            return Results.ValidationProblem(result.Errors.ToDictionary(pair => pair.Key, pair => pair.Value),
                title: "Dados de cadastro inválidos.",
                extensions: ApiProblems.Extensions("validation_error", context.TraceIdentifier));
        }

        if (result.IsConflict)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Conflito de cadastro.",
                detail: "Já existe um cadastro com um ou mais dos dados únicos informados.",
                extensions: ApiProblems.Extensions("driver_conflict", context.TraceIdentifier));
        }

        return Results.Json(new RegisterDriverResponse(result.DriverId!.Value,
            "Motorista cadastrado com sucesso."), statusCode: StatusCodes.Status201Created);
    }
}

public sealed record RegisterDriverRequest(
    string Name,
    string Cpf,
    string Phone,
    string Email,
    string CnhNumber,
    string CnhCategory,
    DateOnly CnhExpirationDate);

public sealed record RegisterDriverResponse(Guid Id, string Message);
