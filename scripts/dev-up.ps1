$ErrorActionPreference = "Stop"
docker compose up -d postgres redis
Write-Host ""
Write-Host "PostgreSQL: localhost:5432" -ForegroundColor Green
Write-Host "Redis:      localhost:6379" -ForegroundColor Green
Write-Host ""
Write-Host "Run API:    dotnet run --project backend/src/Api/GiddyEdu.Api.csproj"
Write-Host "Run Web:    cd web; npm run dev"
Write-Host "Family:     cd mobile/family; npx expo start"
Write-Host "Staff:      cd mobile/staff; npx expo start"
