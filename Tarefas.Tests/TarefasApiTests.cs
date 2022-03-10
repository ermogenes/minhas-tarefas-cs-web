/*
Testes unitários de API, usando o pattern AAA.

Estes testes usam um contexto de Sqlite em memória pré-carregado com dados.
São testadas as entradas, os retornos e a autenticação/autorização de acesso das APIs.
Não é testado (diretamente) o funcionamento do repositório.
*/
using Tarefas.db;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Tarefas.Tests;

public class TarefasApiTests
{
  // Inicializa uma fábrica de aplicação que utiliza um contexto de Sqlite em memória com carga inicial
  private readonly TarefasWebAppFactory _appFactory = new TarefasSqliteWebAppFactory();

  [Theory]
  [InlineData("admin", "correto", HttpStatusCode.OK)]
  [InlineData("admin", "incorreto", HttpStatusCode.Unauthorized)]
  [InlineData("usuario", "correto", HttpStatusCode.OK)]
  [InlineData("usuario", "incorreto", HttpStatusCode.Unauthorized)]
  [InlineData("inexistente", "inexistente", HttpStatusCode.Unauthorized)]
  [InlineData("", "", HttpStatusCode.Unauthorized)]
  public async void Get_Login(string usuario, string senha, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = _appFactory.CreateClient();

    // Act
    using var resposta = await cliente.PostAsJsonAsync("/api/login", new DadosLogin(usuario, senha));

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var possivelToken = await resposta.Content.ReadAsStringAsync();

      // O retorno se parece com um token JWT?
      Assert.Equal(3, possivelToken.Split(".").Length);
    }
  }

  [Theory]
  [InlineData("admin", "correto", "admin", HttpStatusCode.OK)]
  [InlineData("admin", "correto", "usuario", HttpStatusCode.OK)]
  [InlineData("admin", "correto", "inexistente", HttpStatusCode.NotFound)]
  [InlineData("admin", "incorreto", "inexistente", HttpStatusCode.Unauthorized)]
  [InlineData("usuario", "correto", "admin", HttpStatusCode.Forbidden)]
  [InlineData("usuario", "correto", "usuario", HttpStatusCode.OK)]
  [InlineData("usuario", "correto", "inexistente", HttpStatusCode.NotFound)]
  [InlineData("usuario", "incorreto", "admin", HttpStatusCode.Unauthorized)]
  [InlineData("inexistente", "inexistente", "admin", HttpStatusCode.Unauthorized)]
  [InlineData("", "", "inexistente", HttpStatusCode.Unauthorized)]
  public async void Get_UsuariosId(string usuario, string senha, string idSolicitado, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.GetAsync($"/api/usuarios/{idSolicitado}");

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var usuarioRetornado = await resposta.Content.ReadFromJsonAsync<Usuario>();

      // É o usuário solicitado?
      Assert.Equal(idSolicitado, usuarioRetornado?.Id);

      // Possui nome?
      Assert.NotEmpty(usuarioRetornado?.Nome);

      // Não trouxe a senha?
      Assert.Empty(usuarioRetornado?.Senha);
    }

  }

  [Theory]
  [InlineData("admin", "correto", HttpStatusCode.OK)]
  [InlineData("admin", "incorreto", HttpStatusCode.Unauthorized)]
  [InlineData("usuario", "correto", HttpStatusCode.Forbidden)]
  [InlineData("usuario", "incorreto", HttpStatusCode.Unauthorized)]
  [InlineData("inexistente", "inexistente", HttpStatusCode.Unauthorized)]
  [InlineData("", "", HttpStatusCode.Unauthorized)]
  public async void Get_Usuarios(string usuario, string senha, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.GetAsync($"/api/usuarios");

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var usuarios = await resposta.Content.ReadFromJsonAsync<List<Usuario>>();

      // 3 usuários?
      Assert.Equal(3, usuarios?.Count);

      // Todos sem senha?
      Assert.True(usuarios?.All(u => u.Senha == ""));
    }
  }

  [Theory]
  [InlineData("admin", "qualquer", "qualquer", "usuario", HttpStatusCode.Conflict)]
  [InlineData("usuariook", "qualquer", "qualquer", "usuario", HttpStatusCode.Created)]
  [InlineData("adminok", "qualquer", "qualquer", "admin", HttpStatusCode.Unauthorized)]
  [InlineData("1usuariook", "qualquer", "qualquer", "usuario", HttpStatusCode.Created)]
  [InlineData("__", "qualquer", "qualquer", "usuario", HttpStatusCode.BadRequest)]
  [InlineData("comcharespecial#", "qualquer", "qualquer", "usuario", HttpStatusCode.BadRequest)]
  [InlineData("________10________20________30________40________50", "qualquer", "qualquer", "usuario", HttpStatusCode.Created)]
  [InlineData("________10________20________30________40________50_", "qualquer", "qualquer", "usuario", HttpStatusCode.BadRequest)]
  [InlineData("semsenha", "qualquer", "", "usuario", HttpStatusCode.BadRequest)]
  public async void Post_Usuarios(string usuario, string nome, string senha, string papel, HttpStatusCode statusEsperado)
  {
    // Arrange
    var usuarioSolicitado = new Usuario
    {
      Id = usuario,
      Nome = nome,
      Senha = senha,
      Papel = papel
    };

    using var cliente = _appFactory.CreateClient();

    // Act
    using var resposta = await cliente.PostAsJsonAsync("/api/usuarios", usuarioSolicitado);

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var usuarioRetornado = await resposta.Content.ReadFromJsonAsync<Usuario?>();

      // É o usuário solicitado?
      Assert.Equal(usuario, usuarioRetornado?.Id);

      // Nome correto?
      Assert.Equal(nome, usuarioRetornado?.Nome);

      // Não retornou a senha?
      Assert.Equal("", usuarioRetornado?.Senha);

      // Papel correto?
      Assert.Equal(papel, usuarioRetornado?.Papel);

      // URL correto?
      Assert.Equal($"/api/usuarios/{usuario}", resposta.Headers?.Location?.ToString());
    }
  }

  [Theory]
  [InlineData("admin", "correto", "usuariook", "qualquer", "qualquer", "usuario", HttpStatusCode.Created)]
  [InlineData("usuario", "correto", "usuariook", "qualquer", "qualquer", "usuario", HttpStatusCode.Created)]
  [InlineData("admin", "correto", "adminok", "qualquer", "qualquer", "admin", HttpStatusCode.Created)]
  [InlineData("usuario", "correto", "adminok", "qualquer", "qualquer", "admin", HttpStatusCode.Forbidden)]
  public async void Post_Usuarios_Autorizacao(string usuario, string senha, string usuarioIdDesejado, string nome, string senhaDesejada, string papel, HttpStatusCode statusEsperado)
  {
    // Arrange
    var usuarioSolicitado = new Usuario
    {
      Id = usuarioIdDesejado,
      Nome = nome,
      Senha = senhaDesejada,
      Papel = papel
    };

    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.PostAsJsonAsync("/api/usuarios", usuarioSolicitado);

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var usuarioRetornado = await resposta.Content.ReadFromJsonAsync<Usuario?>();

      // Usuário certo?
      Assert.Equal(usuarioIdDesejado, usuarioRetornado?.Id);

      // Nome certo?
      Assert.Equal(nome, usuarioRetornado?.Nome);

      // Sem senha?
      Assert.Equal("", usuarioRetornado?.Senha);

      // Papel certo?
      Assert.Equal(papel, usuarioRetornado?.Papel);

      // URL certo?
      Assert.Equal($"/api/usuarios/{usuarioIdDesejado}", resposta?.Headers?.Location?.ToString());
    }
  }

  [Theory]
  [InlineData("admin", "correto", 2, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", 3, HttpStatusCode.OK)]
  [InlineData("outro", "correto", -1, HttpStatusCode.NoContent)]
  [InlineData("inexistente", "inexistente", -1, HttpStatusCode.Unauthorized)]
  public async void Get_Tarefas(string usuario, string senha, int numTarefasEsperado, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.GetAsync($"/api/tarefas");

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode &&
        resposta.StatusCode != HttpStatusCode.NoContent)
    {
      var tarefas = await resposta.Content.ReadFromJsonAsync<List<Tarefa>>();

      // Quantidade certa de tarefas?
      Assert.Equal(numTarefasEsperado, tarefas?.Count);

      // Todas sem senha?
      Assert.True(tarefas?.All(t => t.Usuario.Senha == ""));
    }
  }

  [Theory]
  [InlineData("admin", "correto", null, null, 2, HttpStatusCode.OK)]
  [InlineData("admin", "correto", false, null, 2, HttpStatusCode.OK)]
  [InlineData("admin", "correto", true, null, 1, HttpStatusCode.OK)]
  [InlineData("admin", "correto", null, "a", 2, HttpStatusCode.OK)]
  [InlineData("admin", "correto", null, "2", 1, HttpStatusCode.OK)]
  [InlineData("admin", "correto", null, "x", -1, HttpStatusCode.NoContent)]
  [InlineData("admin", "correto", true, "a", 1, HttpStatusCode.OK)]
  [InlineData("admin", "correto", true, "b", -1, HttpStatusCode.NoContent)]
  [InlineData("admin", "correto", true, "t", 1, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", null, null, 3, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", false, null, 3, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", true, null, 1, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", null, "b", 3, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", null, "2", 1, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", null, "x", -1, HttpStatusCode.NoContent)]
  [InlineData("usuario", "correto", true, "b", 1, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", true, "a", -1, HttpStatusCode.NoContent)]
  [InlineData("usuario", "correto", true, "t", 1, HttpStatusCode.OK)]
  public async void Get_Tarefas_Filtro(string usuario, string senha, bool? somentePendentes, string? descricao, int numTarefasEsperado, HttpStatusCode statusEsperado)
  {
    // Arrange

    // Monta a URL com os parâmetros informados
    string urlBase = "/api/tarefas";
    var queries = new Dictionary<string, string?>();
    queries.Add("somente_pendentes", somentePendentes?.ToString());
    queries.Add("descricao", descricao);
    string url = QueryHelpers.AddQueryString(urlBase, queries);

    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.GetAsync(url);

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode &&
        resposta.StatusCode != HttpStatusCode.NoContent)
    {
      var tarefas = await resposta.Content.ReadFromJsonAsync<List<Tarefa>>();

      // Quantidade certa de tarefas?
      Assert.Equal(numTarefasEsperado, tarefas?.Count);

      // Todas sem senha?
      Assert.True(tarefas?.All(t => t.Usuario.Senha == ""));
    }
  }

  [Theory]
  [InlineData("admin", "correto", 1, HttpStatusCode.OK)]
  [InlineData("admin", "incorreto", -1, HttpStatusCode.Unauthorized)]
  [InlineData("admin", "correto", 100, HttpStatusCode.NotFound)]
  [InlineData("admin", "correto", 3, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", 3, HttpStatusCode.OK)]
  [InlineData("usuario", "incorreto", -1, HttpStatusCode.Unauthorized)]
  [InlineData("usuario", "correto", 100, HttpStatusCode.NotFound)]
  [InlineData("usuario", "correto", 1, HttpStatusCode.Forbidden)]
  [InlineData("inexistente", "inexistente", -1, HttpStatusCode.Unauthorized)]
  public async void Get_TarefasId(string usuario, string senha, int tarefaIdEsperada, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.GetAsync($"/api/tarefas/{tarefaIdEsperada}");

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var tarefa = await resposta.Content.ReadFromJsonAsync<Tarefa>();

      // Tarefa correta?
      Assert.Equal(tarefaIdEsperada, tarefa?.Id);

      // Alguma descrição?
      Assert.NotEmpty(tarefa?.Descricao);

      // É admin, ou o dono da tarefa?
      Assert.True(usuario == "admin" || tarefa?.UsuarioId == usuario);

      // Sem senha?
      Assert.Empty(tarefa?.Usuario.Senha);
    }
  }

  [Theory]
  [InlineData("admin", "correto", "ta3", false, "admin", HttpStatusCode.Created)]
  [InlineData("admin", "correto", "tb4", false, "outro", HttpStatusCode.Forbidden)]
  [InlineData("admin", "correto", "", false, "admin", HttpStatusCode.BadRequest)]
  [InlineData("admin", "correto", "ta3", true, "admin", HttpStatusCode.BadRequest)]
  [InlineData("usuario", "correto", "tb4", false, "usuario", HttpStatusCode.Created)]
  [InlineData("usuario", "correto", "ta3", false, "outro", HttpStatusCode.Forbidden)]
  [InlineData("usuario", "correto", "", false, "usuario", HttpStatusCode.BadRequest)]
  [InlineData("usuario", "correto", "tb4", true, "usuario", HttpStatusCode.BadRequest)]
  [InlineData("inexistente", "inexistente", "qualquer", false, "inexistente", HttpStatusCode.Unauthorized)]
  public async void Post_Tarefas(string usuario, string senha, string descricao, bool concluida, string usuarioIdDesejado, HttpStatusCode statusEsperado)
  {
    // Arrange
    var tarefaSolicitada = new Tarefa
    {
      Id = -999,
      Descricao = descricao,
      Concluida = concluida,
      UsuarioId = usuarioIdDesejado,
    };

    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.PostAsJsonAsync("/api/tarefas", tarefaSolicitada);

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var tarefa = await resposta.Content.ReadFromJsonAsync<Tarefa>();

      // Código certo?
      Assert.Equal(6, tarefa?.Id);

      // Descrição certa?
      Assert.Equal(descricao, tarefa?.Descricao);

      // Não concluída?
      Assert.False(tarefa?.Concluida);

      // Usuário correto?
      Assert.Equal(usuario, tarefa?.UsuarioId);

      // Sem senha?
      Assert.Empty(tarefa?.Usuario.Senha);

      // URL correto?
      Assert.Equal($"/api/tarefas/{tarefa?.Id}", resposta.Headers?.Location?.ToString());
    }
  }

  [Theory]
  [InlineData("admin", "correto", 1, 1, "alterada", true, "admin", HttpStatusCode.OK)]
  [InlineData("admin", "correto", 1, 1, "alterada", true, "outro", HttpStatusCode.BadRequest)]
  [InlineData("admin", "correto", 1, 1, "", true, "admin", HttpStatusCode.BadRequest)]
  [InlineData("admin", "correto", 100, 100, "alterada", true, "admin", HttpStatusCode.NotFound)]
  [InlineData("admin", "correto", 3, 3, "alterada", true, "usuario", HttpStatusCode.OK)]
  [InlineData("usuario", "correto", 3, 3, "alterada", true, "usuario", HttpStatusCode.OK)]
  [InlineData("usuario", "correto", 3, 3, "alterada", true, "outro", HttpStatusCode.BadRequest)]
  [InlineData("usuario", "correto", 3, 3, "", true, "usuario", HttpStatusCode.BadRequest)]
  [InlineData("usuario", "correto", 100, 100, "alterada", true, "usuario", HttpStatusCode.NotFound)]
  [InlineData("usuario", "correto", 1, 1, "alterada", true, "admin", HttpStatusCode.Forbidden)]
  [InlineData("inexistente", "inexistente", -1, -1, "qualquer", true, "inexistente", HttpStatusCode.Unauthorized)]
  public async void Put_TarefasId(string usuario, string senha, int tarefaId, int tarefaIdSolicitada, string descricao, bool concluida, string usuarioId, HttpStatusCode statusEsperado)
  {
    // Arrange
    var tarefaAlterada = new Tarefa
    {
      Id = tarefaIdSolicitada,
      Descricao = descricao,
      Concluida = concluida,
      UsuarioId = usuarioId,
    };

    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.PutAsJsonAsync($"/api/tarefas/{tarefaId}", tarefaAlterada);

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var tarefa = await resposta.Content.ReadFromJsonAsync<Tarefa>();

      // Tarefa certa?
      Assert.Equal(tarefaIdSolicitada, tarefa?.Id);

      // Descrição certa?
      Assert.Equal(descricao, tarefa?.Descricao);

      // Situação correta?
      Assert.Equal(concluida, tarefa?.Concluida);

      // Usuário correto?
      Assert.Equal(usuarioId, tarefa?.UsuarioId);

      // Sem senha?
      Assert.Empty(tarefa?.Usuario.Senha);
    }
  }

  [Theory]
  [InlineData("admin", "correto", 1, HttpStatusCode.OK)]
  [InlineData("admin", "correto", 2, HttpStatusCode.BadRequest)]
  [InlineData("admin", "correto", 3, HttpStatusCode.OK)]
  [InlineData("admin", "correto", 100, HttpStatusCode.NotFound)]
  [InlineData("usuario", "correto", 3, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", 4, HttpStatusCode.BadRequest)]
  [InlineData("usuario", "correto", 1, HttpStatusCode.Forbidden)]
  [InlineData("usuario", "correto", 100, HttpStatusCode.NotFound)]
  [InlineData("inexistente", "inexistente", -1, HttpStatusCode.Unauthorized)]
  public async void Patch_Tarefas(string usuario, string senha, int tarefaId, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.PatchAsync($"/api/tarefas/{tarefaId}/concluir", null);

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);

    if (resposta.IsSuccessStatusCode)
    {
      var tarefa = await resposta.Content.ReadFromJsonAsync<Tarefa>();

      // Tarefa certa?
      Assert.Equal(tarefaId, tarefa?.Id);

      // Concluída?
      Assert.True(tarefa?.Concluida);

      // É admin ou o dono da tarefa?
      Assert.True(usuario == "admin" || tarefa?.UsuarioId == usuario);

      // Sem senha?
      Assert.Empty(tarefa?.Usuario.Senha);
    }
  }

  [Theory]
  [InlineData("admin", "correto", 1, HttpStatusCode.OK)]
  [InlineData("admin", "correto", 3, HttpStatusCode.OK)]
  [InlineData("admin", "correto", 100, HttpStatusCode.NotFound)]
  [InlineData("usuario", "correto", 3, HttpStatusCode.OK)]
  [InlineData("usuario", "correto", 1, HttpStatusCode.Forbidden)]
  [InlineData("usuario", "correto", 100, HttpStatusCode.NotFound)]
  [InlineData("inexistente", "inexistente", -1, HttpStatusCode.Unauthorized)]
  public async void Delete_Tarefas(string usuario, string senha, int tarefaId, HttpStatusCode statusEsperado)
  {
    // Arrange
    using var cliente = await _appFactory.CriaClienteLogado(new DadosLogin(usuario, senha));

    // Act
    using var resposta = await cliente.DeleteAsync($"/api/tarefas/{tarefaId}");

    // Assert

    // Verifica o código HTTP de retorno
    Assert.Equal(statusEsperado, resposta.StatusCode);
  }
}
