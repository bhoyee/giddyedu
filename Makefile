setup:
	dotnet restore backend/GiddyEdu.slnx
	cd web && npm ci
	cd mobile/family && npm ci
	cd mobile/staff && npm ci

infra-up:
	docker compose up -d postgres redis

infra-down:
	docker compose down

up:
	docker compose up -d --build

down:
	docker compose down

backend-build:
	dotnet build backend/GiddyEdu.slnx

backend-test:
	dotnet test backend/GiddyEdu.slnx

web:
	cd web && npm run dev

family:
	cd mobile/family && npx expo start

staff:
	cd mobile/staff && npx expo start

verify:
	powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
