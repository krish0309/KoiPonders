using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class ReportEditPage : ContentPage
	{
		public ReportEditPage(ReportEditViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = viewModel;
		}
	}
}
