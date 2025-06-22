
# Logibooks Core

[![CI](https://github.com/maxirmx/logibooks.core/actions/workflows/ci.yml/badge.svg)](https://github.com/maxirmx/logibooks.core/actions/workflows/ci.yml)
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

- API: http://localhost:8080/api/sample
- Swagger UI: http://localhost:8080/swagger

### EF Core Migrations

```bash
dotnet ef migrations add InitialCreate --project Logibooks.Core
dotnet ef database update --project Logibooks.Core
```

---

## License

MIT
