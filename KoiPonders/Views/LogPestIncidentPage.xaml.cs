using KoiPonders.ViewModels;

namespace KoiPonders.Views
{
	public partial class LogPestIncidentPage : ContentPage, IQueryAttributable
	{
		private readonly LogPestIncidentViewModel _viewModel;

		public LogPestIncidentPage(LogPestIncidentViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = _viewModel = viewModel;
		}

		public void ApplyQueryAttributes(IDictionary<string, object> query)
		{
			if (query.TryGetValue("fieldId", out var fieldIdRaw)
				&& Guid.TryParse(Convert.ToString(fieldIdRaw), out var fieldId))
			{
				_viewModel.SetTargetField(fieldId);
			}

			if (TryGetDouble(query, "lat", out var latitude) && TryGetDouble(query, "lon", out var longitude))
			{
				_viewModel.ApplyPickedLocation(latitude, longitude);
			}
		}

		private static bool TryGetDouble(IDictionary<string, object> query, string key, out double value)
		{
			value = 0;
			if (!query.TryGetValue(key, out var raw))
			{
				return false;
			}

			if (raw is double d)
			{
				value = d;
				return true;
			}

			return double.TryParse(
				Convert.ToString(raw),
				System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture,
				out value);
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
			await _viewModel.LoadAsync();
		}
	}
}
