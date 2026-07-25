using System.Windows.Input;

namespace KoiPonders.Mvvm
{
	/// <summary>
	/// An asynchronous <see cref="ICommand"/> implementation that prevents re-entrancy while running.
	/// </summary>
	public sealed class AsyncRelayCommand : ICommand
	{
		private readonly Func<object?, Task> _execute;
		private readonly Func<object?, bool>? _canExecute;
		private bool _isExecuting;

		public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
		{
			ArgumentNullException.ThrowIfNull(execute);
			_execute = _ => execute();
			_canExecute = canExecute is null ? null : _ => canExecute();
		}

		public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

		public event EventHandler? CanExecuteChanged;

		public bool CanExecute(object? parameter) =>
			!_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

		public async void Execute(object? parameter)
		{
			if (!CanExecute(parameter))
			{
				return;
			}

			try
			{
				_isExecuting = true;
				RaiseCanExecuteChanged();
				await _execute(parameter).ConfigureAwait(true);
			}
			finally
			{
				_isExecuting = false;
				RaiseCanExecuteChanged();
			}
		}

		public void RaiseCanExecuteChanged() =>
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}
