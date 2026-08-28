namespace ManyControl.Models;

public class SyncPackage
{
    public DateTime ExportedAtUtc { get; set; }

    public DateTime LastChangedAtUtc { get; set; }

    public List<Categoria> Categorias { get; set; } = [];

    public List<Receita> Receitas { get; set; } = [];

    public List<Despesa> Despesas { get; set; } = [];
}
