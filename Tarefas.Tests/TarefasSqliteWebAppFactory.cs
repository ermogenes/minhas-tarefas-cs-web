/*
Cria uma fábrica de aplicações com Sqlite em memória e carga inicial.
*/
using Tarefas.db;
using Tarefas.Repository;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Tarefas.Tests;

class TarefasSqliteWebAppFactory : TarefasWebAppFactory
{
  // Configurações do contexto Sqlite em memória
  private readonly DbContextOptions<tarefasContext> _configContexto;

  public TarefasSqliteWebAppFactory()
  {
    // Cria e abre a conexão
    var conexaoSqliteEmEmemoria = new SqliteConnection("Filename=:memory:");
    conexaoSqliteEmEmemoria.Open();

    // Indica a estrutura contida em tarefasContext
    _configContexto = new DbContextOptionsBuilder<tarefasContext>()
      .UseSqlite(conexaoSqliteEmEmemoria)
      .Options;
  }

  // Este método é chamado ao criar uma instância de teste para a aplicação
  protected override IHost CreateHost(IHostBuilder builder)
  {
    // Adiciona essa configuração à já existente (em Program.cs)
    builder.ConfigureServices(services =>
    {
      // Cria um novo contexto
      var memDB = new tarefasContext(_configContexto);

      // Remove o repositório padrão (que usa o contexto MySQL)
      services.RemoveAll(typeof(ITarefasRepository));

      // Adiciona um novo repositório conectado ao novo contexto (Sqlite)
      services.AddScoped<ITarefasRepository>(repo => new TarefasRepository(memDB));

      // Recria a base em memória a cada execução
      memDB.Database.EnsureDeleted();
      memDB.Database.EnsureCreated();

      // Captura o escopo atual
      using var scope = services.BuildServiceProvider().CreateScope();

      // Recupera o repositório atual
      var memRepo = scope.ServiceProvider.GetRequiredService<ITarefasRepository>();

      // Faz a carga a ser usada nos testes
      memRepo.AdicionaUsuario(new Usuario { Id = "admin", Nome = "Alice", Papel = "admin", Senha = CryptoHelper.Crypto.HashPassword("correto") });
      memRepo.AdicionaUsuario(new Usuario { Id = "usuario", Nome = "Bob", Papel = "usuario", Senha = CryptoHelper.Crypto.HashPassword("correto") });
      memRepo.AdicionaUsuario(new Usuario { Id = "outro", Nome = "Carol", Papel = "usuario", Senha = CryptoHelper.Crypto.HashPassword("correto") });

      memRepo.AdicionaTarefa(new Tarefa { Descricao = "ta1", Concluida = false, UsuarioId = "admin" });
      memRepo.AdicionaTarefa(new Tarefa { Descricao = "ta2", Concluida = true, UsuarioId = "admin" });
      memRepo.AdicionaTarefa(new Tarefa { Descricao = "tb1", Concluida = false, UsuarioId = "usuario" });
      memRepo.AdicionaTarefa(new Tarefa { Descricao = "tb2", Concluida = true, UsuarioId = "usuario" });
      memRepo.AdicionaTarefa(new Tarefa { Descricao = "tb3", Concluida = true, UsuarioId = "usuario" });
    });

    return base.CreateHost(builder);
  }

  // Equivalente a CreateClient, realiza o login e injeta o bearer token (JWT) recebido
  public override async Task<HttpClient> CriaClienteLogado(DadosLogin dadosLogin)
  {
    // Efetua o login
    var cliente = CreateClient();
    using var resposta = await cliente.PostAsJsonAsync("/api/login", dadosLogin);

    if (resposta.IsSuccessStatusCode)
    {
      // Com autorização

      // Tira as aspas da resposta
      var token = (await resposta.Content.ReadAsStringAsync()).Replace("\"", "");

      // Adiciona os headers:
      // Authorization: Bearer <token>
      cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        JwtBearerDefaults.AuthenticationScheme, token
      );
      // Accept: application/json
      cliente.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json")
      );
    }
    else
    {
      // Sem autorização
      cliente.DefaultRequestHeaders.Authorization = null;
    }

    return cliente;
  }
}
