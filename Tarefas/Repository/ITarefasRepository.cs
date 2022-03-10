/*
Interface para os repositórios.

Indica um contrato de API para todos os repositórios seguirem.
*/
using Tarefas.db;

namespace Tarefas.Repository;
public interface ITarefasRepository
{
  Tarefa AdicionaTarefa(Tarefa novaTarefa);

  IEnumerable<Tarefa> ObtemTarefas(string? descricao, bool? somentePendentes, string? usuarioId);

  Tarefa? ObtemTarefaPorId(int id);

  Tarefa AlteraTarefa(Tarefa tarefaAlterada);

  Tarefa ConcluiTarefa(int id);

  void ExcluiTarefa(int id);

  Usuario AdicionaUsuario(Usuario novoUsuario);

  IEnumerable<Usuario> ObtemTodosUsuarios();

  Usuario? ObtemUsuarioPorId(string id);

  bool UsuarioJaCadastrado(string id);
}
