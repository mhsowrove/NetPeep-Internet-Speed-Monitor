param([string]$SdkDirectory,[string]$InnoCompiler)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path (Split-Path $root -Parent) -Parent
$bundle=Join-Path $root 'release\native-windows'
New-Item -ItemType Directory -Force $bundle | Out-Null
if (-not $SdkDirectory) { $SdkDirectory=Join-Path $workspace 'work\webview2\sdk' }
if (-not (Test-Path (Join-Path $SdkDirectory 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
  $cache=Join-Path $root '.build\webview2'
  New-Item -ItemType Directory -Force $cache | Out-Null
  Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4314.35/microsoft.web.webview2.1.0.4314.35.nupkg' -OutFile (Join-Path $cache 'sdk.zip')
  Expand-Archive -LiteralPath (Join-Path $cache 'sdk.zip') -DestinationPath (Join-Path $cache 'sdk') -Force
  $SdkDirectory=Join-Path $cache 'sdk'
}
Copy-Item -LiteralPath (Join-Path $SdkDirectory 'lib\net462\Microsoft.Web.WebView2.Core.dll'),(Join-Path $SdkDirectory 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'),(Join-Path $SdkDirectory 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $bundle -Force
New-Item -ItemType Directory -Force (Join-Path $bundle 'src'),(Join-Path $bundle 'assets') | Out-Null
foreach($file in @('app.html','app.css','app.js','native-bridge.js','format.js')) { Copy-Item -LiteralPath (Join-Path $root "src\$file") -Destination (Join-Path $bundle 'src') -Force }
Copy-Item -LiteralPath (Join-Path $root 'assets\icon.ico'),(Join-Path $root 'assets\icon.svg'),(Join-Path $root 'assets\developer.json') -Destination (Join-Path $bundle 'assets') -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $bundle -Force
if(Test-Path (Join-Path $root 'WINDOWS.md')){Copy-Item -LiteralPath (Join-Path $root 'WINDOWS.md') -Destination (Join-Path $bundle 'README.md') -Force}
if(Test-Path (Join-Path $SdkDirectory 'LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $SdkDirectory 'LICENSE.txt') -Destination (Join-Path $bundle 'WebView2-SDK-LICENSE.txt') -Force}
if(Test-Path (Join-Path $SdkDirectory 'NOTICE.txt')){Copy-Item -LiteralPath (Join-Path $SdkDirectory 'NOTICE.txt') -Destination (Join-Path $bundle 'WebView2-SDK-NOTICE.txt') -Force}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll',(Join-Path $bundle 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $bundle 'Microsoft.Web.WebView2.WinForms.dll'))
$arguments=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/win32icon:'+(Join-Path $root 'assets\icon.ico')),('/out:'+(Join-Path $bundle 'NetPeep.exe')))
foreach($ref in $refs){$arguments+=('/reference:'+$ref)}
& $compiler @arguments (Join-Path $PSScriptRoot 'windows.cs') (Join-Path $PSScriptRoot 'NativeApp.cs')
if($LASTEXITCODE -ne 0){throw 'Native compilation failed'}
$portable=Join-Path (Split-Path $root -Parent) 'NetPeep-2.0.0-Windows-x64-Portable.zip'
Compress-Archive -LiteralPath (Get-ChildItem $bundle).FullName -DestinationPath $portable -Force
if($InnoCompiler){& $InnoCompiler /Q ("/DPayloadDir=$bundle") ("/DReleaseDir="+(Split-Path $root -Parent)) (Join-Path $PSScriptRoot 'NetPeep.iss');if($LASTEXITCODE -ne 0){throw 'Installer build failed'}}
Get-Item -LiteralPath $portable | Select-Object Name,Length
