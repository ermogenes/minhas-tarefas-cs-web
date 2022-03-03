using Tarefas.db;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

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
        ValidIssuer = builder.Configuration["ConfiguracoesJwt:Emissor"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["ConfiguracoesJwt:Audiencia"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
          Encoding.UTF8.GetBytes(builder.Configuration["ConfiguracoesJwt:Chave"])
        ),
        ValidateLifetime = true,
      };
    });

// Configura autorização
builder.Services.AddAuthorization(opt =>
{
  // Cria uma política em que o usuário deve estar logado com perfil "admin"
  opt.AddPolicy("SomenteAdmin", policy =>
    policy.RequireAuthenticatedUser().RequireRole("admin")
  );
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

// Serve arquivos estáticos contidos em "./wwwroot"
app.UseDefaultFiles();
app.UseStaticFiles();

// Adiciona middlewares de autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// Cria os endpoints da API

////////// Recupera todas as tarefas, por nome, com filtros de pendência
app.MapGet("/api/tarefas", ([FromServices] tarefasContext _db,
  [FromQuery(Name = "somente_pendentes")] bool? somentePendentes,
  [FromQuery] string? descricao,
  HttpContext HttpContext
) =>
{
  // Cria query partindo da tabela Tarefa
  var query = _db.Tarefa.AsQueryable<Tarefa>();

  // Adiciona filtro por descrição caso tenha sido informada
  if (!String.IsNullOrEmpty(descricao))
  {
    query = query.Where(t => t.Descricao.Contains(descricao));
  }

  // Filtro de pendência desativado por padrão
  bool filtrarPendentes = somentePendentes ?? false;

  // Caso tenha sido informado, adiciona filtro de pendência
  // A ordenação não é necessária, foi inserida por ilustração
  if (filtrarPendentes)
  {
    query = query.Where(t => !t.Concluida)
      .OrderByDescending(t => t.Id);
  }

  // Recupera o id do usuário logado
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Busca todas as tarefas do usuário logado, incluindo os filtros
  var tarefas = query
    .Where(t => t.UsuarioId == usuarioId)
    .ToList<Tarefa>();

  // Retorna 204 caso não possua tarefas
  if (tarefas.Count == 0)
  {
    return Results.NoContent();
  }

  // Retorna 200, com os dados
  return Results.Ok(tarefas);
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces<List<Tarefa>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized);

////////// Recupera uma tarefa, pelo id
app.MapGet("/api/tarefas/{id}", ([FromServices] tarefasContext _db,
  [FromRoute] int id,
  HttpContext HttpContext
) =>
{
  // Busca pelo id indicado
  var tarefa = _db.Tarefa.Find(id);

  // Caso não encontrado, retorna 404
  if (tarefa == null)
  {
    return Results.NotFound();
  }

  // Recupera o id do usuário logado
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Se não for admin nem for dono da tarefa, retorna 403
  if (!usuarioAdministrador && tarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Retorna 200, com os dados
  return Results.Ok(tarefa);
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

////////// Adiciona uma tarefa à lista do usuário logado
app.MapPost("/api/tarefas", ([FromServices] tarefasContext _db,
  [FromBody] Tarefa novaTarefa,
  HttpContext HttpContext
) =>
{
  // Caso não envie descrição, retorna 400
  if (String.IsNullOrEmpty(novaTarefa.Descricao))
  {
    return Results.BadRequest(new { mensagem = "Não é possivel incluir tarefa sem título." });
  }

  // Recupera o id do usuário logado
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Se não for admin nem for o dono indicado na tarefa, retorna 403
  if (!usuarioAdministrador && novaTarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Cria a tarefa para o usuário logado, com os dados indicados
  var tarefa = new Tarefa
  {
    Descricao = novaTarefa.Descricao,
    Concluida = novaTarefa.Concluida,
    UsuarioId = usuarioId,
  };

  // Grava a tarefa
  _db.Add(tarefa);
  _db.SaveChanges();

  // URL da tarefa recém criada
  var tarefaUrl = $"/api/tarefas/{tarefa.Id}";

  // Retorna 201, com o URL criado e os dados
  return Results.Created(tarefaUrl, tarefa);
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces<Tarefa>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

////////// Altera uma tarefa do usuário logado
app.MapPut("/api/tarefas/{id}", ([FromServices] tarefasContext _db,
  [FromRoute] int id,
  [FromBody] Tarefa tarefaAlterada,
  HttpContext HttpContext
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
  var tarefa = _db.Tarefa.Find(id);

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
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Se não for admin nem for o dono indicado na tarefa, retorna 403
  if (!usuarioAdministrador && tarefaAlterada.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Altera a tarefa
  tarefa.Descricao = tarefaAlterada.Descricao;
  tarefa.Concluida = tarefaAlterada.Concluida;
  _db.SaveChanges();

  // Retorna 200, com os dados
  return Results.Ok(tarefa);
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

////////// Altera a situação de conclusão de uma tarefa do usuário logado
app.MapMethods("/api/tarefas/{id}/concluir", new[] { "PATCH" }, ([FromServices] tarefasContext _db,
  [FromRoute] int id,
  HttpContext HttpContext
) =>
{
  // Busca pelo id indicado
  var tarefa = _db.Tarefa.Find(id);

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
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Se não for admin nem for dono da tarefa, retorna 403
  if (!usuarioAdministrador && tarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Altera a tarefa
  tarefa.Concluida = true;
  _db.SaveChanges();

  // Retorna 200, com os dados
  return Results.Ok(tarefa);
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces<Tarefa>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

////////// Exclui uma tarefa do usuário logado
app.MapDelete("/api/tarefas/{id}", ([FromServices] tarefasContext _db,
  [FromRoute] int id,
  HttpContext HttpContext
) =>
{
  // Busca pelo id indicado
  var tarefa = _db.Tarefa.Find(id);

  // Caso não encontrado, retorna 404
  if (tarefa == null)
  {
    return Results.NotFound();
  }

  // Recupera o id do usuário logado
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Se não for admin nem for o dono indicado na tarefa, retorna 403
  if (!usuarioAdministrador && tarefa.UsuarioId != usuarioId)
  {
    return Results.Forbid();
  }

  // Exclui a tarefa
  _db.Remove(tarefa);
  _db.SaveChanges();

  // Retorna 200
  return Results.Ok();
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

////////// Listagem de usuários
app.MapGet("/api/usuarios", ([FromServices] tarefasContext _db) =>
{
  // Busca todos os usuários, exceto campo senha
  var usuarios = _db.Usuario
    .Select(u => new { u.Id, u.Nome, u.Papel })
    .ToList();

  // Retorna 200, com os dados
  return Results.Ok(usuarios);
})
// Somente usuários logados e com perfil de administrador
.RequireAuthorization("SomenteAdmin")
// Documentação OpenAPI
.Produces<Usuario>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

////////// Dados de um usuário, pelo id
app.MapGet("/api/usuarios/{id}", ([FromServices] tarefasContext _db,
  [FromRoute] string id,
  HttpContext HttpContext,
  IAuthorizationService auth
) =>
{
  // Busca o usuário indicado, exceto campo senha
  var usuario = _db.Usuario
    .Select(u => new { u.Id, u.Nome, u.Papel })
    .SingleOrDefault(u => u.Id == id);

  // Caso não encontrado, retorna 404
  if (usuario == null)
  {
    return Results.NotFound();
  }

  // Recupera o id do usuário logado
  string usuarioId = HttpContext.User.Claims
    .Single(c => c.Type == ClaimTypes.Name)
    .Value;

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Se não for admin nem o próprio usuário, retorna 403
  if (!usuarioAdministrador && usuarioId != id)
  {
    return Results.Forbid();
  }

  // Retorna 200, com os dados
  return Results.Ok(usuario);
})
// Somente usuários logados, qualquer perfil
.RequireAuthorization()
// Documentação OpenAPI
.Produces<Usuario>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

////////// Adiciona um novo usuário
app.MapPost("/api/usuarios", ([FromServices] tarefasContext _db,
  [FromBody] Usuario novoUsuario,
  HttpContext HttpContext
) =>
{
  // Valida o nome de usuário (alfanumérico ou _, de tamanho 3 a 50), retorna 400 caso inválido
  novoUsuario.Id = novoUsuario.Id.ToLower();
  if (!Regex.IsMatch(novoUsuario.Id, "^[a-z0-9_]{3,50}$"))
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
  var usuarioJaExiste = _db.Usuario.Find(novoUsuario.Id) != null;
  if (usuarioJaExiste)
  {
    return Results.Conflict(new { mensagem = "Nome de usuário não está disponível." });
  }

  // Verifica se usuário logado é administrador
  bool usuarioAdministrador = HttpContext.User.IsInRole("admin");

  // Caso esteja tentando criar um usuário com papel diferente do padrão...
  if (!usuarioAdministrador && novoUsuario.Papel != "usuario")
  {
    // ... se estiver logado, retorna 403
    if (HttpContext.User.Identity?.IsAuthenticated ?? false)
    {
      return Results.Forbid();
    }
    // ... se não estiver logado, retorna 401
    return Results.Unauthorized();
  }

  // Cria o usuário, salvando somente um salted-hash da senha
  var usuario = new Usuario
  {
    Id = novoUsuario.Id,
    Nome = novoUsuario.Nome.Trim(),
    Senha = CryptoHelper.Crypto.HashPassword(novoUsuario.Senha),
    Papel = novoUsuario.Papel,
  };

  // Adiciona o usuário
  _db.Add(usuario);
  _db.SaveChanges();

  // Remove a senha do usuário a ser retornado
  var usuarioSemSenha = new Usuario
  {
    Id = usuario.Id,
    Nome = usuario.Nome,
    Papel = usuario.Papel,
  };

  // URL do usuário recém criado
  var usuarioUrl = $"/api/usuarios/{usuario.Id}";

  // Retorna 201, com o URL criado e os dados
  return Results.Created(usuarioUrl, usuarioSemSenha);
})
// Permite acesso anônimo (não logado)
.AllowAnonymous()
// Documentação OpenAPI
.Produces<Usuario>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status409Conflict);

////////// Efetua login de usuário, com senha
app.MapPost("/api/login", ([FromServices] tarefasContext _db,
  [FromBody] DadosLogin dadosParaLogin
) =>
{
  // Busca pelo usuário
  var usuario = _db.Usuario.SingleOrDefault(u => u.Id == dadosParaLogin.usuario);

  // Caso não encontrado, retorna 401
  if (usuario is null)
  {
    return Results.Unauthorized();
  }

  // Caso a senha enviada não bata com a senha armazenada para o usuário, retorna 401
  if (!CryptoHelper.Crypto.VerifyHashedPassword(usuario.Senha, dadosParaLogin.senha))
  {
    return Results.Unauthorized();
  }

  // A partir daqui, sabemos que o usuário e senha batem

  // Recupera as configurações de emissor e audiência
  var emissor = builder.Configuration["ConfiguracoesJwt:Emissor"];
  var audiencia = builder.Configuration["ConfiguracoesJwt:Audiencia"];

  // Cria a lista de claims contendo id do usuário, o nome e o papel
  var afirmacoes = new[]
  {
    new Claim(ClaimTypes.Name, usuario.Id),
    new Claim(ClaimTypes.GivenName, usuario.Nome),
    new Claim(ClaimTypes.Role, usuario.Papel),
  };

  // Define expiração do token em 5 minutos
  var dataHoraExpiracao = DateTime.Now.AddMinutes(5);

  // Recupera a chave de criptografia do token
  var chaveSimetrica = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(builder.Configuration["ConfiguracoesJwt:Chave"])
  );

  // Cria o mecanismo de criptografia do token
  var credenciais = new SigningCredentials(
    chaveSimetrica,
    SecurityAlgorithms.HmacSha256
  );

  // Aplica as configurações do token
  var tokenDescriptor = new JwtSecurityToken(
    issuer: emissor,
    audience: audiencia,
    claims: afirmacoes,
    expires: dataHoraExpiracao,
    signingCredentials: credenciais
  );

  // Cria o token JWT
  var stringToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

  // Retorna 200, com o token
  return Results.Ok(stringToken);
})
// Permite acesso anônimo (não logado)
.AllowAnonymous()
// Documentação OpenAPI
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

app.Run();

// Define formato dos dados fornecidos para login
public record DadosLogin(string usuario, string senha);
