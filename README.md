# Driver Registration API

API REST para autocadastro de motoristas.

A aplicação permite que um motorista cadastre seus dados pessoais e informações da CNH.

## Tecnologias

- .NET 10 / C# 14
- ASP.NET Core Minimal APIs
- Entity Framework Core
- SQLite
- xUnit v3
- OpenAPI

## Estrutura do projeto

O projeto segue os princípios de Clean Architecture, separando as regras de negócio da API e da persistência.

| Projeto | Responsabilidade |
| --- | --- |
| **Domain** | Entidade `Driver`, validações e regras de negócio. |
| **Application** | Caso de uso de cadastro e interfaces de persistência. |
| **Infrastructure** | Acesso ao SQLite com EF Core, repositório e migrations. |
| **Api** | Endpoints HTTP, injeção de dependências e tratamento de erros. |

As dependências seguem em direção ao domínio. A camada de infraestrutura implementa as interfaces definidas pela aplicação.

Optei por manter a estrutura simples, sem repositórios genéricos ou abstrações desnecessárias. Eu preferi usar um modelo Minimal, pq como é apenas um endpoint não fazia sentido o uso de Controller.

## Como executar

### Pré-requisitos

- .NET SDK 10.0.301 ou versão compatível com o `global.json`
- Git

Na raiz do projeto:

```bash
dotnet restore DriverRegistration.slnx
dotnet build DriverRegistration.slnx
dotnet run --project src/DriverRegistration.Api --launch-profile http
```

A API estará disponível em:

`http://localhost:5080`

No ambiente de desenvolvimento, as migrations são aplicadas automaticamente na inicialização. O banco `drivers.db` será criado no diretório de conteúdo da API.

A especificação OpenAPI pode ser acessada em:

`http://localhost:5080/openapi/v1.json`

### Testar pelo Swagger

Na raiz do projeto, inicie a API com o perfil de desenvolvimento:

```bash
dotnet run --project src/DriverRegistration.Api --launch-profile http
```

Mantenha o terminal aberto e acesse [http://localhost:5080/swagger/index.html](http://localhost:5080/swagger/index.html) no navegador. O Swagger está disponível somente no ambiente Development.

1. Expanda **POST /api/v1/drivers**.
2. Clique em **Try it out**.
3. Preencha o JSON usando o exemplo exibido ou dados sintéticos válidos. A validade da CNH deve ser posterior à data atual.
4. Clique em **Execute**.
5. Consulte **Server response** para verificar o código HTTP e o corpo da resposta.

Um cadastro válido retorna **201 Created**. Repetir um CPF, e-mail ou CNH já cadastrado retorna **409 Conflict**. Dados inválidos retornam **400 Bad Request**, com indicação dos campos que precisam ser corrigidos.

Para testar um novo cadastro, utilize outro CPF válido, outro e-mail e outro número de CNH. Os registros permanecem no SQLite após reiniciar a API. Para encerrar a aplicação, pressione **Ctrl+C** no terminal.

### Migrations

O projeto utiliza uma ferramenta local do EF Core, que pode ser restaurada com:

```bash
dotnet tool restore
```

Para aplicar as migrations manualmente:

```bash
dotnet ef database update --project src/DriverRegistration.Infrastructure
```

Em ambientes diferentes de Development, configure `ConnectionStrings__Drivers` com o caminho do banco antes de aplicar as migrations e iniciar a aplicação.

As migrations não são executadas automaticamente em produção.

## Endpoint

### POST /api/v1/drivers

Realiza o cadastro de um motorista.

Exemplo de requisição:

```json
{
  "name": "Motorista de Teste",
  "cpf": "529.982.247-25",
  "phone": "+55 (31) 99999-9999",
  "email": "motorista@example.com",
  "cnhNumber": "12345678901",
  "cnhCategory": "D",
  "cnhExpirationDate": "2029-12-31"
}
```

Resposta de sucesso — `201 Created`:

```json
{
  "id": "34158fb0-3878-4d73-bcfe-a51d785aa9de",
  "message": "Motorista cadastrado com sucesso."
}
```

### Respostas HTTP

| Status | Descrição |
| --- | --- |
| `201` | Motorista cadastrado. |
| `400` | Dados inválidos ou requisição malformada. |
| `409` | CPF, e-mail ou CNH já cadastrados. |
| `415` | Tipo de conteúdo não suportado. |
| `500` | Erro interno. |

Os erros seguem o formato `ProblemDetails`. Nos erros de validação, a resposta identifica os campos inválidos.

O projeto também inclui exemplos de requisição na pasta `examples/`.

## Validações

As validações são aplicadas antes da persistência. Os dados são normalizados para evitar inconsistências entre cadastros.

| Campo | Regra |
| --- | --- |
| Nome | Obrigatório, entre 2 e 200 caracteres. Aceita nomes com acentos e caracteres Unicode. |
| CPF | Validação dos dígitos verificadores e unicidade. |
| Telefone | Número brasileiro válido, com DDD. Aceita telefone fixo e celular. |
| E-mail | Formato válido, normalização e unicidade. |
| CNH | 11 dígitos numéricos e unicidade. |
| Categoria | A, B, C, D, E, AB, AC, AD ou AE. |
| Validade | Deve ser posterior à data atual. |

CPF, telefone, e-mail e categoria são normalizados antes do armazenamento.

As restrições de unicidade também são aplicadas diretamente no SQLite, evitando que cadastros simultâneos gerem registros duplicados.

A validação da CNH considera o formato informado, mas não consulta bases oficiais nem verifica seus dígitos verificadores.

Para verificar a validade da habilitação, a aplicação considera a data civil no fuso `America/Sao_Paulo`.

## Testes

Os testes estão divididos em dois projetos:

- **UnitTests:** regras do domínio, normalização e validações.
- **IntegrationTests:** endpoint HTTP, persistência no SQLite e tratamento de erros.

Para executar todos os testes:

```bash
dotnet test DriverRegistration.slnx -c Release
```

Também é possível executá-los separadamente:

```bash
dotnet test tests/DriverRegistration.UnitTests -c Release
dotnet test tests/DriverRegistration.IntegrationTests -c Release
```

Os testes de integração utilizam SQLite em memória para exercitar o comportamento real do banco, incluindo as restrições de unicidade.
