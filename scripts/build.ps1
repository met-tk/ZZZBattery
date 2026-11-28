param([switch]$SkipTests, [ValidatePattern('^[A-Za-z0-9._-]+$')][string]$PackageName = 'ZZZBattery')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $projectRoot 'NuGet.Config'
$outputPath = Join-Path $projectRoot ('dist\' + $PackageName)
Push-Location $projectRoot
try {
    if (-not $SkipTests) {
        & dotnet test 'tests\ZzzBattery.Tests\ZzzBattery.Tests.csproj' -c Release "-p:RestoreConfigFile=$configPath" -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }
    if (Test-Path -LiteralPath $outputPath) {
        Remove-Item -LiteralPath $outputPath -Recurse -Force
    }
    & dotnet publish 'src\ZzzBattery.Windows\ZzzBattery.Windows.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false "-p:RestoreConfigFile=$configPath" -p:NuGetAudit=false -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $outputPath '使用说明.md')
    $miyoSrc = Join-Path $projectRoot 'MiyoQian'
    if (Test-Path -LiteralPath (Join-Path $miyoSrc 'main.py')) {
        $miyoDst = Join-Path $outputPath 'MiyoQian'
        if (Test-Path -LiteralPath $miyoDst) { Remove-Item -LiteralPath $miyoDst -Recurse -Force }
        $excludedDirs = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]@('.venv', 'venv', '__pycache__', 'data', 'logs', 'tests', '.git', '.github', '.idea', '.pytest_cache', '.ruff_cache', 'docker'),
            [System.StringComparer]::OrdinalIgnoreCase
        )
        $excludedFiles = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]@('config.yaml', 'qrcode.png', '.env', '.dockerignore', 'credentials.yaml', 'credentials.json'),
            [System.StringComparer]::OrdinalIgnoreCase
        )
        $copyMiyoTree = {
            param([string]$SrcDir, [string]$DstDir)
            New-Item -ItemType Directory -Path $DstDir -Force | Out-Null
            foreach ($entry in Get-ChildItem -LiteralPath $SrcDir -Force) {
                if ($entry.PSIsContainer) {
                    if ($excludedDirs.Contains($entry.Name)) { continue }
                    & $copyMiyoTree -SrcDir $entry.FullName -DstDir (Join-Path $DstDir $entry.Name)
                } else {
                    if ($excludedFiles.Contains($entry.Name)) { continue }
                    if ($entry.Name -like '.env.*' -or $entry.Extension -in '.log', '.pyc', '.pyo', '.pyd', '.dpapi') { continue }
                    Copy-Item -LiteralPath $entry.FullName -Destination (Join-Path $DstDir $entry.Name) -Force
                }
            }
        }
        & $copyMiyoTree -SrcDir $miyoSrc -DstDir $miyoDst
    }
    $licensePath = Join-Path $outputPath 'licenses'
    New-Item -ItemType Directory -Path $licensePath -Force | Out-Null
    $runtime = Get-ChildItem -LiteralPath (Join-Path $projectRoot '.packages\microsoft.netcore.app.runtime.win-x64') -Directory |
        Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if ($runtime) {
        Copy-Item -LiteralPath (Join-Path $runtime.FullName 'LICENSE.TXT') -Destination (Join-Path $licensePath 'dotnet-LICENSE.txt')
        Copy-Item -LiteralPath (Join-Path $runtime.FullName 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licensePath 'dotnet-NOTICES.txt')
    }
    $desktop = Get-ChildItem -LiteralPath (Join-Path $projectRoot '.packages\microsoft.windowsdesktop.app.runtime.win-x64') -Directory |
        Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if ($desktop) { Copy-Item -LiteralPath (Join-Path $desktop.FullName 'LICENSE') -Destination (Join-Path $licensePath 'windowsdesktop-LICENSE.txt') }
    $webviewLicense = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.web.webview2\1.0.3719.77\LICENSE.txt'
    if (-not (Test-Path -LiteralPath $webviewLicense)) { $webviewLicense = Join-Path $projectRoot '.packages\microsoft.web.webview2\1.0.3719.77\LICENSE.txt' }
    if (Test-Path -LiteralPath $webviewLicense) { Copy-Item -LiteralPath $webviewLicense -Destination (Join-Path $licensePath 'webview2-LICENSE.txt') }
    $qrLicense = Join-Path $projectRoot '.packages\qrcoder\1.8.1\LICENSE.txt'
    if (Test-Path -LiteralPath $qrLicense) { Copy-Item -LiteralPath $qrLicense -Destination (Join-Path $licensePath 'qrcoder-LICENSE.txt') }
    $zipPath = Join-Path $projectRoot ('dist\' + $PackageName + '-win-x64.zip')
    Compress-Archive -LiteralPath $outputPath -DestinationPath $zipPath -Force
    Get-FileHash -LiteralPath (Join-Path $outputPath 'ZZZBattery.exe') -Algorithm SHA256 |
        ForEach-Object { $_.Hash + '  ZZZBattery.exe' } |
        Set-Content -LiteralPath (Join-Path $projectRoot ('dist\' + $PackageName + '-SHA256SUMS.txt'))
    Write-Output $zipPath
} finally { Pop-Location }
