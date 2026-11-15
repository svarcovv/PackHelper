param(
    [Parameter(Mandatory=$true)][string]$PackPath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$PackVersion,
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
    appVersion = '1.0.0'
    packVersion = $PackVersion
    packId = $packGuid.ToString()
    sha1 = (Get-FileHash -Algorithm SHA1 -LiteralPath $pack).Hash.ToLowerInvariant()
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $pack).Hash.ToLowerInvariant()
    size = (Get-Item -LiteralPath $pack).Length
    server = 'lavabox.fun'
    sourceUrl = $SourceUrl
    discordUrl = 'https://discord.gg/xMBKSnDvt'
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
$executable = Join-Path $output "PackHelper-$PackVersion.exe"
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+',
    '/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Management.dll','/r:System.Web.Extensions.dll',
    "/out:$executable", "/win32icon:$(Join-Path $PSScriptRoot 'assets\packhelper.ico')", "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')", "/resource:$pack,LavaBox.Pack.zip", "/resource:$manifestPath,LavaBox.Release.json") + $sources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать PackHelper.' }
$hashFile = Join-Path $output 'SHA256SUMS.txt'
$sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $executable).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($hashFile, "$sha  $([IO.Path]::GetFileName($executable))`n", $utf8)
Write-Output "Готово: $executable"
Write-Output "Пак: $PackVersion; SHA-1: $($manifest.sha1)"
