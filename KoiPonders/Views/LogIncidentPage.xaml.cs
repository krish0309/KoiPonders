using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class LogIncidentPage : ContentPage
	{
		private readonly LogIncidentViewModel _viewModel;

		public LogIncidentPage(LogIncidentViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = _viewModel = viewModel;
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
			await _viewModel.LoadAsync();
		}
	}
}
