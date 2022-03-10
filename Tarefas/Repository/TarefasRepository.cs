/*
Implementa o pattern Repository com a interface ITarefasRepository.

Permite que as operações sejam realizadas usando diferentes contextos.
As classes de negócio não devem ter visibilidade do contexto.
Todos os acessos a dados devem ser realizados por essa classe.
*/
using Tarefas.db;

namespace Tarefas.Repository;
public class TarefasRepository : ITarefasRepository
{
  private readonly tarefasContext _db;

  // O único construtor exige a indicação do contexto
  public TarefasRepository(tarefasContext db) => _db = db;

  public Tarefa AdicionaTarefa(Tarefa novaTarefa)
  {
    // Adiciona a tarefa
    _db.Tarefa.Add(novaTarefa);
    _db.SaveChanges();

    // Cria o objeto de retorno, sem senha
    var novaTarefaSemSenha = new Tarefa
    {
      Id = novaTarefa.Id,
      Descricao = novaTarefa.Descricao,
      Concluida = novaTarefa.Concluida,
      UsuarioId = novaTarefa.UsuarioId,
      Usuario = new Usuario
      {
        Id = novaTarefa.Usuario.Id,
        Nome = novaTarefa.Usuario.Nome,
        Senha = "",
        Papel = novaTarefa.Usuario.Papel,
      },
    };

    return novaTarefaSemSenha;
  }

  public IEnumerable<Tarefa> ObtemTarefas(
      string? descricao = null,
      bool? somentePendentes = null,
      string? usuarioId = null
  )
  {
    // Inicia o filtro com todas as tarefas
    var query = _db.Tarefa.AsQueryable<Tarefa>();

    // Adiciona o filtro de descrição
    if (!String.IsNullOrEmpty(descricao))
      query = query.Where(t => t.Descricao.Contains(descricao));

    // Adiciona o filtro de pendentes
    if (somentePendentes ?? false)
      query = query.Where(t => !t.Concluida).OrderByDescending(t => t.Id);

    // Adiciona o filtro de usuário
    if (!String.IsNullOrEmpty(usuarioId))
      query = query.Where(t => t.UsuarioId == usuarioId);

    // Realiza a busca, e não inclui as senhas dos usuários no resultado
    var tarefasSemSenhaDeUsuario = query
      .ToList()
      .Select(t => new Tarefa
      {
        Id = t.Id,
        Descricao = t.Descricao,
        Concluida = t.Concluida,
        UsuarioId = t.UsuarioId,
        Usuario = new Usuario
        {
          Id = t.Usuario.Id,
          Nome = t.Usuario.Nome,
          Senha = "",
          Papel = t.Usuario.Papel,
        },
      });

    return tarefasSemSenhaDeUsuario;
  }

  public Tarefa? ObtemTarefaPorId(int id)
  {
    // Retorna uma única tarefa, ou nenhuma, sem senha do usuário
    return _db.Tarefa
      .Where(t => t.Id == id)
      .Select(t => new Tarefa
      {
        Id = t.Id,
        Descricao = t.Descricao,
        Concluida = t.Concluida,
        UsuarioId = t.UsuarioId,
        Usuario = new Usuario
        {
          Id = t.Usuario.Id,
          Nome = t.Usuario.Nome,
          Senha = "",
          Papel = t.Usuario.Papel,
        },
      })
      .SingleOrDefault<Tarefa>();
  }

  public Tarefa AlteraTarefa(Tarefa tarefaAlterada)
  {
    // Recupera a tarefa
    var tarefa = _db.Tarefa
      .Single(t => t.Id == tarefaAlterada.Id);

    // Altera os campos
    tarefa.Descricao = tarefaAlterada.Descricao;
    tarefa.Concluida = tarefaAlterada.Concluida;
    _db.SaveChanges();

    // Cria o objeto de retorno, sem a senha
    var tarefaSemSenha = new Tarefa
    {
      Id = tarefa.Id,
      Descricao = tarefa.Descricao,
      Concluida = tarefa.Concluida,
      UsuarioId = tarefa.UsuarioId,
      Usuario = new Usuario
      {
        Id = tarefa.Usuario.Id,
        Nome = tarefa.Usuario.Nome,
        Senha = "",
        Papel = tarefa.Usuario.Papel,
      },
    };

    return tarefaSemSenha;
  }

  public Tarefa ConcluiTarefa(int id)
  {
    // Recupera a tarefa
    var tarefa = _db.Tarefa
      .Single(t => t.Id == id);

    // Altera o campo
    tarefa.Concluida = true;
    _db.SaveChanges();

    // Cria o objeto de retorno, sem a senha
    var tarefaSemSenha = new Tarefa
    {
      Id = tarefa.Id,
      Descricao = tarefa.Descricao,
      Concluida = tarefa.Concluida,
      UsuarioId = tarefa.UsuarioId,
      Usuario = new Usuario
      {
        Id = tarefa.Usuario.Id,
        Nome = tarefa.Usuario.Nome,
        Senha = "",
        Papel = tarefa.Usuario.Papel,
      },
    };

    return tarefaSemSenha;
  }

  public void ExcluiTarefa(int id)
  {
    // Recupera a tarefa
    var tarefa = _db.Tarefa
      .Single(t => t.Id == id);

    // Remove a tarefa
    _db.Tarefa.Remove(tarefa);
    _db.SaveChanges();

    return;
  }

  public Usuario AdicionaUsuario(Usuario novoUsuario)
  {
    // Adiciona o usuário
    _db.Usuario.Add(novoUsuario);
    _db.SaveChanges();

    return novoUsuario;
  }

  public IEnumerable<Usuario> ObtemTodosUsuarios()
  {
    // Retorna todos os usuários
    return _db.Usuario.ToList<Usuario>();
  }

  public Usuario? ObtemUsuarioPorId(string id)
  {
    // Retorna o usuário, ou null
    return _db.Usuario
      .SingleOrDefault<Usuario>(u => u.Id == id);
  }

  public bool UsuarioJaCadastrado(string id)
  {
    // Retorna true/false de acordo com a existência do registro
    return _db.Usuario
      .Any<Usuario>(u => u.Id == id);
  }

}
