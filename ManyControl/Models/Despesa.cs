namespace ManyControl.Models;

public class Despesa
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Descricao { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public DateTime Data { get; set; }

    public DateTime? Vencimento { get; set; }

    public Guid? CategoriaId { get; set; }

    public Categoria? Categoria { get; set; }

    public bool Recorrente { get; set; }
 
    public bool Paga { get; set; }

    public DateTime? DataPagamento { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
