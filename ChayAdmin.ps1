param([switch]$StartOnly, [switch]$RestartApi)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$apiUrl = "http://localhost:5001"
$healthUrl = "$apiUrl/health/db"

if ($RestartApi) {
    $rootPrefix = [IO.Path]::GetFullPath($projectRoot).TrimEnd('\') + '\'
    $listeners = Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($listenerPid in $listeners) {
        $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $listenerPid" -ErrorAction SilentlyContinue
        $processPath = $processInfo.ExecutablePath
        $belongsToProject = $processPath -and $processPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)
        if ($belongsToProject -and ($processInfo.Name -like "Library.Api*" -or $processInfo.CommandLine -match "Library.Api")) {
            Stop-Process -Id $listenerPid -Force
            Write-Host "Đã dừng API cũ của repo (PID $listenerPid)." -ForegroundColor Yellow
        }
        else {
            Write-Error "Cổng 5001 đang được tiến trình khác sử dụng (PID $listenerPid); script không tự dừng tiến trình ngoài repo."
            exit 1
        }
    }
}

function Test-LibraryApi {
    try {
        $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2
        return $health.status -eq "ok" -and $health.database -eq "connected"
    }
    catch {
        return $false
    }
}

if (-not (Test-LibraryApi)) {
    try {
        SqlLocalDB start MSSQLLocalDB | Out-Null
    }
    catch {
        Write-Error "Không khởi động được SQL Server LocalDB. Hãy kiểm tra LocalDB đã cài và thử: SqlLocalDB start MSSQLLocalDB"
        exit 1
    }

    $env:ASPNETCORE_URLS = $apiUrl
    $env:ConnectionStrings__Library = "Server=(localdb)\MSSQLLocalDB;Database=LibraryManagement;Trusted_Connection=True;TrustServerCertificate=True"
    $env:Logging__EventLog__LogLevel__Default = "None"

    $apiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", ".\Source\Library.Api\Library.Api.csproj", "--no-launch-profile") `
        -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru

    $apiReady = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Seconds 1
        if (Test-LibraryApi) {
            $apiReady = $true
            break
        }
        if ($apiProcess.HasExited) {
            break
        }
    }

    if (-not $apiReady) {
        Write-Error "API chưa sẵn sàng tại $healthUrl. Kiểm tra database LibraryManagement đã được tạo/seed và cổng 5001 chưa bị ứng dụng khác chiếm."
        exit 1
    }
}

if ($StartOnly) {
    Write-Host "API và LocalDB đã sẵn sàng tại $apiUrl." -ForegroundColor Green
    Write-Host "Chế độ StartOnly: API tiếp tục chạy nền."
    exit 0
}

Write-Host "API và LocalDB đã sẵn sàng tại $apiUrl. Đang mở ứng dụng quản trị..." -ForegroundColor Green
Set-Location $projectRoot
dotnet run --project ".\Source\Library.Admin.WinForms\Library.Admin.WinForms.csproj"
