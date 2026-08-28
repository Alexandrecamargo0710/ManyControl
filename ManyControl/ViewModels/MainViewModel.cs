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
    private readonly IDialogService _dialogService;

    // Métricas do Dashboard
    [ObservableProperty]
    public partial decimal Saldo { get; set; }

    [ObservableProperty]
    public partial decimal TotalReceitas { get; set; }

    [ObservableProperty]
    public partial decimal TotalDespesas { get; set; }

    [ObservableProperty]
    public partial int TotalCategorias { get; set; }

    // Sincronização
    [ObservableProperty]
    public partial string LastSyncText { get; set; } = "Nunca sincronizado";

    [ObservableProperty]
    public partial string LastSyncModeText { get; set; } = "Última ação: nenhuma";

    [ObservableProperty]
    public partial string SyncStatusText { get; set; } = "Tudo sincronizado";

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    // Listas Recentes
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
    public partial bool IsEditRecorrenteVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEditVencimentoVisible { get; set; }

    private Guid? _editingReceitaId;
    private Guid? _editingDespesaId;
    private EditMode _currentEditMode = EditMode.None;

    public MainViewModel(FinanceService financeService, SyncService syncService, IDialogService dialogService)
    {
        _financeService = financeService;
        _syncService = syncService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task CarregarDadosAsync()
    {
        await _financeService.ProcessarDespesasRecorrentesAsync(DateTime.Today);

        var categorias = await _financeService.GetCategoriasAsync();
        var receitas = await _financeService.GetReceitasAsync();
        var despesas = await _financeService.GetDespesasAsync();

        Saldo = await _financeService.GetSaldoAsync();
        TotalReceitas = await _financeService.GetTotalReceitasAsync();
        TotalDespesas = await _financeService.GetTotalDespesasAsync();
        TotalCategorias = categorias.Count;

        ReceitasRecentes.Clear();
        foreach (var receita in receitas.Take(5))
        {
            ReceitasRecentes.Add(receita);
        }

        DespesasRecentes.Clear();
        foreach (var despesa in despesas.Take(5))
        {
            DespesasRecentes.Add(despesa);
        }

        LastSyncText = _syncService.GetLastSyncText();
        LastSyncModeText = _syncService.GetLastSyncModeText();
        SyncStatusText = "Tudo sincronizado";
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
            DespesaRecorrente);

        LimparFormularioDespesa();
        await CarregarDadosAsync();
    }

    [RelayCommand]
    public void CancelarDespesa()
    {
        LimparFormularioDespesa();
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
        OpenEditModal(EditMode.Receita, receita.Descricao, receita.Valor, receita.Data, false, null);
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
        OpenEditModal(EditMode.Despesa, despesa.Descricao, despesa.Valor, despesa.Data, despesa.Recorrente, despesa.Vencimento);
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
                EditRecorrente);
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
        EditVencimento = DateTime.Today;
        IsEditRecorrenteVisible = false;
        IsEditVencimentoVisible = false;
    }

    private void OpenEditModal(EditMode mode, string descricao, decimal valor, DateTime data, bool recorrente, DateTime? vencimento)
    {
        _currentEditMode = mode;
        IsEditModalVisible = true;

        if (mode == EditMode.Receita)
        {
            EditModalTitle = "Editar receita";
            IsEditRecorrenteVisible = false;
            IsEditVencimentoVisible = false;
        }
        else
        {
            EditModalTitle = "Editar despesa";
            IsEditRecorrenteVisible = true;
            IsEditVencimentoVisible = true;
        }

        EditDescricao = descricao;
        EditValor = valor.ToString("N2", PtBr);
        EditData = data;
        EditRecorrente = recorrente;
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

    private enum EditMode
    {
        None,
        Receita,
        Despesa
    }
}
