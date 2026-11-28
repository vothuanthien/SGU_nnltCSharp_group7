$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($PSScriptRoot).TrimEnd('\').TrimEnd(':')) -ErrorAction SilentlyContinue
    if ($drive -and $drive.Free -lt 5GB) {
        throw 'At least 5 GB free space is required on the project drive before building. Free disk space and restart Docker Desktop if it reported read-only filesystem errors.'
    }
    if (!(Test-Path .env)) {
        $sqlPassword = 'Local!' + [Guid]::NewGuid().ToString('N') + '9a'
        @("MSSQL_SA_PASSWORD=$sqlPassword", 'ADMIN_PASSWORD=Admin@12345') | Set-Content .env
    }
    docker compose -f docker-compose.yml -f docker-compose.web.yml up -d --build
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose failed. Inspect the Docker error above and check free disk space and Docker Desktop.' }
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $health = Invoke-RestMethod 'http://localhost:5001/health/db' -TimeoutSec 3
            $web = Invoke-WebRequest 'http://localhost:5002' -UseBasicParsing -TimeoutSec 5
            if ($health.database -eq 'connected' -and $web.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    if (!$ready) { throw 'Services are not ready. Run docker compose -f docker-compose.yml -f docker-compose.web.yml logs --tail 100' }
    Write-Host 'Web: http://localhost:5002 | API: http://localhost:5001/health/db'
} finally { Pop-Location }
