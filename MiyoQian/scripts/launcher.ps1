param(
    [ValidateSet("Start", "Stop", "Foreground", "Status")]
    [string]$Action = "Start",
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ProjectDir = Split-Path -Parent $PSScriptRoot
$PythonExe = Join-Path $ProjectDir ".venv\Scripts\python.exe"
$PythonWExe = Join-Path $ProjectDir ".venv\Scripts\pythonw.exe"
$MainScript = Join-Path $ProjectDir "main.py"
$ConfigFile = Join-Path $ProjectDir "config.yaml"
$RuntimeFile = Join-Path $ProjectDir "data\web_runtime.json"

if (-not (Test-Path $PythonExe)) {
    $uvCmd = Get-Command uv -ErrorAction SilentlyContinue
    $uvPath = if ($uvCmd) { $uvCmd.Source } else { Join-Path $env:USERPROFILE ".local\bin\uv.exe" }
    if ($uvPath -and (Test-Path $uvPath)) {
        Write-Host "首次运行，正在使用 uv 初始化项目虚拟环境..."
        Push-Location $ProjectDir
        try {
            & $uvPath sync
        } finally {
            Pop-Location
        }
    }
    if (-not (Test-Path $PythonExe)) {
        Write-Host "[错误] 未找到项目虚拟环境: $PythonExe" -ForegroundColor Red
        Write-Host "请先安装 uv 并在项目根目录运行 uv sync 后重试。"
        exit 1
    }
}

function Ensure-Config {
    if (-not (Test-Path $ConfigFile)) {
        Write-Host "首次启动，正在生成默认配置文件 config.yaml ..."
        Push-Location $ProjectDir
        try {
            & $PythonExe $MainScript init | Out-Null
        } finally {
            Pop-Location
        }
    }
}

function Get-ProjectProcesses {
    $normDir = $ProjectDir.TrimEnd("\").ToLowerInvariant() + "\"
    $runtimePid = 0
    if (Test-Path $RuntimeFile) {
        try {
            $info = Get-Content $RuntimeFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $runtimePid = [int]$info.pid
        } catch {
            $runtimePid = 0
        }
    }
    $all = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -match '^pythonw?\.exe$' -and $_.CommandLine -match 'main\.py'
    }
    $roots = @($all | Where-Object {
        ($runtimePid -gt 0 -and $_.ProcessId -eq $runtimePid) -or
        ($_.ExecutablePath -and $_.ExecutablePath.ToLowerInvariant().StartsWith($normDir))
    })
    $rootIds = @($roots | ForEach-Object { [int]$_.ProcessId })
    $parentIds = @($roots | ForEach-Object { [int]$_.ParentProcessId })
    $all | Where-Object {
        ([int]$_.ProcessId -in $rootIds) -or
        ([int]$_.ParentProcessId -in $rootIds) -or
        ([int]$_.ProcessId -in $parentIds)
    }
}

function Get-RunningRuntime {
    if (Test-Path $RuntimeFile) {
        try {
            $info = Get-Content $RuntimeFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $proc = Get-Process -Id ([int]$info.pid) -ErrorAction SilentlyContinue
            if ($proc) {
                return $info
            }
        } catch {
        }
    }
    return $null
}

function Test-LoopbackUrl([string]$Url) {
    return $Url -match '^https?://(127\.0\.0\.1|localhost|\[::1\])(:\d+)?/?$'
}

function Test-UrlReady([string]$Url) {
    if (-not (Test-LoopbackUrl $Url)) {
        return $false
    }
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Uri "$($Url.TrimEnd('/'))/api/auth/status" -TimeoutSec 2
        return $resp.StatusCode -eq 200
    } catch {
        return $false
    }
}

switch ($Action) {
    "Start" {
        Ensure-Config
        $existing = Get-RunningRuntime
        if ($existing -and (Test-UrlReady ([string]$existing.url))) {
            Write-Host "米游签已在后台运行: $($existing.url)"
            if (-not $NoBrowser -and (Test-LoopbackUrl ([string]$existing.url))) { Start-Process ([string]$existing.url) }
            exit 0
        }

        $stray = Get-ProjectProcesses
        if ($stray) {
            Write-Host "检测到旧的未响应进程，正在清理..."
            $stray | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Milliseconds 400
        }

        if (Test-Path $RuntimeFile) {
            Remove-Item $RuntimeFile -Force -ErrorAction SilentlyContinue
        }

        $runner = if (Test-Path $PythonWExe) { $PythonWExe } else { $PythonExe }
        Write-Host "正在后台启动米游签服务..."
        Start-Process -FilePath $runner -ArgumentList "main.py", "serve" -WorkingDirectory $ProjectDir -WindowStyle Hidden | Out-Null

        $targetUrl = $null
        for ($i = 0; $i -lt 30; $i++) {
            Start-Sleep -Milliseconds 500
            $info = Get-RunningRuntime
            if ($info -and (Test-UrlReady ([string]$info.url))) {
                $targetUrl = [string]$info.url
                break
            }
        }

        if (-not $targetUrl) {
            Write-Host "[错误] 启动超时，请查看 logs\miyouqian.log 或运行「前台调试启动.bat」排查。" -ForegroundColor Red
            exit 1
        }

        Write-Host "米游签已启动: $targetUrl"
        Write-Host "提示：关闭浏览器页面不会停止后台每日自动签到；如需停止服务请运行「停止米游签.bat」。"
        if (-not $NoBrowser -and (Test-LoopbackUrl $targetUrl)) { Start-Process $targetUrl }
    }
    "Stop" {
        $procs = Get-ProjectProcesses
        if (-not $procs) {
            if (Test-Path $RuntimeFile) {
                Remove-Item $RuntimeFile -Force -ErrorAction SilentlyContinue
            }
            Write-Host "当前没有正在运行的米游签后台进程。"
            exit 0
        }
        foreach ($p in $procs) {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Milliseconds 300
        if (Test-Path $RuntimeFile) {
            Remove-Item $RuntimeFile -Force -ErrorAction SilentlyContinue
        }
        Write-Host "已停止米游签后台服务。"
    }
    "Foreground" {
        Ensure-Config
        $existing = Get-RunningRuntime
        if ($existing) {
            Write-Host "提示：检测到后台服务正在运行 (PID=$($existing.pid), $($existing.url))，将先停止后台服务再切换至前台运行..."
            Get-ProjectProcesses | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Milliseconds 400
        }
        Push-Location $ProjectDir
        try {
            & $PythonExe $MainScript serve
        } finally {
            Pop-Location
        }
    }
    "Status" {
        $existing = Get-RunningRuntime
        if ($existing -and (Test-UrlReady $existing.url)) {
            Write-Host "运行中: PID=$($existing.pid) URL=$($existing.url)"
        } else {
            Write-Host "未运行"
        }
    }
}
