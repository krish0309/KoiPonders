using System.Collections.ObjectModel;
using System.Windows.Input;
using KoiPonders.Models;
using KoiPonders.Mvvm;
using KoiPonders.Services;
using KoiPonders.Views;

namespace KoiPonders.ViewModels
{
	/// <summary>
	/// Lists saved fields with summary stats and supports opening reports or deleting a field.
	/// </summary>
	public sealed class FieldsViewModel : ObservableObject
	{
		private readonly IFarmDataStore _dataStore;
		private bool _isRefreshing;

		public FieldsViewModel(IFarmDataStore dataStore)
		{
			_dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));

			RefreshCommand = new AsyncRelayCommand(LoadAsync);
			OpenReportsCommand = new AsyncRelayCommand(OpenReportsAsync);
			DeleteFieldCommand = new AsyncRelayCommand(DeleteFieldAsync);
		}

		public ObservableCollection<PlantField> Fields { get; } = new();

		public bool IsRefreshing
		{
			get => _isRefreshing;
			set => SetProperty(ref _isRefreshing, value);
		}

		public ICommand RefreshCommand { get; }

		public ICommand OpenReportsCommand { get; }

		public ICommand DeleteFieldCommand { get; }

		public async Task LoadAsync()
		{
			IsRefreshing = true;
			try
			{
				var fields = await _dataStore.GetFieldsAsync();
				Fields.Clear();
				foreach (var field in fields)
				{
					Fields.Add(field);
				}
			}
			finally
			{
				IsRefreshing = false;
			}
		}

		private async Task OpenReportsAsync(object? parameter)
		{
			if (parameter is not PlantField field)
			{
				return;
			}

			await Shell.Current.GoToAsync($"{nameof(ReportEditPage)}?fieldId={field.Id}");
		}

		private async Task DeleteFieldAsync(object? parameter)
		{
			if (parameter is not PlantField field)
			{
				return;
			}

			var confirm = await Shell.Current.CurrentPage.DisplayAlert(
				"Delete field",
				$"Delete '{field.Name}' and its {field.Reports.Count} report(s)?",
				"Delete",
				"Cancel");

			if (!confirm)
			{
				return;
			}

			await _dataStore.DeleteFieldAsync(field.Id);
			Fields.Remove(field);
		}
	}
}
