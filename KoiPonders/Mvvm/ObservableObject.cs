using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KoiPonders.Mvvm
{
	/// <summary>
	/// Minimal base class implementing <see cref="INotifyPropertyChanged"/> for hand-rolled MVVM.
	/// </summary>
	public abstract class ObservableObject : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>
		/// Sets the backing field and raises <see cref="PropertyChanged"/> when the value changes.
		/// </summary>
		protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return false;
			}

			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}
}
