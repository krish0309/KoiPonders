using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class RegionalFeedPage : ContentPage
	{
		public RegionalFeedPage(RegionalFeedViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = viewModel;
		}
	}
}
