using Microsoft.EntityFrameworkCore;

namespace Tarefas.db
{
  public partial class tarefasContext : DbContext
  {
    public tarefasContext()
    {
    }

    public tarefasContext(DbContextOptions<tarefasContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Tarefa> Tarefa { get; set; } = null!;
    public virtual DbSet<Usuario> Usuario { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.UseCollation("utf8_general_ci")
          .HasCharSet("utf8");

      modelBuilder.Entity<Tarefa>(entity =>
      {
        entity.ToTable("tarefa");

        entity.HasIndex(e => e.UsuarioId, "fk_tarefa_usuario_idx");

        entity.Property(e => e.Id).HasColumnName("id");

        entity.Property(e => e.Concluida).HasColumnName("concluida");

        entity.Property(e => e.Descricao)
                  .HasMaxLength(200)
                  .HasColumnName("descricao");

        entity.Property(e => e.UsuarioId)
                  .HasMaxLength(200)
                  .HasColumnName("usuario_id");

        entity.HasOne(d => d.Usuario)
                  .WithMany(p => p.Tarefa)
                  .HasForeignKey(d => d.UsuarioId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("fk_tarefa_usuario");
      });

      modelBuilder.Entity<Usuario>(entity =>
      {
        entity.ToTable("usuario");

        entity.Property(e => e.Id)
                  .HasMaxLength(50)
                  .HasColumnName("id");

        entity.Property(e => e.Nome)
                  .HasMaxLength(200)
                  .HasColumnName("nome");

        entity.Property(e => e.Papel)
                  .HasMaxLength(50)
                  .HasColumnName("papel")
                  .HasDefaultValueSql("'usuario'");

        entity.Property(e => e.Senha)
                  .HasMaxLength(256)
                  .HasColumnName("senha");
      });

      OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
  }
}
