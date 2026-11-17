$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$OutputEncoding = [Console]::OutputEncoding
Add-Type -Path (Join-Path $PSScriptRoot 'windows.cs')
while ($true) {
  try { [NetPeepNative]::Sample() | ConvertTo-Json -Compress -Depth 5 }
  catch { @{ error = $_.Exception.Message } | ConvertTo-Json -Compress }
  if ($env:NETPEEP_ONCE -eq '1') { break }
  Start-Sleep -Milliseconds 1000
}
