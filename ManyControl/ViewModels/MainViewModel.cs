using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManyControl.Models;
using ManyControl.Services;

namespace ManyControl.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly FinanceService _financeService;
    private readonly SyncService _syncService;
    private readonly UpdateService _updateService;
    private readonly IDialogService _dialogService;
    private static bool _hasCheckedUpdateOnStartup = false;

    // Métricas do Dashboard Geral
    [ObservableProperty]
    public partial decimal Saldo { get; set; }

    [ObservableProperty]
    public partial decimal TotalReceitas { get; set; }

    [ObservableProperty]
    public partial decimal TotalDespesas { get; set; }

    [ObservableProperty]
    public partial int TotalCategorias { get; set; }

    // Filtro e Métricas do Mês Selecionado
    [ObservableProperty]
    public partial DateTime DataReferencia { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial string MesAnoTexto { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMesAtual { get; set; } = true;

    [ObservableProperty]
    public partial bool IsMesDiferenteDoAtual { get; set; }

    [ObservableProperty]
    public partial decimal ReceitasMes { get; set; }

    [ObservableProperty]
    public partial decimal DespesasMes { get; set; }

    [ObservableProperty]
    public partial decimal BalancoMes { get; set; }

    [ObservableProperty]
    public partial decimal DespesasPagasMes { get; set; }

    [ObservableProperty]
    public partial decimal DespesasPendentesMes { get; set; }

    // Sincronização
    [ObservableProperty]
    public partial string LastSyncText { get; set; } = "Nunca sincronizado";

    [ObservableProperty]
    public partial string LastSyncModeText { get; set; } = "Última ação: nenhuma";

    [ObservableProperty]
    public partial string SyncStatusText { get; set; } = "Tudo sincronizado";

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial bool IsDownloadingUpdate { get; set; }

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    [ObservableProperty]
    public partial string UpdateProgressText { get; set; } = string.Empty;

    // Listas Recentes do Mês
    public ObservableCollection<Receita> ReceitasRecentes { get; } = new();
    public ObservableCollection<Despesa> DespesasRecentes { get; } = new();

    // Formulário Nova Receita
    [ObservableProperty]
    public partial string ReceitaDescricao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReceitaValor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime ReceitaData { get; set; } = DateTime.Today;

    // Formulário Nova Despesa
    [ObservableProperty]
    public partial string DespesaDescricao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DespesaValor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime DespesaData { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial DateTime DespesaVencimento { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial bool DespesaRecorrente { get; set; }

    [ObservableProperty]
    public partial bool DespesaPaga { get; set; }

    // Modal de Edição (Overlay)
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

    private Guid? _editingReceitaId;
    private Guid? _editingDespesaId;
    private EditMode _currentEditMode = EditMode.None;

    public MainViewModel(FinanceService financeService, SyncService syncService, UpdateService updateService, IDialogService dialogService)
    {
        _financeService = financeService;
        _syncService = syncService;
        _updateService = updateService;
        _dialogService = dialogService;
        AtualizarTextoMes();
    }

    [RelayCommand]
    public async Task CarregarDadosAsync()
    {
        AtualizarTextoMes();

        await _financeService.ProcessarDespesasRecorrentesAsync(DateTime.Today);

        var ano = DataReferencia.Year;
        var mes = DataReferencia.Month;

        var categorias = await _financeService.GetCategoriasAsync();
        var receitasDoMes = await _financeService.GetReceitasPorMesAsync(ano, mes);
        var despesasDoMes = await _financeService.GetDespesasPorMesAsync(ano, mes);

        Saldo = await _financeService.GetSaldoAsync();
        TotalReceitas = await _financeService.GetTotalReceitasAsync();
        TotalDespesas = await _financeService.GetTotalDespesasAsync();
        TotalCategorias = categorias.Count;

        ReceitasMes = receitasDoMes.Sum(r => r.Valor);
        DespesasMes = despesasDoMes.Sum(d => d.Valor);
        BalancoMes = ReceitasMes - DespesasMes;
        DespesasPagasMes = despesasDoMes.Where(d => d.Paga).Sum(d => d.Valor);
        DespesasPendentesMes = despesasDoMes.Where(d => !d.Paga).Sum(d => d.Valor);

        ReceitasRecentes.Clear();
        foreach (var receita in receitasDoMes.Take(6))
        {
            ReceitasRecentes.Add(receita);
        }

        DespesasRecentes.Clear();
        foreach (var despesa in despesasDoMes.Take(6))
        {
            DespesasRecentes.Add(despesa);
        }

        LastSyncText = _syncService.GetLastSyncText();
        LastSyncModeText = _syncService.GetLastSyncModeText();
        SyncStatusText = "Tudo sincronizado";

        if (!_hasCheckedUpdateOnStartup)
        {
            _hasCheckedUpdateOnStartup = true;
            _ = Task.Run(VerificarAtualizacaoInicialAsync);
        }
    }

    [RelayCommand]
    public async Task MesAnteriorAsync()
    {
        DataReferencia = DataReferencia.AddMonths(-1);
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public async Task ProximoMesAsync()
    {
        DataReferencia = DataReferencia.AddMonths(1);
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public async Task MesAtualAsync()
    {
        DataReferencia = DateTime.Today;
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public async Task ExplicarMetricaAsync(string? metrica)
    {
        switch (metrica?.ToLowerInvariant())
        {
            case "balanco":
                await _dialogService.ShowAlertAsync(
                    "Balanço do Mês",
                    "É o que sobrou (ou faltou) exclusivamente neste mês selecionado.\n\n" +
                    "• Fórmula: Receitas do Mês − Despesas do Mês\n\n" +
                    "Mostra se as suas contas fecharam no positivo ou negativo no período.",
                    "Entendi");
                break;

            case "saldo":
                await _dialogService.ShowAlertAsync(
                    "Saldo Geral (Conta / Total)",
                    "É o dinheiro total acumulado na sua conta/carteira desde o início do uso do app.\n\n" +
                    "• Fórmula: Todas as Receitas de sempre − Todas as Despesas de sempre\n\n" +
                    "Representa o seu saldo real consolidado.",
                    "Entendi");
                break;

            case "receitas":
                await _dialogService.ShowAlertAsync(
                    "Receitas do Mês",
                    "Soma bruta de todas as entradas financeiras (salários, vendas, etc.) registradas no mês selecionado.",
                    "Entendi");
                break;

            case "despesas":
                await _dialogService.ShowAlertAsync(
                    "Despesas do Mês",
                    "Soma de todas as contas e gastos registrados para o mês selecionado.",
                    "Entendi");
                break;

            case "pendentes":
                await _dialogService.ShowAlertAsync(
                    "A Pagar (Pendente)",
                    "Total de despesas deste mês que ainda NÃO foram pagas.\n\n" +
                    "Toque no badge '⏳ PENDENTE' de uma despesa para marcá-la como paga assim que quitar a conta.",
                    "Entendi");
                break;

            case "pagas":
                await _dialogService.ShowAlertAsync(
                    "Já Pago",
                    "Total de despesas deste mês que já foram quitadas e marcadas como '✓ PAGA'.",
                    "Entendi");
                break;
        }
    }

    private void AtualizarTextoMes()
    {
        var hoje = DateTime.Today;
        IsMesAtual = DataReferencia.Year == hoje.Year && DataReferencia.Month == hoje.Month;
        IsMesDiferenteDoAtual = !IsMesAtual;

        var nomeMes = DataReferencia.ToString("MMMM", PtBr);
        if (!string.IsNullOrEmpty(nomeMes))
        {
            nomeMes = char.ToUpper(nomeMes[0]) + nomeMes[1..];
        }

        MesAnoTexto = $"{nomeMes} de {DataReferencia.Year}";
    }

    [RelayCommand]
    public async Task SalvarReceitaAsync()
    {
        if (!TryParseMoney(ReceitaValor, out var valor) || valor <= 0)
        {
            await _dialogService.ShowAlertAsync("Atenção", "Informe um valor válido e maior que zero para a receita.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(ReceitaDescricao))
        {
            await _dialogService.ShowAlertAsync("Atenção", "Informe a descrição da receita.", "OK");
            return;
        }

        await _financeService.AddReceitaAsync(
            ReceitaDescricao.Trim(),
            valor,
            ReceitaData,
            null);

        LimparFormularioReceita();
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public void CancelarReceita()
    {
        LimparFormularioReceita();
    }

    [RelayCommand]
    public async Task SalvarDespesaAsync()
    {
        if (!TryParseMoney(DespesaValor, out var valor) || valor <= 0)
        {
            await _dialogService.ShowAlertAsync("Atenção", "Informe um valor válido e maior que zero para a despesa.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(DespesaDescricao))
        {
            await _dialogService.ShowAlertAsync("Atenção", "Informe a descrição da despesa.", "OK");
            return;
        }

        await _financeService.AddDespesaAsync(
            DespesaDescricao.Trim(),
            valor,
            DespesaData,
            null,
            DespesaVencimento,
            DespesaRecorrente,
            DespesaPaga);

        LimparFormularioDespesa();
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public void CancelarDespesa()
    {
        LimparFormularioDespesa();
    }

    [RelayCommand]
    public async Task AlternarStatusPagamentoAsync(Despesa? despesa)
    {
        if (despesa is null)
        {
            return;
        }

        if (!despesa.Paga)
        {
            var confirmar = await _dialogService.ShowConfirmationAsync(
                "Confirmar Pagamento",
                $"Marcar a despesa '{despesa.Descricao}' ({despesa.Valor.ToString("C", PtBr)}) como PAGA?",
                "Sim, marcar como Paga",
                "Cancelar");

            if (confirmar)
            {
                await _financeService.SetDespesaPagaAsync(despesa.Id, true);
                await CarregarDadosAsync();
            }
        }
        else
        {
            var confirmar = await _dialogService.ShowConfirmationAsync(
                "Reabrir Despesa",
                $"Deseja reabrir a despesa '{despesa.Descricao}' ({despesa.Valor.ToString("C", PtBr)}) como PENDENTE?",
                "Sim, reabrir",
                "Cancelar");

            if (confirmar)
            {
                await _financeService.SetDespesaPagaAsync(despesa.Id, false);
                await CarregarDadosAsync();
            }
        }
    }

    [RelayCommand]
    public async Task SyncAsync()
    {
        IsSyncing = true;
        SyncStatusText = "Sincronizando...";
        try
        {
            var result = await _syncService.SyncAsync();
            SyncStatusText = result.Success ? "Tudo sincronizado" : result.Message;
            LastSyncModeText = _syncService.GetLastSyncModeText();
            await CarregarDadosAsync();

            if (!result.Success)
            {
                await _dialogService.ShowAlertAsync("Sincronização", result.Message, "OK");
            }
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    public async Task ImportSyncAsync()
    {
        try
        {
            var file = await _dialogService.PickFileAsync("Selecione o pacote de sincronização");
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            var result = await _syncService.ImportAsync(stream);
            SyncStatusText = result.Success ? "Tudo sincronizado" : result.Message;
            LastSyncModeText = _syncService.GetLastSyncModeText();
            await CarregarDadosAsync();

            if (!result.Success)
            {
                await _dialogService.ShowAlertAsync("Sincronização", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Sincronização", ex.Message, "OK");
        }
    }

    [RelayCommand]
    public void EditarReceita(Receita? receita)
    {
        if (receita is null)
        {
            return;
        }

        _editingReceitaId = receita.Id;
        OpenEditModal(EditMode.Receita, receita.Descricao, receita.Valor, receita.Data, false, false, null);
    }

    [RelayCommand]
    public async Task ExcluirReceitaAsync(Receita? receita)
    {
        if (receita is null)
        {
            return;
        }

        var confirm = await _dialogService.ShowConfirmationAsync("Excluir receita", $"Excluir '{receita.Descricao}'?", "Excluir", "Cancelar");
        if (!confirm)
        {
            return;
        }

        await _financeService.DeleteReceitaAsync(receita.Id);
        if (_editingReceitaId == receita.Id)
        {
            FecharEdicaoModal();
        }

        await CarregarDadosAsync();
    }

    [RelayCommand]
    public void EditarDespesa(Despesa? despesa)
    {
        if (despesa is null)
        {
            return;
        }

        _editingDespesaId = despesa.Id;
        OpenEditModal(EditMode.Despesa, despesa.Descricao, despesa.Valor, despesa.Data, despesa.Recorrente, despesa.Paga, despesa.Vencimento);
    }

    [RelayCommand]
    public async Task ExcluirDespesaAsync(Despesa? despesa)
    {
        if (despesa is null)
        {
            return;
        }

        var confirm = await _dialogService.ShowConfirmationAsync("Excluir despesa", $"Excluir '{despesa.Descricao}'?", "Excluir", "Cancelar");
        if (!confirm)
        {
            return;
        }

        await _financeService.DeleteDespesaAsync(despesa.Id);
        if (_editingDespesaId == despesa.Id)
        {
            FecharEdicaoModal();
        }

        await CarregarDadosAsync();
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

        if (_currentEditMode == EditMode.Receita && _editingReceitaId.HasValue)
        {
            await _financeService.UpdateReceitaAsync(
                _editingReceitaId.Value,
                EditDescricao.Trim(),
                valor,
                EditData,
                null);
        }
        else if (_currentEditMode == EditMode.Despesa && _editingDespesaId.HasValue)
        {
            await _financeService.UpdateDespesaAsync(
                _editingDespesaId.Value,
                EditDescricao.Trim(),
                valor,
                EditData,
                null,
                EditVencimento,
                EditRecorrente,
                EditPaga);
        }
        else
        {
            await _dialogService.ShowAlertAsync("Edição", "Não foi possível identificar o item que está sendo editado.", "OK");
            return;
        }

        FecharEdicaoModal();
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public void FecharEdicaoModal()
    {
        _currentEditMode = EditMode.None;
        _editingReceitaId = null;
        _editingDespesaId = null;
        IsEditModalVisible = false;
        EditModalTitle = "Editar";
        EditDescricao = string.Empty;
        EditValor = string.Empty;
        EditData = DateTime.Today;
        EditRecorrente = false;
        EditPaga = false;
        EditVencimento = DateTime.Today;
        IsEditRecorrenteVisible = false;
        IsEditVencimentoVisible = false;
        IsEditPagaVisible = false;
    }

    private void OpenEditModal(EditMode mode, string descricao, decimal valor, DateTime data, bool recorrente, bool paga, DateTime? vencimento)
    {
        _currentEditMode = mode;
        IsEditModalVisible = true;

        if (mode == EditMode.Receita)
        {
            EditModalTitle = "Editar receita";
            IsEditRecorrenteVisible = false;
            IsEditVencimentoVisible = false;
            IsEditPagaVisible = false;
        }
        else
        {
            EditModalTitle = "Editar despesa";
            IsEditRecorrenteVisible = true;
            IsEditVencimentoVisible = true;
            IsEditPagaVisible = true;
        }

        EditDescricao = descricao;
        EditValor = valor.ToString("N2", PtBr);
        EditData = data;
        EditRecorrente = recorrente;
        EditPaga = paga;
        EditVencimento = vencimento ?? data;
    }

    private void LimparFormularioReceita()
    {
        ReceitaDescricao = string.Empty;
        ReceitaValor = string.Empty;
        ReceitaData = DateTime.Today;
    }

    private void LimparFormularioDespesa()
    {
        DespesaDescricao = string.Empty;
        DespesaValor = string.Empty;
        DespesaData = DateTime.Today;
        DespesaVencimento = DateTime.Today;
        DespesaRecorrente = false;
        DespesaPaga = false;
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

    private async Task VerificarAtualizacaoInicialAsync()
    {
        try
        {
            await Task.Delay(2500);
            var update = await _updateService.CheckForUpdatesAsync();

            if (update != null && update.IsNewer && !string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var confirm = await _dialogService.ShowConfirmationAsync(
                        $"Nova Versão ({update.TagName}) 🎉",
                        $"Uma nova versão do ManyControl está disponível!\n\n" +
                        $"{(string.IsNullOrWhiteSpace(update.ReleaseNotes) ? string.Empty : $"Novidades:\n{update.ReleaseNotes}\n\n")}" +
                        $"Deseja baixar e atualizar agora?",
                        "Atualizar Agora",
                        "Depois");

                    if (confirm)
                    {
                        await BaixarEInstalarAtualizacaoAsync(update);
                    }
                });
            }
        }
        catch
        {
            // Silencioso na inicialização para evitar interrupções offline
        }
    }

    private async Task BaixarEInstalarAtualizacaoAsync(UpdateInfo update)
    {
        IsDownloadingUpdate = true;
        UpdateProgress = 0;
        UpdateProgressText = "Iniciando download da atualização...";

        var progress = new Progress<double>(p =>
        {
            UpdateProgress = p;
            UpdateProgressText = $"Baixando atualização... {p * 100:F0}%";
        });

        try
        {
            var downloadedFile = await _updateService.DownloadUpdateAsync(update, progress);
            if (!string.IsNullOrWhiteSpace(downloadedFile) && File.Exists(downloadedFile))
            {
                UpdateProgressText = "Download concluído! Reiniciando aplicativo...";
                await Task.Delay(400);
                _updateService.InstallUpdate(downloadedFile);
            }
        }
        catch (Exception ex)
        {
            IsDownloadingUpdate = false;
            await _dialogService.ShowAlertAsync("Falha na Atualização", $"Não foi possível atualizar automaticamente: {ex.Message}", "OK");
        }
    }

    private enum EditMode
    {
        None,
        Receita,
        Despesa
    }
}
