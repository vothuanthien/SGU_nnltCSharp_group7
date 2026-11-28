$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    docker compose -f docker-compose.yml -f docker-compose.web.yml down
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose failed.' }
} finally { Pop-Location }
