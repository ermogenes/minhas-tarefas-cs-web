/*
Define a estrutura abstrata que todas as fábircas de aplicações devem ter.

Permite que crie-se testes que usem diferentes contextos e cargas de dados, por exemplo.

Garante que todas as implementações devam ser um WebApplicationFactory<Program>,
e também conter o método CriaClienteLogado.
*/
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;
using System.Net.Http;

namespace Tarefas.Tests;

abstract class TarefasWebAppFactory : WebApplicationFactory<Program>
{
  public abstract Task<HttpClient> CriaClienteLogado(DadosLogin dadosLogin);
}
