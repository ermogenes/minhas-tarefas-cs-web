# minhas-tarefas-cs-web
Uma aplicação em C# (web) com Minimal APIs, EFCore 6, MySQL e Pomelo

[![Build and test](https://github.com/ermogenes/minhas-tarefas-cs-web/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/ermogenes/minhas-tarefas-cs-web/actions/workflows/build-and-test.yml)

## OpenAPI (Swagger)
Pacote:
```
dotnet add package Swashbuckle.AspNetCore
```

Código:
```cs
builder.Services.AddSwaggerGen();
builder.Services.AddEndpointsApiExplorer();
...
app.UseSwagger();
app.UseSwaggerUI();
```

## _Arquivos estáticos_
Código:
```cs
app.UseDefaultFiles();
app.UseStaticFiles();
```

## EntityFramework Core 6 (MySQL com Pomelo)
Banco: https://github.com/ermogenes/minhas-tarefas-mysql

Para subir o MySQL com Docker:
```
docker run -p 3306:3306 -e MYSQL_ROOT_PASSWORD=1234 mysql:8.0.28
```

Foi utilizada a lib [Pomelo](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql) (`Pomelo.EntityFrameworkCore.MySql`) em vez do Connector/NET oficial da Oracle, devido ao suporte simplificado a diferentes versões do MySQL.

Comandos utilizados para fazer o _scaffolding_:

```
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef

dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Pomelo.EntityFrameworkCore.MySql

dotnet ef dbcontext scaffold "server=localhost;port=3306;uid=root;pwd=1234;database=tarefas" Pomelo.EntityFrameworkCore.MySql -o db -f --no-pluralize
```

String de conexão transferida de `tarefasContext.OnConfiguring` para `appsettings.json`:

```json
"ConnectionStrings": {
    "tarefasConnection": "server=localhost;port=3306;uid=root;pwd=1234;database=tarefas"
}
```

Exemplo:
```cs
...
builder.Services.AddDbContext<tarefasContext>(opt =>
{
    string connectionString = builder.Configuration.GetConnectionString("tarefasConnection");
    var serverVersion = ServerVersion.AutoDetect(connectionString);
    opt.UseMySql(connectionString, serverVersion);
});
...
app.MapGet("/api/tarefas", ([FromServices] tarefasContext _db) =>
{
    return Results.Ok(_db.Tarefa.ToList<Tarefa>());
});
...
```

# Segurança

Hashing de senha:
```
dotnet add package CryptoHelper
```

Autenticação e autorização com JWT:
```
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

# Loop JSON

```cs
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt =>
{
  opt.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
```

# Solução
```
dotnet new sln
```

# Testes
Criar a solução:
```
dotnet new xunit -o Tarefas.Tests
```

Adicionar a referência do projeto nos testes:
```
dotnet add reference ../Tarefas/Tarefas.csproj
```

Adicionar os pacotes:
```
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

Tornar o `Program` do projeto acessível aos testes:
```
  <ItemGroup>
    <InternalsVisibleTo Include="Tarefas.Tests" />
  </ItemGroup>
```

Criar uma instância de `WebApplicationFactory<Program>`. Executando `CreateClient()` você tem acesso a uma instância de `Program`.

É possível sobrescrever `IHost CreateHost(IHostBuilder builder)` e alterar como desejar os objetos injetados.

O exemplo remove o repositório injetado e injeta um novo que usa outro contexto (no app é MySQL, nos testes é Sqlite em memória). Também faz uma carga inicial (que funciona como _Arrange_ dos testes).

Outra estratégia possível é não usar repositório, e reinjetar diretamente um contexto. [Não é recomendado](https://docs.microsoft.com/pt-br/ef/core/testing/choosing-a-testing-strategy) utilizar _In Memory Databases_ para [testes de integração](https://docs.microsoft.com/pt-br/aspnet/core/test/integration-tests?view=aspnetcore-6.0).
