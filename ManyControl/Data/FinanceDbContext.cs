using Microsoft.EntityFrameworkCore;
using ManyControl.Models;

namespace ManyControl.Data;

public class FinanceDbContext : DbContext
{
    private readonly string _dbPath;

    public FinanceDbContext(DbContextOptions<FinanceDbContext> options, string dbPath)
        : base(options)
    {
        _dbPath = dbPath;
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Receita> Receitas => Set<Receita>();
    public DbSet<Despesa> Despesas => Set<Despesa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Nome)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Tipo)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(e => e.CreatedAt)
                .IsRequired();
            entity.Property(e => e.UpdatedAt)
                .IsRequired();
            entity.Property(e => e.DeletedAt);
        });

        modelBuilder.Entity<Receita>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Descricao)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.Valor)
                .HasColumnType("decimal(18,2)");
            entity.Property(e => e.Recebida)
                .IsRequired()
                .HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.DeletedAt);

            entity.HasOne(e => e.Categoria)
                .WithMany()
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Despesa>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Descricao)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.Valor)
                .HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.DeletedAt);

            entity.HasOne(e => e.Categoria)
                .WithMany()
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }
}
