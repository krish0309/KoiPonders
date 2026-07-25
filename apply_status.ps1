$p = "KoiPonders\MapViewModel.cs"
$t = Get-Content $p -Raw

# Revert StatusMessage setter back to private
$t = $t.Replace(
  "            set => SetProperty(ref _statusMessage, value);",
  "            private set => SetProperty(ref _statusMessage, value);")

# Add a public SetStatus method right after the StatusMessage property block.
# Anchor on the NewFieldName property that follows StatusMessage.
$anchor = "        public string NewFieldName"
$method = @"
		/// <summary>
		/// Updates the status banner from outside the view model (e.g. map tap handling).
		/// </summary>
		public void SetStatus(string message) => StatusMessage = message;

"@
$t = $t.Replace($anchor, $method + $anchor)

Set-Content -Path $p -Value $t -Encoding UTF8 -NoNewline
Write-Host "=== verify ==="
Select-String -Path $p -Pattern "private set => SetProperty\(ref _statusMessage|public void SetStatus"
