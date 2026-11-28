$ErrorActionPreference = "Stop"

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8



$ProjectDir = Split-Path -Parent $PSScriptRoot

$PythonExe = Join-Path $ProjectDir ".venv\Scripts\python.exe"



if (-not (Test-Path $PythonExe)) {

    Write-Host "[错误] 未找到项目虚拟环境: $PythonExe" -ForegroundColor Red

    pause

    exit 1

}



function Invoke-AutostartPy([string]$Code) {

    Push-Location $ProjectDir

    try {

        & $PythonExe -c $Code

    } finally {

        Pop-Location

    }

}



while ($true) {

    $statusJson = Invoke-AutostartPy "import json, pathlib; from miyouqian.core.autostart import get_autostart_status; print(json.dumps(get_autostart_status(pathlib.Path('.'), '127.0.0.1'), ensure_ascii=False))"

    $status = $statusJson | ConvertFrom-Json

    $stateText = if ($status.enabled) { "已开启" } else { "未开启" }



    Write-Host ""

    Write-Host "=========================================="

    Write-Host "  米游签 - 开机自启动设置（用户自主选择）"

    Write-Host "=========================================="

    Write-Host "当前开机自启状态: $stateText"

    if ($status.shortcut_path) {

        Write-Host "启动快捷方式路径: $($status.shortcut_path)"

    }

    Write-Host ""

    Write-Host "  [1] 开启开机自启（登录 Windows 后后台静默运行）"

    Write-Host "  [2] 关闭开机自启"

    Write-Host "  [3] 退出"

    Write-Host ""

    $choice = (Read-Host "请输入选项编号 (1/2/3)").Trim()



    if ($choice -eq "1") {

        Invoke-AutostartPy "import pathlib; from miyouqian.core.autostart import set_autostart; res = set_autostart(pathlib.Path('.'), True, '127.0.0.1'); print('已开启开机自启:', res['shortcut_path'])"

    } elseif ($choice -eq "2") {

        Invoke-AutostartPy "import pathlib; from miyouqian.core.autostart import set_autostart; set_autostart(pathlib.Path('.'), False, '127.0.0.1'); print('已关闭开机自启')"

    } elseif ($choice -eq "3" -or $choice -eq "") {

        break

    } else {

        Write-Host "无效输入，请输入 1、2 或 3。" -ForegroundColor Yellow

    }

}

