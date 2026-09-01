using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManyControl.Models;
using ManyControl.Services;

namespace ManyControl.ViewModels;

public partial class ExtratoViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly FinanceService _financeService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    public partial DateTime MesReferencia { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial string MesAnoTexto { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal TotalReceitasMes { get; set; }

    [ObservableProperty]
    public partial decimal TotalDespesasMes { get; set; }

    [ObservableProperty]
    public partial bool IsMesAtual { get; set; } = true;

    [ObservableProperty]
    public partial bool IsMesDiferenteDoAtual { get; set; }

    [ObservableProperty]
    public partial decimal SaldoMes { get; set; }

    [ObservableProperty]
    public partial decimal TotalDespesasPagasMes { get; set; }

    [ObservableProperty]
    public partial decimal TotalDespesasPendentesMes { get; set; }

    [ObservableProperty]
    public partial int TotalTransacoesMes { get; set; }

    [ObservableProperty]
    public partial string FiltroTipo { get; set; } = "Todos";

    [ObservableProperty]
    public partial bool IsFiltroTodos { get; set; } = true;

    [ObservableProperty]
    public partial bool IsFiltroReceitas { get; set; }

    [ObservableProperty]
    public partial bool IsFiltroDespesas { get; set; }

    [ObservableProperty]
    public partial bool IsFiltroPendentes { get; set; }

    [ObservableProperty]
    public partial bool IsFiltroPagas { get; set; }

    [ObservableProperty]
    public partial double ProporcaoDespesas { get; set; }

    [ObservableProperty]
    public partial string ProporcaoTexto { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCarregando { get; set; }

    public ObservableCollection<TransacaoItemViewModel> Transacoes { get; } = new();

    // Modal de Edição
    [ObservableProperty]
    public partial bool IsEditModalVisible { get; set; }

    [ObservableProperty]
    public partial string EditModalTitle { get; set; } = "Editar";

    [ObservableProperty]
    public partial string EditDescricao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditValor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime EditData { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial DateTime EditVencimento { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial bool EditRecorrente { get; set; }

    [ObservableProperty]
    public partial bool EditPaga { get; set; }

    [ObservableProperty]
    public partial bool IsEditRecorrenteVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEditVencimentoVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEditPagaVisible { get; set; }

    [ObservableProperty]
    public partial bool EditReceitaRecebida { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEditReceitaRecebidaVisible { get; set; }

    [ObservableProperty]
    public partial decimal TotalReceitasPendentesMes { get; set; }

    private Guid? _editingId;
    private string _editingTipo = string.Empty;

    public ExtratoViewModel(FinanceService financeService, IDialogService dialogService)
    {
        _financeService = financeService;
        _dialogService = dialogService;
        AtualizarTextoMes();
    }

    [RelayCommand]
    public async Task CarregarMesAsync()
    {
        IsCarregando = true;
        try
        {
            AtualizarTextoMes();

            var ano = MesReferencia.Year;
            var mes = MesReferencia.Month;

            // Processa recorrência caso haja despesas a gerar
            await _financeService.ProcessarDespesasRecorrentesAsync(MesReferencia);

            var receitas = await _financeService.GetReceitasPorMesAsync(ano, mes);
            var despesas = await _financeService.GetDespesasPorMesAsync(ano, mes);

            var totalReceitasRecebidas = receitas.Where(r => r.Recebida).Sum(r => r.Valor);
            TotalReceitasPendentesMes = receitas.Where(r => !r.Recebida).Sum(r => r.Valor);
            TotalReceitasMes = totalReceitasRecebidas;
            TotalDespesasMes = despesas.Sum(d => d.Valor);
            TotalDespesasPagasMes = despesas.Where(d => d.Paga).Sum(d => d.Valor);
            TotalDespesasPendentesMes = despesas.Where(d => !d.Paga).Sum(d => d.Valor);
            SaldoMes = TotalReceitasMes - TotalDespesasMes;

            if (TotalReceitasMes > 0)
            {
                var perc = (double)(TotalDespesasMes / TotalReceitasMes);
                ProporcaoDespesas = Math.Clamp(perc, 0.0, 1.0);
                ProporcaoTexto = $"{perc:P0} das receitas comprometidas com despesas";
            }
            else if (TotalDespesasMes > 0)
            {
                ProporcaoDespesas = 1.0;
                ProporcaoTexto = "Despesas realizadas sem receitas registradas";
            }
            else
            {
                ProporcaoDespesas = 0.0;
                ProporcaoTexto = "Nenhuma movimentação registrada no mês";
            }

            var lista = new List<TransacaoItemViewModel>();

            if (FiltroTipo is "Todos" or "Receitas")
            {
                foreach (var r in receitas)
                {
                    lista.Add(new TransacaoItemViewModel
                    {
                        Id = r.Id,
                        Tipo = "Receita",
                        Descricao = r.Descricao,
                        Valor = r.Valor,
                        Data = r.Data,
                        Vencimento = null,
                        Recorrente = false,
                        Paga = false,
                        Recebida = r.Recebida,
                        ValorTexto = $"+ {r.Valor.ToString("C", PtBr)}",
                        ValorCor = r.Recebida ? "#10B981" : "#FBBF24",
                        TipoBadge = r.Recebida ? "✓ Recebida" : "⏳ A receber",
                        TipoBadgeCor = r.Recebida ? "#064E3B" : "#78350F",
                        ReceitaOriginal = r
                    });
                }
            }

            if (FiltroTipo is "Todos" or "Despesas" or "Pendentes" or "Pagas")
            {
                var despesasFiltradas = despesas;
                if (FiltroTipo == "Pendentes")
                {
                    despesasFiltradas = despesas.Where(d => !d.Paga).ToList();
                }
                else if (FiltroTipo == "Pagas")
                {
                    despesasFiltradas = despesas.Where(d => d.Paga).ToList();
                }

                foreach (var d in despesasFiltradas)
                {
                    lista.Add(new TransacaoItemViewModel
                    {
                        Id = d.Id,
                        Tipo = "Despesa",
                        Descricao = d.Descricao,
                        Valor = d.Valor,
                        Data = d.Data,
                        Vencimento = d.Vencimento,
                        Recorrente = d.Recorrente,
                        Paga = d.Paga,
                        ValorTexto = $"- {d.Valor.ToString("C", PtBr)}",
                        ValorCor = "#EF4444", // Vermelho
                        TipoBadge = d.Recorrente ? "Recorrente" : "Despesa",
                        TipoBadgeCor = d.Recorrente ? "#4C1D95" : "#7F1D1D",
                        DespesaOriginal = d
                    });
                }
            }

            Transacoes.Clear();
            foreach (var item in lista.OrderByDescending(x => x.Data).ThenByDescending(x => x.Descricao))
            {
                Transacoes.Add(item);
            }

            TotalTransacoesMes = Transacoes.Count;
        }
        finally
        {
            IsCarregando = false;
        }
    }

    [RelayCommand]
    public async Task MesAnteriorAsync()
    {
        MesReferencia = MesReferencia.AddMonths(-1);
        await CarregarMesAsync();
    }

    [RelayCommand]
    public async Task ProximoMesAsync()
    {
        MesReferencia = MesReferencia.AddMonths(1);
        await CarregarMesAsync();
    }

    [RelayCommand]
    public async Task MesAtualAsync()
    {
        MesReferencia = DateTime.Today;
        await CarregarMesAsync();
    }

    [RelayCommand]
    public async Task MudarFiltroAsync(string tipo)
    {
        FiltroTipo = tipo;
        IsFiltroTodos = tipo == "Todos";
        IsFiltroReceitas = tipo == "Receitas";
        IsFiltroDespesas = tipo == "Despesas";
        IsFiltroPendentes = tipo == "Pendentes";
        IsFiltroPagas = tipo == "Pagas";
        await CarregarMesAsync();
    }

    [RelayCommand]
    public async Task AlternarStatusPagamentoAsync(TransacaoItemViewModel? item)
    {
        if (item is null || item.Tipo != "Despesa")
        {
            return;
        }

        if (!item.Paga)
        {
            var confirmar = await _dialogService.ShowConfirmationAsync(
                "Confirmar Pagamento",
                $"Marcar a despesa '{item.Descricao}' ({item.Valor.ToString("C", PtBr)}) como PAGA?",
                "Sim, marcar como Paga",
                "Cancelar");

            if (confirmar)
            {
                await _financeService.SetDespesaPagaAsync(item.Id, true);
                await CarregarMesAsync();
            }
        }
        else
        {
            var confirmar = await _dialogService.ShowConfirmationAsync(
                "Reabrir Despesa",
                $"Deseja reabrir a despesa '{item.Descricao}' ({item.Valor.ToString("C", PtBr)}) como PENDENTE?",
                "Sim, reabrir",
                "Cancelar");

            if (confirmar)
            {
                await _financeService.SetDespesaPagaAsync(item.Id, false);
                await CarregarMesAsync();
            }
        }
    }

    [RelayCommand]
    public async Task AlternarRecebimentoAsync(TransacaoItemViewModel? item)
    {
        if (item is null || item.ReceitaOriginal is null)
        {
            return;
        }

        await _financeService.MarcarReceitaComoRecebidaAsync(item.ReceitaOriginal.Id, !item.ReceitaOriginal.Recebida);
        await CarregarMesAsync();
    }

    [RelayCommand]
    public void EditarTransacao(TransacaoItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _editingId = item.Id;
        _editingTipo = item.Tipo;
        IsEditModalVisible = true;

        EditDescricao = item.Descricao;
        EditValor = item.Valor.ToString("N2", PtBr);
        EditData = item.Data;

        if (item.Tipo == "Receita")
        {
            EditModalTitle = "Editar receita";
            IsEditRecorrenteVisible = false;
            IsEditVencimentoVisible = false;
            IsEditPagaVisible = false;
            EditPaga = false;
            IsEditReceitaRecebidaVisible = true;
            EditReceitaRecebida = item.Recebida;
        }
        else
        {
            EditModalTitle = "Editar despesa";
            IsEditRecorrenteVisible = true;
            IsEditVencimentoVisible = true;
            IsEditPagaVisible = true;
            IsEditReceitaRecebidaVisible = false;
            EditReceitaRecebida = true;
            EditRecorrente = item.Recorrente;
            EditPaga = item.Paga;
            EditVencimento = item.Vencimento ?? item.Data;
        }
    }

    [RelayCommand]
    public async Task ExcluirTransacaoAsync(TransacaoItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var confirm = await _dialogService.ShowConfirmationAsync(
            $"Excluir {item.Tipo.ToLowerInvariant()}",
            $"Tem certeza que deseja excluir '{item.Descricao}'?",
            "Excluir",
            "Cancelar");

        if (!confirm)
        {
            return;
        }

        if (item.Tipo == "Receita")
        {
            await _financeService.DeleteReceitaAsync(item.Id);
        }
        else
        {
            await _financeService.DeleteDespesaAsync(item.Id);
        }

        if (_editingId == item.Id)
        {
            FecharEdicaoModal();
        }

        await CarregarMesAsync();
    }

    [RelayCommand]
    public async Task SalvarEdicaoModalAsync()
    {
        if (!TryParseMoney(EditValor, out var valor) || valor <= 0)
        {
            await _dialogService.ShowAlertAsync("Atenção", "Informe um valor válido e maior que zero.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditDescricao))
        {
            await _dialogService.ShowAlertAsync("Atenção", "Informe a descrição.", "OK");
            return;
        }

        if (!_editingId.HasValue)
        {
            return;
        }

        if (_editingTipo == "Receita")
        {
            await _financeService.UpdateReceitaAsync(
                _editingId.Value,
                EditDescricao.Trim(),
                valor,
                EditData,
                null,
                EditReceitaRecebida);
        }
        else
        {
            await _financeService.UpdateDespesaAsync(
                _editingId.Value,
                EditDescricao.Trim(),
                valor,
                EditData,
                null,
                EditVencimento,
                EditRecorrente,
                EditPaga);
        }

        FecharEdicaoModal();
        await CarregarMesAsync();
    }

    [RelayCommand]
    public void FecharEdicaoModal()
    {
        _editingId = null;
        _editingTipo = string.Empty;
        IsEditModalVisible = false;
        EditModalTitle = "Editar";
        EditDescricao = string.Empty;
        EditValor = string.Empty;
        EditData = DateTime.Today;
        EditRecorrente = false;
        EditPaga = false;
        EditVencimento = DateTime.Today;
        EditReceitaRecebida = true;
        IsEditRecorrenteVisible = false;
        IsEditVencimentoVisible = false;
        IsEditPagaVisible = false;
        IsEditReceitaRecebidaVisible = false;
    }

    [RelayCommand]
    public async Task ExplicarMetricaAsync(string? metrica)
    {
        switch (metrica?.ToLowerInvariant())
        {
            case "balanco":
                await _dialogService.ShowAlertAsync(
                    "Balanço do Mês",
                    "É a diferença entre o que você recebeu e o que gastou exclusivamente neste mês selecionado.\n\n" +
                    "• Fórmula: Receitas do Mês − Despesas do Mês\n\n" +
                    "Mostra se as suas contas fecharam no positivo ou negativo no período.",
                    "Entendi");
                break;

            case "receitas":
                await _dialogService.ShowAlertAsync(
                    "Entradas do Mês",
                    "Soma bruta de todas as receitas e rendimentos registrados neste mês selecionado.",
                    "Entendi");
                break;

            case "despesas":
                await _dialogService.ShowAlertAsync(
                    "Saídas do Mês",
                    "Soma de todas as contas e despesas registradas para este mês selecionado.",
                    "Entendi");
                break;

            case "pendentes":
                await _dialogService.ShowAlertAsync(
                    "A Pagar (Pendente)",
                    "Total de despesas deste mês que ainda NÃO foram quitadas.\n\n" +
                    "Toque no badge '⏳ PENDENTE' de uma despesa para marcá-la como paga.",
                    "Entendi");
                break;

            case "pagas":
                await _dialogService.ShowAlertAsync(
                    "Já Pago",
                    "Total de contas deste mês que você já quitou e marcou como '✓ PAGA'.",
                    "Entendi");
                break;
        }
    }

    private void AtualizarTextoMes()
    {
        var hoje = DateTime.Today;
        IsMesAtual = MesReferencia.Year == hoje.Year && MesReferencia.Month == hoje.Month;
        IsMesDiferenteDoAtual = !IsMesAtual;

        var nomeMes = MesReferencia.ToString("MMMM", PtBr);
        // Primeira letra maiúscula
        if (nomeMes.Length > 0)
        {
            nomeMes = char.ToUpper(nomeMes[0]) + nomeMes[1..];
        }

        MesAnoTexto = $"{nomeMes} de {MesReferencia.Year}";
    }

    private static bool TryParseMoney(string? value, out decimal amount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            amount = 0m;
            return false;
        }

        var sanitized = value.Trim()
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);

        if (decimal.TryParse(sanitized, NumberStyles.Number, PtBr, out amount))
        {
            return true;
        }

        var normalized = sanitized.Replace(".", string.Empty).Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}

public class TransacaoItemViewModel
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }
    public DateTime? Vencimento { get; set; }
    public bool HasVencimento => Vencimento.HasValue;
    public bool Recorrente { get; set; }
    public bool Paga { get; set; }
    public bool IsDespesa => Tipo == "Despesa";
    public string StatusPagamentoTexto => Paga ? "PAGA" : "PENDENTE";
    public string StatusPagamentoCor => Paga ? "#34D399" : "#FBBF24";
    public string StatusPagamentoFundo => Paga ? "#064E3B" : "#451A03";
    public string StatusPagamentoIcone => Paga ? "✓" : "⏳";
    public bool Recebida { get; set; } = true;
    public bool IsReceita => Tipo == "Receita";
    public string ValorTexto { get; set; } = string.Empty;
    public string ValorCor { get; set; } = "#FFFFFF";
    public string TipoBadge { get; set; } = string.Empty;
    public string TipoBadgeCor { get; set; } = "#374151";
    public Receita? ReceitaOriginal { get; set; }
    public Despesa? DespesaOriginal { get; set; }
}
