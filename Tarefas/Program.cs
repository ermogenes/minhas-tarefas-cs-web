using Tarefas.db;
using Tarefas.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configurações de segurança do ambiente
var ConfiguracoesSeguranca = new
{
  emissor = builder.Configuration["ConfiguracoesJwt:Emissor"],
  audiencia = builder.Configuration["ConfiguracoesJwt:Audiencia"],
  chaveSimetrica = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(builder.Configuration["ConfiguracoesJwt:Chave"])
  ),
  minutosValidade = Convert.ToInt32(builder.Configuration["ConfiguracoesJwt:MinutosValidade"]),
};

// Configuração da conexão com o BD
builder.Services.AddDbContext<tarefasContext>(opt =>
{
  // Recupera a string de conexão do arquivo de configuração appsettings.json
  string connectionString = builder
    .Configuration
    .GetConnectionString("tarefasConnection");

  // Autodetecta a versão do MySQL
  var serverVersion = ServerVersion.AutoDetect(connectionString);

  // Deixa a conexão disponível para ser usada nos endpoints
  opt.UseMySql(connectionString, serverVersion);
});

// Adiciona o repositório como serviço
// Será instanciado usando o contexto injetado
builder.Services.AddScoped<ITarefasRepository, TarefasRepository>();

// Gera a UI do OpenAPI (Swagger)
builder.Services.AddSwaggerGen(opt =>
{
  // Configurações básicas para exibição
  opt.SwaggerDoc("v1", new OpenApiInfo
  {
    Version = "v1",
    Title = "Minhas Tarefas API",
    Description = "Uma API para controle de tarefas, por usuário.",
  });

  // Configura a UI para autenticação
  opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Segurança usando JWT (digite \"Bearer token-recebido-após-login\")",
  });
  opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme,
                }
            },
            new string[] {}
        }
    });
});

// Adiciona o endpoint "/swagger" para a UI do OpenAPI
builder.Services.AddEndpointsApiExplorer();

// Configura autenticação
builder.Services
    // Configura o esquema de autenticação para JWT (Bearer token)
    .AddAuthentication(opt =>
    {
      opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
      opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    // Configura o JWT
    .AddJwtBearer(opt =>
    {
      opt.TokenValidationParameters = new TokenValidationParameters
      {
        // Valida issuer, audience e a chave do issuer, além do tempo de vida
        ValidateIssuer = true,
        ValidIssuer = ConfiguracoesSeguranca.emissor,
        ValidateAudience = true,
        ValidAudience = ConfiguracoesSeguranca.audiencia,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = ConfiguracoesSeguranca.chaveSimetrica,
        ValidateLifetime = true,
      };
    });

// Configura autorização
builder.Services.AddAuthorization(opt =>
{
  // Cria uma política em que o usuário deve estar logado com perfil "admin"
  opt.AddPolicy("SomenteAdmin", policy =>
    policy
      .RequireAuthenticatedUser()
      .RequireRole("admin")
  );
});

// Garante que a conversão para JSON não entrará em loop
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt =>
{
  opt.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

// Recursos desativados em produção
if (app.Environment.IsDevelopment())
{
  // Página de exceção
  app.UseDeveloperExceptionPage();

  // OpenAPI (Swagger)
  app.UseSwagger();
  app.UseSwaggerUI();
}
else
{
  // Em produção, usar um endpoint de erro
  app.UseExceptionHandler("/api/erro");
}

// Serve arquivos estáticos contidos em "./wwwroot"
app.UseDefaultFiles();
app.UseStaticFiles();

// Adiciona middlewares de autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// Cria os endpoints da API

// ------------- Recupera todas as tarefas, por nome, com filtros de pendência
app.MapGet("/api/tarefas", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromQuery(Name = "somente_pendentes")] bool? somentePendentes,
  [FromQuery] string? descricao
) =>
{
  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Busca as tarefas
  var tarefas = repo.ObtemTarefas(descricao, somentePendentes, usuarioId);

  // Retorna 204 caso não possua tarefas
  if (tarefas.Count() == 0)
  {
    return Results.NoContent();
  }

  // Retorna 200, com os dados
  return Results.Ok(tarefas);
})
.RequireAuthorization() // Somente usuários logados
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized);

// ------------- Recupera uma tarefa, pelo id
app.MapGet("/api/tarefas/{id}", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromRoute] int id
) =>
{
  // Busca pelo id indicado
  var tarefa = repo.ObtemTarefaPorId(id);

  // Caso não encontrado, retorna 404
  if (tarefa == null)
  {
    return Results.NotFound();
  }

  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Se não for admin nem for dono da tarefa, retorna 403
  if (!UsuarioEhAdmin(usuarioLogado) && tarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Retorna 200, com os dados
  return Results.Ok(tarefa);
})
.RequireAuthorization() // Somente usuários logados
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// ------------- Adiciona uma tarefa à lista do usuário logado
app.MapPost("/api/tarefas", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromBody] Tarefa novaTarefa
) =>
{
  // Caso não envie descrição, retorna 400
  if (String.IsNullOrEmpty(novaTarefa.Descricao))
  {
    return Results.BadRequest(new { mensagem = "Não é possivel incluir tarefa sem título." });
  }

  // Caso envie tarefa concluída, retorna 400
  if (novaTarefa.Concluida)
  {
    return Results.BadRequest(new { mensagem = "Não é permitido incluir tarefa já concluída." });
  }

  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Se não for o dono indicado na tarefa, retorna 403
  if (novaTarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Cria a tarefa para o usuário logado, com os dados indicados
  var tarefa = new Tarefa
  {
    UsuarioId = usuarioId,
    Descricao = novaTarefa.Descricao,
    Concluida = novaTarefa.Concluida,
  };

  // Grava a tarefa
  var tarefaCriada = repo.AdicionaTarefa(tarefa);

  // URL da tarefa recém criada
  var tarefaUrl = $"/api/tarefas/{tarefaCriada.Id}";

  // Retorna 201, com o URL criado e os dados
  return Results.Created(tarefaUrl, tarefaCriada);
})
.RequireAuthorization() // Somente usuários logados
.Produces<Tarefa>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// ------------- Altera uma tarefa do usuário logado
app.MapPut("/api/tarefas/{id}", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromRoute] int id,
  [FromBody] Tarefa tarefaAlterada
) =>
{
  // Verifica se a tarefa indicada é a mesma da enviada, retornando 400 caso não seja
  if (tarefaAlterada.Id != id)
  {
    return Results.BadRequest(new { mensagem = "Id inconsistente." });
  }

  // Caso não envie descrição, retorna 400
  if (String.IsNullOrEmpty(tarefaAlterada.Descricao))
  {
    return Results.BadRequest(new { mensagem = "Não é permitido deixar uma tarefa sem título." });
  }

  // Busca pelo id indicado
  var tarefa = repo.ObtemTarefaPorId(id);

  // Caso não encontrado, retorna 404
  if (tarefa == null)
  {
    return Results.NotFound();
  }

  // Caso tente alterar o dono da tarefa, retorna 400
  if (tarefaAlterada.UsuarioId != tarefa.UsuarioId)
  {
    return Results.BadRequest(new { mensagem = "Não é permitido alterar o usuário de uma tarefa." });
  }

  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Se não for admin nem for o dono indicado na tarefa, retorna 403
  if (!UsuarioEhAdmin(usuarioLogado) && tarefaAlterada.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Altera a tarefa
  var tarefaRetorno = repo.AlteraTarefa(tarefaAlterada);

  // Retorna 200, com os dados
  return Results.Ok(tarefaRetorno);
})
.RequireAuthorization() // Somente usuários logados
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// ------------- Altera a situação de conclusão de uma tarefa do usuário logado
app.MapMethods("/api/tarefas/{id}/concluir", new[] { "PATCH" }, (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromRoute] int id
) =>
{
  // Busca pelo id indicado
  var tarefa = repo.ObtemTarefaPorId(id);

  // Caso não encontrado, retorna 404
  if (tarefa == null)
  {
    return Results.NotFound();
  }

  // Caso já esteja concluída, retorna 400
  if (tarefa.Concluida)
  {
    return Results.BadRequest(new { mensagem = "Tarefa concluída anteriormente." });
  }

  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Se não for admin nem for dono da tarefa, retorna 403
  if (!UsuarioEhAdmin(usuarioLogado) && tarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Altera a tarefa
  var tarefaAlterada = repo.ConcluiTarefa(id);

  // Retorna 200, com os dados
  return Results.Ok(tarefaAlterada);
})
.RequireAuthorization() // Somente usuários logados
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

// ------------- Exclui uma tarefa do usuário logado
app.MapDelete("/api/tarefas/{id}", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromRoute] int id
) =>
{
  // Busca pelo id indicado
  var tarefa = repo.ObtemTarefaPorId(id);

  // Caso não encontrado, retorna 404
  if (tarefa == null)
  {
    return Results.NotFound();
  }

  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Se não for admin nem for o dono indicado na tarefa, retorna 403
  if (!UsuarioEhAdmin(usuarioLogado) && tarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Exclui a tarefa
  repo.ExcluiTarefa(id);

  // Retorna 200
  return Results.Ok();
})
.RequireAuthorization() // Somente usuários logados
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// ------------- Listagem de usuários
app.MapGet("/api/usuarios", (
  [FromServices] ITarefasRepository repo
) =>
{
  // Busca todos os usuários, exceto campo senha
  var usuarios = repo.ObtemTodosUsuarios();

  // Retira as senhas
  var usuariosSemSenha = usuarios
    .ToList<Usuario>()
    .Select(u =>
      new Usuario { Id = u.Id, Nome = u.Nome, Senha = "", Papel = u.Papel }
    );

  // Retorna 200, com os dados
  return Results.Ok(usuariosSemSenha);
})
.RequireAuthorization("SomenteAdmin") // Somente administradores logados
.Produces<Usuario>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// ------------- Dados de um usuário, pelo id
app.MapGet("/api/usuarios/{id}", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromRoute] string id
) =>
{
  // Busca o usuário indicado, exceto campo senha
  var usuario = repo.ObtemUsuarioPorId(id);

  // Caso não encontrado, retorna 404
  if (usuario == null)
  {
    return Results.NotFound();
  }

  // Recupera o id do usuário logado
  string usuarioId = ObtemIdUsuarioLogado(usuarioLogado);

  // Se não for admin nem o próprio usuário, retorna 403
  if (!UsuarioEhAdmin(usuarioLogado) && usuarioId != id)
  {
    return Results.Forbid();
  }

  // Remove a senha do objeto a retornar
  var usuarioSemSenha = new Usuario
  {
    Id = usuario.Id,
    Nome = usuario.Nome,
    Senha = "",
    Papel = usuario.Papel
  };

  // Retorna 200, com os dados
  return Results.Ok(usuarioSemSenha);
})
.RequireAuthorization() // Somente usuários logados
.Produces<Usuario>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// ------------- Adiciona um novo usuário
app.MapPost("/api/usuarios", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromBody] Usuario novoUsuario
) =>
{
  // Valida o id de usuário (alfanumérico ou _, de tamanho 3 a 50), retorna 400 caso inválido
  novoUsuario.Id = novoUsuario.Id.ToLower();
  if (IdValido(novoUsuario.Id))
  {
    return Results.BadRequest(new { mensagem = "Nome de usuário deve conter somente de 3 a 50 caracteres alfanuméricos ou '_'." });
  }

  // Caso não informe um nome, retorna 400
  if (String.IsNullOrEmpty(novoUsuario.Nome))
  {
    return Results.BadRequest(new { mensagem = "Não é possivel incluir usuário sem nome." });
  }

  // Caso não informe uma senha, retorna 400
  if (String.IsNullOrEmpty(novoUsuario.Senha))
  {
    return Results.BadRequest(new { mensagem = "Não é possivel incluir usuário sem senha." });
  }

  // Verifica se usuário com o id desejado já existe, retorna 409 caso já exista
  if (repo.UsuarioJaCadastrado(novoUsuario.Id))
  {
    return Results.Conflict(new { mensagem = "Nome de usuário não está disponível." });
  }

  // Somente admin pode criar um usuário com papel diferente do padrão
  if (novoUsuario.Papel != "usuario" && !UsuarioEhAdmin(usuarioLogado))
  {
    if (UsuarioEstahAutenticado(usuarioLogado))
    {
      // Se estiver logado, retorna 403
      return Results.Forbid();
    }

    // Se não estiver logado, retorna 401
    return Results.Unauthorized();
  }

  // Cria o usuário, salvando somente um salted-hash da senha
  var usuario = new Usuario
  {
    Id = novoUsuario.Id,
    Nome = novoUsuario.Nome.Trim(),
    Senha = CriaHash(novoUsuario.Senha),
    Papel = novoUsuario.Papel,
  };

  // Adiciona o usuário
  var usuarioCriado = repo.AdicionaUsuario(usuario);

  // Retira a senha
  var usuarioSemSenha = new Usuario
  {
    Id = usuarioCriado.Id,
    Nome = usuarioCriado.Nome,
    Senha = "",
    Papel = usuarioCriado.Papel
  };

  // URL do usuário recém criado
  var usuarioUrl = $"/api/usuarios/{usuario.Id}";

  // Retorna 201, com o URL criado e os dados
  return Results.Created(usuarioUrl, usuarioSemSenha);
})
.AllowAnonymous() // Permite acesso anônimo (não logado)
.Produces<Usuario>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status409Conflict);

// ------------- Efetua login de usuário, com senha
app.MapPost("/api/login", (
  [FromServices] ITarefasRepository repo,
  ClaimsPrincipal usuarioLogado,
  [FromBody] DadosLogin dadosParaLogin
) =>
{
  // Busca pelo usuário
  var usuario = repo.ObtemUsuarioPorId(dadosParaLogin.usuario);

  // Caso não encontrado, retorna 401
  if (usuario is null)
  {
    return Results.Unauthorized();
  }

  // Caso a senha enviada não bata com a senha armazenada para o usuário, retorna 401
  if (!SenhaConfere(usuario.Senha, dadosParaLogin.senha))
  {
    return Results.Unauthorized();
  }

  // Cria o token JWT
  var token = CriaToken(usuario);

  // Retorna 200, com o token
  return Results.Ok(token);
})
.AllowAnonymous() // Permite acesso anônimo (não logado)
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

// Trata exceções em produção com erro 500 (rfc7231)
app.Map("/api/erro", () => Results.Problem());

app.Run();

// Funções auxiliares
bool IdValido(string id) => !Regex.IsMatch(id, "^[a-z0-9_]{3,50}$");

string ObtemIdUsuarioLogado(ClaimsPrincipal usuarioLogado) =>
  usuarioLogado?.Identity?.Name ?? "";

bool UsuarioEstahAutenticado(ClaimsPrincipal usuarioLogado) =>
  usuarioLogado?.Identity?.IsAuthenticated ?? false;

bool UsuarioEhAdmin(ClaimsPrincipal? usuarioLogado) =>
  usuarioLogado?.IsInRole("admin") ?? false;

string CriaHash(string senha) => CryptoHelper.Crypto.HashPassword(senha);

bool SenhaConfere(string hashArmazenado, string senhaInformada) =>
  CryptoHelper.Crypto.VerifyHashedPassword(hashArmazenado, senhaInformada);

string CriaToken(Usuario usuario)
{
  // Cria a lista de claims contendo id do usuário, o nome e o papel
  var afirmacoes = new[]
  {
    new Claim(ClaimTypes.Name, usuario.Id),
    new Claim(ClaimTypes.GivenName, usuario.Nome),
    new Claim(ClaimTypes.Role, usuario.Papel),
  };

  // Define expiração do token em 5 minutos
  var dataHoraExpiracao = DateTime.Now.AddMinutes(ConfiguracoesSeguranca.minutosValidade);

  // Cria mecanismo de credenciais, indicando chave e algoritmo
  var credenciais = new SigningCredentials(
    ConfiguracoesSeguranca.chaveSimetrica,
    SecurityAlgorithms.HmacSha256
  );

  // Cria o token
  var token = new JwtSecurityToken(
    issuer: ConfiguracoesSeguranca.emissor,
    audience: ConfiguracoesSeguranca.audiencia,
    claims: afirmacoes,
    expires: dataHoraExpiracao,
    signingCredentials: credenciais
  );

  // Converte o token JWT para string
  var stringToken = new JwtSecurityTokenHandler().WriteToken(token);

  return stringToken;
}

// Define formato dos dados fornecidos para login
public record DadosLogin(string usuario, string senha);
