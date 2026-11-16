param([string]$WorkDirectory)
$ErrorActionPreference = 'Stop'
if (!$WorkDirectory) { $WorkDirectory=Join-Path $PSScriptRoot 'test-work' }
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$work = (Resolve-Path -LiteralPath $WorkDirectory).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$test = Join-Path $work 'CoreTests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Where-Object { $_.Name -notmatch '^(Program|MainWindow|Icons|AppAssets|CrispBorder|DemoRecorder)' } | ForEach-Object FullName)
$testSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | Where-Object { $_.Name -ne 'UiTests.cs' } | ForEach-Object FullName)
$arguments = @('/nologo','/define:PACKHELPER_TESTS','/target:exe','/codepage:65001','/r:System.Management.dll','/r:System.Web.Extensions.dll','/r:System.Net.Http.dll', "/out:$test", "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')") + $sources + $testSources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $test $work
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
