namespace DriverRegistration.Api;

internal static class ApiProblems
{
    public static Dictionary<string, object?> Extensions(string code, string traceId) =>
        new() { ["code"] = code, ["traceId"] = traceId };

    public static IResult ForStatus(int statusCode, string traceId)
    {
        if (statusCode == StatusCodes.Status400BadRequest)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["body"] = ["Informe um objeto JSON válido com os tipos esperados."] },
                title: "Requisição inválida.", extensions: Extensions("validation_error", traceId));
        }

        var (title, code) = statusCode switch
        {
            404 => ("Recurso não encontrado.", "not_found"),
            405 => ("Método não permitido.", "method_not_allowed"),
            413 => ("Requisição muito grande.", "payload_too_large"),
            415 => ("Tipo de conteúdo não suportado. Utilize application/json.", "unsupported_media_type"),
            _ => ("Não foi possível concluir a solicitação.", "internal_error")
        };
        return Results.Problem(statusCode: statusCode, title: title, extensions: Extensions(code, traceId));
    }
}
