$p = "KoiPonders\MainPage.xaml.cs"
$t = Get-Content $p -Raw

$t = $t.Replace(
  '_viewModel.StatusMessage = "That spot is outside your fields. Tap inside a field boundary.";',
  '_viewModel.SetStatus("That spot is outside your fields. Tap inside a field boundary.");')

$t = $t.Replace(
  '_viewModel.StatusMessage = $"Logging incident for ''{fieldName}''.";',
  '_viewModel.SetStatus($"Logging incident for ''{fieldName}''.");')

Set-Content -Path $p -Value $t -Encoding UTF8 -NoNewline
Write-Host "=== verify ==="
Select-String -Path $p -Pattern "SetStatus|StatusMessage ="
