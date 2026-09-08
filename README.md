# GiddyEdu

GiddyEdu is a multi-tenant SaaS school management platform designed for
primary and secondary schools, with an initial focus on Nigeria.

The long-term platform contains 47 commercial suites organised into
shared engineering domains.

## Core Stack

### Backend
- .NET 10
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Redis
- Hangfire

### Web
- Next.js
- TypeScript
- Tailwind CSS
- shadcn/ui
- TanStack Query

### Mobile
- React Native
- Expo
- TypeScript

### AI
- Python
- FastAPI

### Infrastructure
- Docker
- Docker Compose
- Caddy
- S3-compatible object storage
- GitHub Actions
- OpenTelemetry

## Architecture

GiddyEdu starts as a modular monolith.

Do not introduce microservices without an approved Architecture Decision Record.

Read AGENTS.md before making changes.

## Initial platform operator

Platform administration is protected by the global `PlatformAdministrator` Identity role. A tenant role, including `Tenant Administrator`, cannot grant platform access.

To provision the first tenantless operator, set `PLATFORMBOOTSTRAP__ENABLED=true` with `PLATFORMBOOTSTRAP__EMAIL` and `PLATFORMBOOTSTRAP__DISPLAYNAME` for one controlled API startup, then disable bootstrap. The operator uses the password-reset email flow to establish their private credential and signs in at `/platform-login`; school registration remains tenant-only. Once any platform administrator exists, subsequent bootstrap attempts are inert.
