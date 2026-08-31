using ManyControl.Data;
using ManyControl.Models;
using Microsoft.EntityFrameworkCore;

namespace ManyControl.Services;

public class DatabaseService
{
    private readonly IDbContextFactory<FinanceDbContext> _contextFactory;

    public DatabaseService(IDbContextFactory<FinanceDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task InitializeAsync()
    {
        return InitializeInternalAsync();
    }

    private async Task InitializeInternalAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        await context.Database.EnsureCreatedAsync();
        await EnsureDeletedAtColumnsAsync(context);
        await EnsureDespesaPagamentoColumnsAsync(context);

        if (!await context.Categorias.AnyAsync())
        {
            context.Categorias.AddRange(
                new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nome = "Salário", Tipo = "Receita" },
                new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nome = "Freelance", Tipo = "Receita" },
                new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nome = "Investimentos", Tipo = "Receita" },
                new Categoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Nome = "Alimentação", Tipo = "Despesa" },
                new Categoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Nome = "Transporte", Tipo = "Despesa" },
                new Categoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Nome = "Moradia", Tipo = "Despesa" },
                new Categoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Nome = "Saúde", Tipo = "Despesa" },
                new Categoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Nome = "Assinaturas", Tipo = "Despesa" }
            );

            await context.SaveChangesAsync();
        }

        await DeduplicateCategoriasAsync(context);
    }

    private static async Task DeduplicateCategoriasAsync(FinanceDbContext context)
    {
        var activeCategorias = await context.Categorias
            .Where(c => c.DeletedAt == null)
            .ToListAsync();

        var groups = activeCategorias
            .GroupBy(c => (c.Nome.Trim().ToLowerInvariant(), c.Tipo.Trim().ToLowerInvariant()))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var keep = group.OrderBy(c => c.CreatedAt).First();
            var duplicates = group.Skip(1).ToList();

            foreach (var duplicate in duplicates)
            {
                var despesas = await context.Despesas.Where(d => d.CategoriaId == duplicate.Id).ToListAsync();
                foreach (var d in despesas)
                {
                    d.CategoriaId = keep.Id;
                }

                var receitas = await context.Receitas.Where(r => r.CategoriaId == duplicate.Id).ToListAsync();
                foreach (var r in receitas)
                {
                    r.CategoriaId = keep.Id;
                }

                context.Categorias.Remove(duplicate);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureDeletedAtColumnsAsync(FinanceDbContext context)
    {
        await TryExecuteAsync(context, "ALTER TABLE Categorias ADD COLUMN DeletedAt TEXT NULL");
        await TryExecuteAsync(context, "ALTER TABLE Receitas ADD COLUMN DeletedAt TEXT NULL");
        await TryExecuteAsync(context, "ALTER TABLE Despesas ADD COLUMN DeletedAt TEXT NULL");
    }

    private static async Task EnsureDespesaPagamentoColumnsAsync(FinanceDbContext context)
    {
        await TryExecuteAsync(context, "ALTER TABLE Despesas ADD COLUMN Paga INTEGER NOT NULL DEFAULT 0");
        await TryExecuteAsync(context, "ALTER TABLE Despesas ADD COLUMN DataPagamento TEXT NULL");
    }

    private static async Task TryExecuteAsync(FinanceDbContext context, string sql)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // Coluna já existe. Quando migrarmos para migrations reais, removemos este atalho.
        }
    }
}
