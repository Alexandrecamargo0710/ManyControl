using ManyControl.Data;
using ManyControl.Models;
using Microsoft.EntityFrameworkCore;

namespace ManyControl.Services;

public class FinanceService
{
    private readonly IDbContextFactory<FinanceDbContext> _contextFactory;

    public FinanceService(IDbContextFactory<FinanceDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Categoria>> GetCategoriasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Categorias
            .AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    public async Task<List<Receita>> GetReceitasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Receitas
            .AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .Include(r => r.Categoria)
            .OrderByDescending(r => r.Data)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<Despesa>> GetDespesasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Despesas
            .AsNoTracking()
            .Where(d => d.DeletedAt == null)
            .Include(d => d.Categoria)
            .OrderByDescending(d => d.Data)
            .ThenByDescending(d => d.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<Receita>> GetReceitasPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Receitas
            .AsNoTracking()
            .Where(r => r.DeletedAt == null && r.Data.Year == ano && r.Data.Month == mes)
            .Include(r => r.Categoria)
            .OrderByDescending(r => r.Data)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<Despesa>> GetDespesasPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Despesas
            .AsNoTracking()
            .Where(d => d.DeletedAt == null && d.Data.Year == ano && d.Data.Month == mes)
            .Include(d => d.Categoria)
            .OrderByDescending(d => d.Data)
            .ThenByDescending(d => d.UpdatedAt)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalReceitasPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Receitas
            .Where(r => r.DeletedAt == null && r.Recebida && r.Data.Year == ano && r.Data.Month == mes)
            .SumAsync(r => (decimal?)r.Valor) ?? 0m;
    }

    public async Task<decimal> GetTotalReceitasPendentesPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Receitas
            .Where(r => r.DeletedAt == null && !r.Recebida && r.Data.Year == ano && r.Data.Month == mes)
            .SumAsync(r => (decimal?)r.Valor) ?? 0m;
    }

    public async Task<decimal> GetTotalDespesasPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Despesas
            .Where(d => d.DeletedAt == null && d.Data.Year == ano && d.Data.Month == mes)
            .SumAsync(d => (decimal?)d.Valor) ?? 0m;
    }

    public async Task<int> ProcessarDespesasRecorrentesAsync(DateTime dataReferencia)
    {
        var hoje = DateTime.Today;

        // Limite de segurança: nunca gerar despesas no banco para meses futuros além do mês atual!
        if (dataReferencia.Year > hoje.Year || (dataReferencia.Year == hoje.Year && dataReferencia.Month > hoje.Month))
        {
            return 0;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var anoAlvo = dataReferencia.Year;
        var mesAlvo = dataReferencia.Month;

        var recorrentes = await context.Despesas
            .Where(d => d.DeletedAt == null && d.Recorrente)
            .ToListAsync();

        if (recorrentes.Count == 0)
        {
            return 0;
        }

        // Busca todas as despesas do mês alvo (incluindo deletadas) para respeitar exclusões manuais
        var despesasDoMesAlvo = await context.Despesas
            .Where(d => d.Data.Year == anoAlvo && d.Data.Month == mesAlvo)
            .Select(d => d.Descricao.Trim().ToLower())
            .ToListAsync();

        var grupos = recorrentes
            .GroupBy(d => d.Descricao.Trim().ToLowerInvariant())
            .ToList();

        var novosInseridos = 0;

        foreach (var grupo in grupos)
        {
            var descricaoNorm = grupo.Key;

            // Se já existe uma despesa com esse nome no mês alvo (mesmo se foi deletada), não recria
            if (despesasDoMesAlvo.Contains(descricaoNorm))
            {
                continue;
            }

            var modelo = grupo.OrderByDescending(d => d.Data).First();
            var dataModelo = new DateTime(modelo.Data.Year, modelo.Data.Month, 1);
            var dataAlvoMes = new DateTime(anoAlvo, mesAlvo, 1);

            if (dataModelo >= dataAlvoMes)
            {
                continue;
            }

            var diasNoMes = DateTime.DaysInMonth(anoAlvo, mesAlvo);
            var diaData = Math.Min(modelo.Data.Day, diasNoMes);
            var novaData = new DateTime(anoAlvo, mesAlvo, diaData);

            DateTime? novoVencimento = null;
            if (modelo.Vencimento.HasValue)
            {
                var diaVenc = Math.Min(modelo.Vencimento.Value.Day, diasNoMes);
                novoVencimento = new DateTime(anoAlvo, mesAlvo, diaVenc);
            }

            context.Despesas.Add(new Despesa
            {
                Id = Guid.NewGuid(),
                Descricao = modelo.Descricao,
                Valor = modelo.Valor,
                Data = novaData,
                Vencimento = novoVencimento,
                CategoriaId = modelo.CategoriaId,
                Recorrente = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            novosInseridos++;
        }

        if (novosInseridos > 0)
        {
            await context.SaveChangesAsync();
        }

        return novosInseridos;
    }

    public async Task LimparDespesasRecorrentesFuturasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var hoje = DateTime.Today;
        var proximoMes = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1);

        var futuras = await context.Despesas
            .Where(d => d.Data >= proximoMes && d.Recorrente)
            .ToListAsync();

        if (futuras.Count > 0)
        {
            context.Despesas.RemoveRange(futuras);
            await context.SaveChangesAsync();
        }
    }

    public async Task<decimal> GetTotalReceitasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Receitas
            .Where(r => r.DeletedAt == null && r.Recebida)
            .SumAsync(r => (decimal?)r.Valor) ?? 0m;
    }

    public async Task<decimal> GetTotalReceitasPendentesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Receitas
            .Where(r => r.DeletedAt == null && !r.Recebida)
            .SumAsync(r => (decimal?)r.Valor) ?? 0m;
    }

    public async Task<decimal> GetTotalDespesasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Despesas
            .Where(d => d.DeletedAt == null)
            .SumAsync(d => (decimal?)d.Valor) ?? 0m;
    }

    public async Task<decimal> GetTotalDespesasPagasPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Despesas
            .Where(d => d.DeletedAt == null && d.Data.Year == ano && d.Data.Month == mes && d.Paga)
            .SumAsync(d => (decimal?)d.Valor) ?? 0m;
    }

    public async Task<decimal> GetTotalDespesasPendentesPorMesAsync(int ano, int mes)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Despesas
            .Where(d => d.DeletedAt == null && d.Data.Year == ano && d.Data.Month == mes && !d.Paga)
            .SumAsync(d => (decimal?)d.Valor) ?? 0m;
    }

    public async Task<decimal> GetSaldoAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var receitas = await context.Receitas
            .Where(r => r.DeletedAt == null && r.Recebida)
            .SumAsync(r => (decimal?)r.Valor) ?? 0m;

        var despesas = await context.Despesas
            .Where(d => d.DeletedAt == null)
            .SumAsync(d => (decimal?)d.Valor) ?? 0m;

        return receitas - despesas;
    }

    public async Task<DateTime> GetLastChangedAtUtcAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var categorias = await context.Categorias
            .Select(c => (DateTime?)c.UpdatedAt)
            .MaxAsync() ?? DateTime.MinValue;

        var receitas = await context.Receitas
            .Select(r => (DateTime?)r.UpdatedAt)
            .MaxAsync() ?? DateTime.MinValue;

        var despesas = await context.Despesas
            .Select(d => (DateTime?)d.UpdatedAt)
            .MaxAsync() ?? DateTime.MinValue;

        return new[] { categorias, receitas, despesas }.Max();
    }

    public async Task<Categoria> EnsureCategoriaAsync(string nome, string tipo)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var categoria = await context.Categorias
            .FirstOrDefaultAsync(c => c.Nome == nome && c.Tipo == tipo && c.DeletedAt == null);

        if (categoria != null)
        {
            return categoria;
        }

        categoria = new Categoria
        {
            Nome = nome,
            Tipo = tipo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();
        return categoria;
    }

    public async Task AddReceitaAsync(string descricao, decimal valor, DateTime data, Guid? categoriaId, bool recebida = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.Receitas.Add(new Receita
        {
            Id = Guid.NewGuid(),
            Descricao = descricao,
            Valor = valor,
            Data = data,
            CategoriaId = categoriaId,
            Recebida = recebida,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task UpdateReceitaAsync(Guid id, string descricao, decimal valor, DateTime data, Guid? categoriaId, bool recebida)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var receita = await context.Receitas.FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null);
        if (receita is null)
        {
            return;
        }

        receita.Descricao = descricao;
        receita.Valor = valor;
        receita.Data = data;
        receita.CategoriaId = categoriaId;
        receita.Recebida = recebida;
        receita.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task MarcarReceitaComoRecebidaAsync(Guid id, bool recebida)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var receita = await context.Receitas.FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null);
        if (receita is null)
        {
            return;
        }

        receita.Recebida = recebida;
        receita.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteReceitaAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var receita = await context.Receitas.FirstOrDefaultAsync(r => r.Id == id);
        if (receita is null)
        {
            return;
        }

        receita.DeletedAt = DateTime.UtcNow;
        receita.UpdatedAt = receita.DeletedAt.Value;
        await context.SaveChangesAsync();
    }

    public async Task AddDespesaAsync(string descricao, decimal valor, DateTime data, Guid? categoriaId, DateTime? vencimento, bool recorrente, bool paga = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.Despesas.Add(new Despesa
        {
            Id = Guid.NewGuid(),
            Descricao = descricao,
            Valor = valor,
            Data = data,
            CategoriaId = categoriaId,
            Vencimento = vencimento,
            Recorrente = recorrente,
            Paga = paga,
            DataPagamento = paga ? DateTime.Now : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task UpdateDespesaAsync(Guid id, string descricao, decimal valor, DateTime data, Guid? categoriaId, DateTime? vencimento, bool recorrente, bool? paga = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var despesa = await context.Despesas.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);
        if (despesa is null)
        {
            return;
        }

        despesa.Descricao = descricao;
        despesa.Valor = valor;
        despesa.Data = data;
        despesa.CategoriaId = categoriaId;
        despesa.Vencimento = vencimento;
        despesa.Recorrente = recorrente;
        if (paga.HasValue)
        {
            despesa.Paga = paga.Value;
            despesa.DataPagamento = paga.Value ? (despesa.DataPagamento ?? DateTime.Now) : null;
        }
        despesa.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task SetDespesaPagaAsync(Guid id, bool paga)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var despesa = await context.Despesas.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);
        if (despesa is null)
        {
            return;
        }

        despesa.Paga = paga;
        despesa.DataPagamento = paga ? DateTime.Now : null;
        despesa.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteDespesaAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var despesa = await context.Despesas.FirstOrDefaultAsync(d => d.Id == id);
        if (despesa is null)
        {
            return;
        }

        despesa.DeletedAt = DateTime.UtcNow;
        despesa.UpdatedAt = despesa.DeletedAt.Value;
        await context.SaveChangesAsync();
    }

    public async Task<SyncPackage> CreateSyncPackageAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return new SyncPackage
        {
            ExportedAtUtc = DateTime.UtcNow,
            LastChangedAtUtc = await GetLastChangedAtUtcAsync(),
            Categorias = await context.Categorias.AsNoTracking().ToListAsync(),
            Receitas = await context.Receitas.AsNoTracking().ToListAsync(),
            Despesas = await context.Despesas.AsNoTracking().ToListAsync()
        };
    }

    public async Task<bool> ApplySyncPackageAsync(SyncPackage package)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        bool hasChanges = false;

        foreach (var categoria in package.Categorias)
        {
            if (categoria.DeletedAt != null)
            {
                var existingDeleted = await context.Categorias.FirstOrDefaultAsync(x => x.Id == categoria.Id);
                if (existingDeleted != null && categoria.UpdatedAt > existingDeleted.UpdatedAt)
                {
                    existingDeleted.DeletedAt = categoria.DeletedAt;
                    existingDeleted.UpdatedAt = categoria.UpdatedAt;
                    hasChanges = true;
                }
                continue;
            }

            var existing = await context.Categorias.FirstOrDefaultAsync(x =>
                x.Id == categoria.Id ||
                (x.Nome.ToLower() == categoria.Nome.ToLower() && x.Tipo.ToLower() == categoria.Tipo.ToLower() && x.DeletedAt == null));

            if (existing is null)
            {
                context.Categorias.Add(categoria);
                hasChanges = true;
                continue;
            }

            if (existing.Id != categoria.Id)
            {
                foreach (var r in package.Receitas.Where(r => r.CategoriaId == categoria.Id))
                {
                    r.CategoriaId = existing.Id;
                }
                foreach (var d in package.Despesas.Where(d => d.CategoriaId == categoria.Id))
                {
                    d.CategoriaId = existing.Id;
                }
            }

            if (categoria.UpdatedAt > existing.UpdatedAt)
            {
                existing.Nome = categoria.Nome;
                existing.Tipo = categoria.Tipo;
                existing.UpdatedAt = categoria.UpdatedAt;
                hasChanges = true;
            }
        }

        await context.SaveChangesAsync();

        foreach (var receita in package.Receitas)
        {
            var existing = await context.Receitas.FirstOrDefaultAsync(x => x.Id == receita.Id);
            if (existing is null)
            {
                context.Receitas.Add(receita);
                hasChanges = true;
                continue;
            }

            if (receita.UpdatedAt > existing.UpdatedAt)
            {
                context.Entry(existing).CurrentValues.SetValues(receita);
                hasChanges = true;
            }
        }

        foreach (var despesa in package.Despesas)
        {
            var existing = await context.Despesas.FirstOrDefaultAsync(x => x.Id == despesa.Id);
            if (existing is null)
            {
                context.Despesas.Add(despesa);
                hasChanges = true;
                continue;
            }

            if (despesa.UpdatedAt > existing.UpdatedAt)
            {
                context.Entry(existing).CurrentValues.SetValues(despesa);
                hasChanges = true;
            }
        }

        await context.SaveChangesAsync();
        return hasChanges;
    }

    public async Task DeduplicateCategoriasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var activeCategorias = await context.Categorias
            .Where(c => c.DeletedAt == null)
            .ToListAsync();

        var groups = activeCategorias
            .GroupBy(c => (c.Nome.Trim().ToLowerInvariant(), c.Tipo.Trim().ToLowerInvariant()))
            .Where(g => g.Count() > 1);

        bool changes = false;
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
                changes = true;
            }
        }

        if (changes)
        {
            await context.SaveChangesAsync();
        }
    }
}
