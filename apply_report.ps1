$p = "KoiPonders\MapViewModel.cs"
$t = Get-Content $p -Raw
$nl = "`r`n"

# 1. Add _isReporting field
$t = $t.Replace(
  "private bool _isGpsCapture;$nl",
  "private bool _isGpsCapture;$nl        private bool _isReporting;$nl")

# 2. Gate Draw/Walk while reporting
$t = $t.Replace(
  "StartDrawCommand = new RelayCommand(StartDraw, () => !IsBusy);",
  "StartDrawCommand = new RelayCommand(StartDraw, () => !IsBusy && !IsReporting);")
$t = $t.Replace(
  "StartGpsCaptureCommand = new AsyncRelayCommand(StartGpsCaptureAsync, () => !IsBusy);",
  "StartGpsCaptureCommand = new AsyncRelayCommand(StartGpsCaptureAsync, () => !IsBusy && !IsReporting);")

# 3. Add report command construction
$oldCancel = "            CancelCommand = new RelayCommand(CancelEditing, () => IsDrawing || IsGpsCapture);$nl        }"
$newCancel = @"
			CancelCommand = new RelayCommand(CancelEditing, () => IsDrawing || IsGpsCapture);
			StartReportCommand = new RelayCommand(StartReport, () => !IsBusy && !IsReporting);
			CancelReportCommand = new RelayCommand(CancelReport, () => IsReporting);
		}
"@
$t = $t.Replace($oldCancel, $newCancel)

# 4. Add IsReporting property + make StatusMessage settable after IsBusy
$oldBusy = @"
		public bool IsBusy => IsDrawing || IsGpsCapture;

		public string StatusMessage
		{
			get => _statusMessage;
			private set => SetProperty(ref _statusMessage, value);
		}
"@
$newBusy = @"
		public bool IsBusy => IsDrawing || IsGpsCapture;

		public bool IsReporting
		{
			get => _isReporting;
			private set
			{
				if (SetProperty(ref _isReporting, value))
				{
					RaiseCommandStates();
				}
			}
		}

		public string StatusMessage
		{
			get => _statusMessage;
			set => SetProperty(ref _statusMessage, value);
		}
"@
$t = $t.Replace($oldBusy, $newBusy)

# 5. Add command properties after CancelCommand
$oldCancelProp = @"
		public ICommand CancelCommand { get; }
"@
$newCancelProp = @"
		public ICommand CancelCommand { get; }

		public ICommand StartReportCommand { get; }

		public ICommand CancelReportCommand { get; }
"@
$t = $t.Replace($oldCancelProp, $newCancelProp)

# 6. Add StartReport/CancelReport methods after CancelEditing
$oldEditing = @"
		private void CancelEditing()
		{
			ResetEditingState();
			IsDrawing = false;
			IsGpsCapture = false;
			StatusMessage = "Editing cancelled.";
		}
"@
$newEditing = @"
		private void CancelEditing()
		{
			ResetEditingState();
			IsDrawing = false;
			IsGpsCapture = false;
			StatusMessage = "Editing cancelled.";
		}

		private void StartReport()
		{
			if (Fields.Count == 0)
			{
				StatusMessage = "Add a field first, then tap inside it to report.";
				return;
			}

			IsReporting = true;
			StatusMessage = "Report mode: tap inside a field to log an incident there.";
		}

		private void CancelReport()
		{
			IsReporting = false;
			StatusMessage = "Report cancelled.";
		}
"@
$t = $t.Replace($oldEditing, $newEditing)

# 7. Raise report command states
$oldRaise = "            (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();$nl        }"
$newRaise = @"
			(CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(StartReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CancelReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}
"@
$t = $t.Replace($oldRaise, $newRaise)

Set-Content -Path $p -Value $t -Encoding UTF8 -NoNewline
Write-Host "=== verification ==="
Select-String -Path $p -Pattern "_isReporting|StartReportCommand|IsReporting|private void StartReport"
