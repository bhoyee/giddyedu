$ErrorActionPreference = "Stop"

Get-Content .env | Where-Object { $_ -and -not $_.StartsWith("#") -and $_.Contains("=") } | ForEach-Object {
    $name, $value = $_ -split "=", 2
    [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), "Process")
}
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=$env:POSTGRES_DB;Username=$env:POSTGRES_USER;Password=$env:POSTGRES_PASSWORD"
$env:ConnectionStrings__Redis = "localhost:6379"

docker compose up -d postgres redis
dotnet tool restore
dotnet ef database update --project backend/src/Infrastructure/GiddyEdu.Infrastructure.csproj --startup-project backend/src/Api/GiddyEdu.Api.csproj
docker compose up -d --build api worker web

$deadline = (Get-Date).AddMinutes(2)
do {
    try { $ready = Invoke-WebRequest -UseBasicParsing http://localhost:8080/health/ready -TimeoutSec 5 } catch { $ready = $null }
    if ($null -ne $ready -and $ready.StatusCode -eq 200) { break }
    Start-Sleep -Seconds 3
} while ((Get-Date) -lt $deadline)

if ($null -eq $ready -or $ready.StatusCode -ne 200) { throw "GiddyEdu API readiness did not become healthy." }

Write-Host ""
Write-Host "PostgreSQL: localhost:5432" -ForegroundColor Green
Write-Host "Redis:      localhost:6379" -ForegroundColor Green
Write-Host "API:        http://localhost:8080" -ForegroundColor Green
Write-Host "Web:        http://localhost:3000" -ForegroundColor Green
Write-Host ""
Write-Host "Family:     cd mobile/family; npx expo start"
Write-Host "Staff:      cd mobile/staff; npx expo start"
