param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "artifacts"),
    [switch]$Compact
)

$ErrorActionPreference = "Stop"
$dotnet = Join-Path $PSScriptRoot "..\.tools\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$project = Join-Path $PSScriptRoot "TextGrab\TextGrab.csproj"
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$versionNode = $projectXml.SelectSingleNode("/Project/PropertyGroup/Version")
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "TextGrab.csproj does not declare a Version."
}
$version = $versionNode.InnerText.Trim()
if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "TextGrab.csproj Version '$version' is not safe to use in an archive filename."
}
$publishFolderName = if ($Compact) { "TextGrab-compact-win-x64" } else { "TextGrab-win-x64" }
$archiveName = if ($Compact) { "TextGrab-v$version-compact-requires-dotnet10-win-x64.zip" } else { "TextGrab-v$version-win-x64.zip" }
$publish = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $publishFolderName))
$zip = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $archiveName))
$stagingZip = [System.IO.Path]::GetFullPath((Join-Path $outputRoot ".$archiveName.$([guid]::NewGuid().ToString('N')).tmp.zip"))
$reportName = if ($Compact) { "smoke-report-compact.json" } else { "smoke-report.json" }
$report = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $reportName))
$requiredPrefix = $outputRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $publish.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    [System.IO.Path]::GetFileName($publish) -ne $publishFolderName) {
    throw "Refusing to clean a publish directory outside the requested output directory."
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }

$selfContained = if ($Compact) { "false" } else { "true" }
& $dotnet publish $project -c $Configuration -r win-x64 --self-contained $selfContained -p:DebugType=None -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$quotedReport = '"' + $report.Replace('"', '\"') + '"'
$smokeExecutable = if ($Compact) { $dotnet } else { Join-Path $publish "TextGrab.exe" }
$smokeArguments = if ($Compact) {
    $quotedAssembly = '"' + (Join-Path $publish "TextGrab.dll").Replace('"', '\"') + '"'
    @($quotedAssembly, "--smoke-test", $quotedReport)
} else {
    @("--smoke-test", $quotedReport)
}
$smoke = Start-Process -FilePath $smokeExecutable -ArgumentList $smokeArguments -PassThru -Wait -WindowStyle Hidden
if ($smoke.ExitCode -ne 0) { throw "Published OCR smoke test failed. See $report" }

Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publish,
        $stagingZip,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
    Move-Item -LiteralPath $stagingZip -Destination $zip -Force

    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    $checksumPath = "$zip.sha256"
    $checksumLine = "$hash  $([System.IO.Path]::GetFileName($zip))`r`n"
    [System.IO.File]::WriteAllText($checksumPath, $checksumLine, [System.Text.UTF8Encoding]::new($false))

    Write-Host "Published: $publish"
    Write-Host "ZIP:       $zip"
    Write-Host "SHA-256:   $checksumPath"
    Write-Host "Smoke:     $report"
}
finally {
    if (Test-Path -LiteralPath $stagingZip) {
        Remove-Item -LiteralPath $stagingZip -Force
    }
}
