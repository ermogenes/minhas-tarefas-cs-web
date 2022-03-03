namespace Tarefas.db
{
  public partial class Tarefa
  {
    public int Id { get; set; }
    public string Descricao { get; set; } = null!;
    public bool Concluida { get; set; }
    public string UsuarioId { get; set; } = null!;

    public virtual Usuario Usuario { get; set; } = null!;
  }
}
