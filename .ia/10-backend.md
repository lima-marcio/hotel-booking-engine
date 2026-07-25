# Backend Standards

## Stack
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQLite (Development)
- SQL Server (Production)

## Architecture
- Feature-based folders.
- Controllers contain no business rules.
- Services contain business rules.
- Manual mapping (no AutoMapper).
- Fluent API with IEntityTypeConfiguration.
- JWT Bearer authentication.
- Swagger enabled with JWT.
- Dependency Injection through extension methods.
- Global exception middleware.
- Serilog logging.

## Git
- Initialize Git.
- Generate .gitignore for .NET.

## Build
```bash
dotnet restore
dotnet build
dotnet run
dotnet publish -c Release
```
