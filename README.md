
# Logibooks Core

[![ci](https://github.com/maxirmx/logibooks.core/actions/workflows/ci.yml/badge.svg)](https://github.com/maxirmx/logibooks.core/actions/workflows/ci.yml)
[![publish](https://github.com/maxirmx/logibooks.core/actions/workflows/publish.yml/badge.svg)](https://github.com/maxirmx/logibooks.core/actions/workflows/publish.yml)
[![codecov](https://codecov.io/gh/maxirmx/logibooks.core/branch/main/graph/badge.svg)](https://codecov.io/gh/maxirmx/logibooks.core)

This is the core backend service for the Logibooks logistics system. It provides a RESTful API over a PostgreSQL database using Entity Framework Core.

## Technologies

- ASP.NET Core 7.0
- PostgreSQL
- Entity Framework Core
- Docker & Docker Compose
- Swagger UI (for API docs)

### Default Roles

- Logist (`Логист`)
- Administrator (`Администратор`)

## Getting Started

### Prerequisites

- Docker & Docker Compose
- Visual Studio 2022+ (optional for local dev)

### Run with Docker

```bash
docker-compose up --build
```

### Access

- API: https://localhost:8081/api/sample
- Swagger UI: https://localhost:8081/swagger

The HTTPS certificate should be placed in the `https` folder next to
`docker-compose.yml` as `aspnetapp.pfx`. This folder is mounted into the
container and the password for the certificate is set to `changeit` in
`docker-compose.yml`.

### EF Core Migrations

```bash
dotnet ef migrations add InitialCreate --project Logibooks.Core
dotnet ef database update --project Logibooks.Core
```

---

## License

MIT
