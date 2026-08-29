$ErrorActionPreference = "Stop"

Write-Host "Verifying GiddyEdu backend..." -ForegroundColor Cyan
dotnet restore backend/GiddyEdu.slnx
dotnet build backend/GiddyEdu.slnx --configuration Release --no-restore
dotnet test backend/GiddyEdu.slnx --configuration Release --no-build

Write-Host "Verifying GiddyEdu web..." -ForegroundColor Cyan
Push-Location web
npm ci
npm run lint
npm run build
Pop-Location

Write-Host "Verifying Family mobile app..." -ForegroundColor Cyan
Push-Location mobile/family
npm ci
npx tsc --noEmit
Pop-Location

Write-Host "Verifying Staff mobile app..." -ForegroundColor Cyan
Push-Location mobile/staff
npm ci
npx tsc --noEmit
Pop-Location

Write-Host "All configured checks passed." -ForegroundColor Green
