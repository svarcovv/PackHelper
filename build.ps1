param(
    [Parameter(Mandatory=$true)][string]$PackPath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$PackVersion,
    [ValidatePattern('^[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}$')][string]$AppVersion = '1.4.0',
    [string]$PackId = '4e7502b2-1883-44d9-a2b4-03cfd5884e13',
    [string]$SourceUrl = 'https://github.com/svarcovv/PackHelper',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)
$ErrorActionPreference = 'Stop'
$pack = (Resolve-Path -LiteralPath $PackPath).Path
if ((Get-Item -LiteralPath $pack).Length -gt 64MB) { throw 'Архив больше 64 МБ.' }
$packGuid = [guid]::ParseExact($PackId, 'D')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($pack)
try {
    $entry = $archive.GetEntry('pack.mcmeta')
    if ($null -eq $entry) { throw 'pack.mcmeta должен лежать в корне ZIP.' }
    $reader = New-Object IO.StreamReader($entry.Open())
    try { $metadata = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($null -eq $metadata.pack) { throw 'Неверный pack.mcmeta.' }
} finally { $archive.Dispose() }
$manifest = [ordered]@{
    appVersion = $AppVersion
    packVersion = $PackVersion
    packId = $packGuid.ToString()
    sha1 = (Get-FileHash -Algorithm SHA1 -LiteralPath $pack).Hash.ToLowerInvariant()
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $pack).Hash.ToLowerInvariant()
    size = (Get-Item -LiteralPath $pack).Length
    server = 'lavabox.fun'
    sourceUrl = $SourceUrl
    discordUrl = 'https://discord.gg/xMBKSnDvt'
    developerUrl = 'https://t.me/AntiDoDmiGEI'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$manifestPath = Join-Path $output 'pack-release.json'
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json) + "`n", $utf8)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw 'Не найден компилятор .NET Framework.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$executable = Join-Path $output "PackHelper-$AppVersion.exe"
$build = Join-Path $output '.build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$versionFile = Join-Path $build 'BuildVersion.cs'
[IO.File]::WriteAllText($versionFile, "[assembly: System.Reflection.AssemblyVersion(`"$AppVersion.0`")]`n[assembly: System.Reflection.AssemblyFileVersion(`"$AppVersion.0`")]`n", $utf8)
$framework = Split-Path -Parent $compiler
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',
    '/r:System.Windows.Forms.dll','/r:System.Management.dll','/r:System.Web.Extensions.dll','/r:System.Net.Http.dll','/r:System.Xml.Linq.dll',
    "/r:$framework\WPF\PresentationCore.dll", "/r:$framework\WPF\PresentationFramework.dll", "/r:$framework\WPF\WindowsBase.dll", '/r:System.Xaml.dll',
    "/out:$executable", "/win32icon:$(Join-Path $PSScriptRoot 'assets\packhelper.ico')", "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')", "/resource:$pack,LavaBox.Pack.zip", "/resource:$manifestPath,LavaBox.Release.json", "/resource:$(Join-Path $PSScriptRoot 'src\Window.xaml'),PackHelper.Window.xaml") + $sources + $versionFile
$arguments += "/resource:$(Join-Path $PSScriptRoot 'src\Theme.xaml'),PackHelper.Theme.xaml"
$arguments += "/resource:$(Join-Path $PSScriptRoot 'assets\packhelper.ico'),PackHelper.App.ico"
$arguments += "/resource:$(Join-Path $PSScriptRoot 'assets\packhelper.svg'),PackHelper.App.svg"
foreach($icon in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'assets\icons') -File)) {
    $resource = if($icon.Extension -eq '.svg') { 'PackHelper.Icons.'+$icon.Name } else { 'PackHelper.Licenses.'+$icon.Name }
    $arguments += "/resource:$($icon.FullName),$resource"
}
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать PackHelper.' }
$hashFile = Join-Path $output 'SHA256SUMS.txt'
$sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $executable).Hash.ToLowerInvariant()
$updatePath = Join-Path $output 'update.json'
$update = [ordered]@{schemaVersion=1;appVersion=$AppVersion;packVersion=$PackVersion;packId=$manifest.packId;packSha1=$manifest.sha1;packSha256=$manifest.sha256;exeName=[IO.Path]::GetFileName($executable);exeSize=(Get-Item -LiteralPath $executable).Length;exeSha256=$sha;downloadUrl="https://github.com/svarcovv/PackHelper/releases/download/v$AppVersion/$([IO.Path]::GetFileName($executable))"}
[IO.File]::WriteAllText($updatePath, ($update | ConvertTo-Json) + "`n", $utf8)
$sums = @(foreach($file in @($executable,$manifestPath,$updatePath)) { '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($file) })
[IO.File]::WriteAllLines($hashFile,$sums,$utf8)
Write-Output "Готово: $executable"
Write-Output "Пак: $PackVersion; SHA-1: $($manifest.sha1)"
