using System.Text.Json.Nodes;
using DriverRegistration.Application;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DriverRegistration.Api;

public sealed class RegisterDriverSchemaTransformer(IRegistrationDateProvider dateProvider) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(RegisterDriverRequest) || schema.Properties is null)
            return Task.CompletedTask;

        var example = new JsonObject
        {
            ["name"] = "Motorista Exemplo",
            ["cpf"] = "111.444.777-35",
            ["phone"] = "+55 (11) 99999-9999",
            ["email"] = "motorista.exemplo@example.com",
            ["cnhNumber"] = "98765432100",
            ["cnhCategory"] = "D",
            ["cnhExpirationDate"] = dateProvider.Today.AddYears(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        };
        schema.Example = example;

        Describe("name", "Nome entre 2 e 200 caracteres Unicode após normalizar espaços; sem controles.", minLength: 2);
        Describe("cpf", "11 dígitos com verificadores válidos, únicos; aceita máscara e espaços externos.",
            @"^\s*(?:[0-9]{11}|[0-9]{3}\.[0-9]{3}\.[0-9]{3}-[0-9]{2})\s*$", 11);
        Describe("phone", "Telefone brasileiro com DDD válido; fixo de 8 dígitos (2–5) ou celular de 9 (9). " +
            "Aceita +55, parênteses e hífen; normalizado para 10 ou 11 dígitos nacionais.",
            @"^\s*(?:\+55 *)?(?:[0-9]{2}|\([0-9]{2}\)) *[0-9]{4,5}-?[0-9]{4}\s*$", 10);
        Describe("email", "E-mail único, com até 254 caracteres após trim; sem espaços internos ou controles. " +
            "Normalizado em minúsculas; não verifica titularidade ou entrega.", minLength: 3);
        Describe("cnhNumber", "CNH única de 11 dígitos, sem sequências repetidas; não verifica autenticidade. " +
            "Aceita espaços externos.", @"^\s*[0-9]{11}\s*$", 11);
        Describe("cnhCategory", "Categorias permitidas: A, B, C, D, E, AB, AC, AD, AE. " +
            "Aceita minúsculas e espaços externos; normalizada em maiúsculas.",
            @"^\s*(?:[aA][bBcCdDeE]?|[bBcCdDeE])\s*$", 1);
        Describe("cnhExpirationDate", "Data civil yyyy-MM-dd, estritamente posterior ao dia atual de America/Sao_Paulo.");
        return Task.CompletedTask;

        void Describe(string field, string description, string? pattern = null, int? minLength = null)
        {
            if (schema.Properties[field] is OpenApiSchema property)
            {
                property.Description = description;
                property.Pattern = pattern;
                property.MinLength = minLength;
                property.Example = example[field]?.DeepClone();
            }
        }
    }
}
