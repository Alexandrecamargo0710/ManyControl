using ManyControl.ViewModels;

namespace ManyControl.Views;

public partial class ExtratoPage : ContentPage
{
    private readonly ExtratoViewModel _viewModel;

    public ExtratoPage(ExtratoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarMesCommand.ExecuteAsync(null);
    }
}
