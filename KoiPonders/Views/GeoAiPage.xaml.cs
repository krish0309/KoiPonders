using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class GeoAiPage : ContentPage
	{
		public GeoAiPage(GeoAiViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = viewModel;
		}
	}
}
