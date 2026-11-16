param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [string]$WorkDirectory
)
$ErrorActionPreference='Stop'
if (!$WorkDirectory) { $WorkDirectory=Join-Path $PSScriptRoot 'test-work/ui' }
$fixture=Join-Path $WorkDirectory ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'game/logs'),(Join-Path $fixture 'captures') -Force | Out-Null
$executable=(Resolve-Path -LiteralPath $ExecutablePath).Path
$fixture=(Resolve-Path -LiteralPath $fixture).Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
$framework=Split-Path $compiler
$runner=Join-Path $fixture 'UiTests.exe'
Copy-Item -LiteralPath $executable -Destination $fixture
$arguments=@('/nologo','/target:exe','/codepage:65001',"/r:$executable",'/r:System.Xaml.dll',"/r:$framework\WPF\PresentationCore.dll","/r:$framework\WPF\PresentationFramework.dll","/r:$framework\WPF\WindowsBase.dll","/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')","/out:$runner",(Join-Path $PSScriptRoot 'tests/UiTests.cs'))
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
& $runner (Join-Path $fixture 'game') (Join-Path $fixture 'captures')
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
