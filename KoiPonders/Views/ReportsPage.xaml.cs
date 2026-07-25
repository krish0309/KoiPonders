using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class ReportsPage : ContentPage
	{
		private readonly ReportsViewModel _viewModel;

		public ReportsPage(ReportsViewModel viewModel)
		{
			InitializeComponent();
			_viewModel = viewModel;
			BindingContext = _viewModel;
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
			await _viewModel.LoadAsync();
		}
	}
}
