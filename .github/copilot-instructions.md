# Logibooks Core

Logibooks Core is an ASP.NET Core 8.0 RESTful API backend service for a logistics system. It provides parcel tracking, user management, register processing, and scheduled jobs over a PostgreSQL database using Entity Framework Core.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Prerequisites and Environment Setup
- .NET 8.0 SDK (command: `dotnet --version` should show 8.0.x)
- Docker & Docker Compose (for full application stack)
- PostgreSQL client tools (optional, for database debugging)

### Bootstrap, Build, and Test the Repository
**CRITICAL: NEVER CANCEL builds or tests. Set timeouts to 60+ minutes for builds and 30+ minutes for tests.**

```bash
# Navigate to repository root
cd /home/runner/work/logibooks.core/logibooks.core

# Restore NuGet packages - takes ~25 seconds
# Option 1: Solution-level (recommended for local development)
dotnet restore Logibooks.sln

# Option 2: Project-level (matches CI workflow)  
dotnet restore Logibooks.Core.Tests/Logibooks.Core.Tests.csproj

# Build the solution - takes ~20 seconds  
# Option 1: Solution-level (builds all projects)
dotnet build Logibooks.sln --no-restore --configuration Release

# Option 2: Project-level (matches CI, builds dependencies automatically)
dotnet build Logibooks.Core.Tests/Logibooks.Core.Tests.csproj --no-restore --configuration Release

# Run tests - takes ~75 seconds, NEVER CANCEL. Set timeout to 30+ minutes.
# Option 1: Solution-level
dotnet test Logibooks.sln --configuration Release --verbosity normal

# Option 2: Project-level (matches CI)
dotnet test Logibooks.Core.Tests/Logibooks.Core.Tests.csproj --configuration Release --verbosity normal
```

### Docker Deployment (Full Application Stack)
**CRITICAL: NEVER CANCEL Docker builds. Set timeout to 60+ minutes.**

```bash
# Build all services - takes 5-15 minutes depending on network. NEVER CANCEL.
docker compose -f docker-compose.yml build

# If build fails with NU1301 (NuGet connectivity), try:
# - Check internet connectivity: curl -I https://api.nuget.org
# - Retry the build command
# - Use validation Option B for newman testing without Docker

# Start all services (PostgreSQL, Adminer, API)
docker compose -f docker-compose.yml up -d

# Wait for API to be ready - takes 30-60 seconds
for i in {1..20}; do
  if curl -fs http://localhost:8080/swagger > /dev/null; then
    echo "API is ready" && break
  fi
  sleep 5
done

# Verify API is accessible
curl -fs http://localhost:8080/swagger

# Run newman collection against running API (complete validation)
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000

# Stop services when done
docker compose -f docker-compose.yml down
```

### Access Points
- **API Base URL**: http://localhost:8080/
- **API with HTTPS**: https://localhost:8081/ (with valid certificates)
- **Swagger UI**: http://localhost:8080/swagger
- **Database Admin (Adminer)**: http://localhost:8082 (when using docker-compose)
  - Server: `db`, Username: `postgres`, Password: `postgres`, Database: `logibooks`

## Validation and Testing

### ALWAYS Run Complete Validation After Changes
**MANUAL VALIDATION REQUIREMENT**: After building and running the application, you MUST test actual functionality by running through complete user scenarios.

#### Essential Validation Steps
1. **Build Validation**: `dotnet build` must succeed with 0 errors, 0 warnings
2. **Test Validation**: All 589 tests must pass (expect ~75 seconds runtime)
3. **API Validation**: Swagger UI must load at http://localhost:8080/swagger
4. **Database Validation**: Migrations must apply successfully on startup

#### Required Validation Commands
**CRITICAL: Run these commands to validate any changes:**

```bash
# 1. Run test project (MUST pass - all 589 tests)
dotnet test Logibooks.Core.Tests/Logibooks.Core.Tests.csproj --configuration Release --verbosity normal
# Expected: "Test Run Successful. Total tests: 589, Passed: 589"

# 2. Run Postman collection with Docker setup (comprehensive API validation)
# Option A: Full Docker setup (recommended when Docker networking works)
docker compose -f docker-compose.yml up -d

# Wait for API to be ready (takes 30-60 seconds)
for i in {1..20}; do
  if curl -fs http://localhost:8080/swagger > /dev/null; then
    echo "API is ready" && break
  fi
  sleep 5
done

# Run newman collection (should pass with running API)
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000 --bail
# Expected: All API tests pass successfully

# Cleanup
docker compose -f docker-compose.yml down

# Option B: If Docker build fails with NU1301 (network issues), validate commands syntax only
echo "Docker networking issue detected (NU1301). Validating newman command syntax..."
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000 --bail --dry-run 2>/dev/null || \
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000 --bail
# Expected: Command syntax validation (will show ECONNREFUSED - this is expected without running API)
echo "Newman command syntax validated. Run with Docker when networking issues are resolved."
```

#### Complete User Scenarios to Test
After making ANY changes, ALWAYS test these workflows:

**Scenario 1: API Health Check**
```bash
# Start API with Docker and test basic connectivity
docker compose up -d
sleep 30
curl -X GET "http://localhost:8080/api/status/status" -H "accept: application/json"
# Expected: 200 OK response with status information
```

**Scenario 2: Authentication Flow**
```bash
# Test authentication endpoint (should fail without credentials)
curl -X POST "http://localhost:8080/api/auth/check" -H "accept: application/json"
# Expected: 401 Unauthorized

# Test Swagger UI accessibility
curl -s http://localhost:8080/swagger | grep -i "swagger" > /dev/null && echo "Swagger UI accessible"
```

**Scenario 3: Database Migration and Connectivity Test**
```bash
# Check API logs for successful startup and migrations
docker compose logs api --tail=20 | grep -i "migration\|database\|started"
# Expected: No database connection errors, migrations applied successfully
```

**Scenario 4: Full Postman Test Suite with Docker Setup**
```bash
# Complete integration test with Docker services
docker compose -f docker-compose.yml up -d

# Wait for API readiness (automated check)
for i in {1..20}; do
  if curl -fs http://localhost:8080/swagger > /dev/null; then
    echo "API is ready for testing" && break
  fi
  sleep 5
done

# Run comprehensive API tests via Postman collection
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000
# Expected: All critical API endpoints respond correctly

# Cleanup after testing
docker compose -f docker-compose.yml down
```

#### Use Postman Collection for Full API Testing
```bash
# Install newman (if available)
npm install -g newman

# Option 1: Run with Docker setup (recommended for complete validation)
docker compose -f docker-compose.yml up -d
for i in {1..20}; do
  if curl -fs http://localhost:8080/swagger > /dev/null; then break; fi
  sleep 5
done
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000 --bail
docker compose -f docker-compose.yml down

# Option 2: Run against manually started API (requires API to be running separately)
newman run tests/postman.json --global-var "base_url=http://localhost:8080" --timeout 5000
# Note: This will fail with "ECONNREFUSED" if API is not running
```

### Known Issues and Workarounds
- **Docker Network Issues (NU1301)**: If `docker compose build` fails with NuGet connectivity errors (NU1301), this indicates Docker networking issues in the environment. The validation commands are designed to handle this:
  - Option A: Retry `docker compose build` after checking network connectivity
  - Option B: Use validation Option B which validates newman command syntax without requiring Docker
  - The newman command will show "ECONNREFUSED" when API is not running - this is expected behavior
- **Database Connection**: The API expects PostgreSQL at `db:5432`. Without Docker, update connection string in `appsettings.json` to point to local PostgreSQL instance.
- **HTTPS Certificates**: HTTPS endpoints require valid certificates. Use HTTP endpoints (port 8080) for local development and testing.
- **Solution Build Commands**: Always specify `Logibooks.sln` when running dotnet commands to avoid "multiple project" errors.

## Architecture and Code Navigation

### Project Structure
```
Logibooks.Core/              # Main ASP.NET Core API project
├── Controllers/              # 16 API controllers (Auth, Parcels, Users, etc.)
├── Data/                     # Entity Framework DbContext and models
├── Services/                 # Business logic and background jobs
├── Migrations/               # EF Core database migrations
├── Authorization/            # JWT authentication logic
├── Models/                   # Domain models and DTOs
└── Program.cs               # Application startup and configuration

Logibooks.Core.Tests/        # NUnit test project (589 tests)
├── Controllers/             # Controller unit tests
├── Services/                # Service unit tests  
└── test.data/              # Test data files (Excel, zip files)

docker-compose.yml           # Multi-service Docker setup
.github/workflows/           # CI/CD pipelines
└── ci.yml                  # Build, test, and Docker validation
└── publish.yml             # Container image publishing
```

### Key Controllers and Their Purpose
- **AuthController**: JWT authentication and user validation
- **ParcelsController**: Parcel tracking and validation logic
- **RegistersController**: Excel register upload and processing
- **UsersController**: User management and roles
- **StatusController**: Health check and API status

### Database and Migrations
- **Primary Database**: PostgreSQL with Entity Framework Core
- **Migrations**: Located in `Logibooks.Core/Migrations/`
- **Seeded Data**: Users, roles, countries, and reference data
- **Connection String**: Configured in `appsettings.json` for Docker environment

### Background Jobs (Quartz Scheduler)
- **UpdateCountriesJob**: Runs daily at 3:05 AM (`0 0 3 5 * ?`)
- **UpdateFeacnCodesJob**: Runs daily at 4:00 AM (`0 0 4 * * ?`)

## Common Tasks and Commands

### Build Commands with Timing Expectations
```bash
# Full clean build - takes ~45 seconds total. NEVER CANCEL.
dotnet clean Logibooks.sln
dotnet restore Logibooks.sln
dotnet build Logibooks.sln --configuration Release

# Test with coverage - takes ~90 seconds. NEVER CANCEL.
dotnet test Logibooks.sln --configuration Release --collect:"XPlat Code Coverage"
```

### Development Workflow
```bash
# Start development with file watching (if needed)
dotnet watch run --project Logibooks.Core

# Run specific test class
dotnet test --filter "FullyQualifiedName~ParcelsControllerTests"

# Check EF migrations status (requires database connection)
dotnet ef migrations list --project Logibooks.Core
```

### Debugging and Troubleshooting
```bash
# Check application configuration
cat Logibooks.Core/appsettings.json

# View recent Docker logs
docker compose logs api --tail=50

# Check database connectivity (when PostgreSQL is running)
psql -h localhost -U postgres -d logibooks -c "SELECT 1;"
```

## CI/CD Integration

### GitHub Actions Workflows
- **ci.yml**: Runs on every push/PR - builds, tests, creates Docker images (~5-10 minutes)
- **publish.yml**: Runs on version tags - publishes to GitHub Container Registry

### Pre-commit Validation
Before committing changes, ALWAYS run:
```bash
# NEVER CANCEL: Full validation takes ~2 minutes
dotnet restore Logibooks.sln
dotnet build Logibooks.sln --configuration Release  
dotnet test Logibooks.sln --configuration Release --verbosity normal
```

### Expected CI Timing
- **Restore & Build**: 2-3 minutes
- **Test Execution**: 2-4 minutes  
- **Docker Build**: 5-10 minutes
- **Total CI Time**: 10-15 minutes typical

## Frequently Referenced Information

### Solution File Structure
```
Logibooks.sln
├── Logibooks.Core (Main API)
├── Logibooks.Core.Tests (Test Project)  
└── docker-compose (Docker project)
```

### Package Dependencies (Key Libraries)
- **Microsoft.EntityFrameworkCore**: 9.0.7 (Database ORM)
- **Npgsql.EntityFrameworkCore.PostgreSQL**: 9.0.4 (PostgreSQL provider)
- **AutoMapper**: 15.0.1 (Object mapping)
- **Quartz**: 3.14.0 (Job scheduling)
- **Swashbuckle.AspNetCore**: 9.0.3 (Swagger documentation)
- **NUnit**: 4.3.2 (Testing framework)

### Configuration Files
- **appsettings.json**: Database connection, logging, JWT settings
- **docker-compose.yml**: Multi-service deployment configuration
- **Logibooks.Core.csproj**: Main project dependencies and settings
- **mapping/*.yaml**: Excel import mapping configurations

## Error Patterns and Solutions

### Build Errors
- **NU1301 NuGet errors**: Network connectivity issues, retry the command
- **EF migration errors**: Database connection or schema issues
- **JWT configuration errors**: Check AppSettings.Secret configuration

### Runtime Errors  
- **Database connection errors**: Ensure PostgreSQL is running and accessible
- **Authentication failures**: Verify JWT token configuration and expiration
- **Port binding errors**: Check if ports 8080/8081 are available

### Test Failures
- **Database tests**: Often use in-memory database, check test isolation
- **Controller tests**: Verify mock setup and HTTP context configuration
- **Integration tests**: May require database cleanup between tests

---

**CRITICAL REMINDERS:**
- **NEVER CANCEL builds or tests** - they may take 5-15 minutes in some environments
- **ALWAYS validate manually** after code changes via complete user scenarios  
- **Set explicit timeouts of 60+ minutes** for build commands and 30+ minutes for tests
- **Docker builds can take 15+ minutes** depending on network conditions