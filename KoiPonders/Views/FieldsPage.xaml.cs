using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class FieldsPage : ContentPage
	{
		private readonly FieldsViewModel _viewModel;

		public FieldsPage(FieldsViewModel viewModel)
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
