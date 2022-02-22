namespace Tarefas.db
{
    public partial class Usuario
    {
        public Usuario()
        {
            Tarefa = new HashSet<Tarefa>();
        }

        public string Id { get; set; } = null!;
        public string Nome { get; set; } = null!;
        public string Senha { get; set; } = null!;

        public virtual ICollection<Tarefa> Tarefa { get; set; }
    }
}
