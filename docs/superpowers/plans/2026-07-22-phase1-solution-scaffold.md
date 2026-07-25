# Phase 1 - Solution Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the Git repository, backend solution and frontend application skeletons for the Hotel Booking Engine so that Phase 2 (Authentication) can start from a working, demoable base.

**Architecture:** A single Git repository with `backend/` (ASP.NET Core Web API, feature-based folders, EF Core wired for SQLite dev / SQL Server prod, Serilog, global exception middleware, Swagger with a JWT security scheme, DI via extension methods) and `frontend/` (Vite + React 19 + TypeScript, Tailwind, Axios, React Router, TanStack Query, React Hook Form, Zod). A trivial vertical "Health" feature is built end-to-end (backend service + controller + test, frontend page + query) purely to prove the architecture and the two apps can talk to each other before real business features begin in Phase 2+.

**Tech Stack:** .NET 10, ASP.NET Core Web API (controllers), EF Core (Sqlite + SqlServer providers), Serilog.AspNetCore, Swashbuckle.AspNetCore, xUnit, Microsoft.AspNetCore.TestHost — React 19, TypeScript, Vite, Tailwind CSS, Axios, React Router, TanStack Query, React Hook Form, Zod.

## Global Constraints

- .NET 10, ASP.NET Core Web API, Entity Framework Core, SQLite (Development), SQL Server (Production). [10-backend.md]
- Controllers contain no business rules; services contain business rules. [10-backend.md, 30-conventions.md]
- Manual mapping only (no AutoMapper). [10-backend.md]
- Fluent API with `IEntityTypeConfiguration` for EF configuration. [10-backend.md]
- JWT Bearer authentication; Swagger enabled with JWT. [10-backend.md]
- Dependency Injection through extension methods. [10-backend.md]
- Global exception middleware. [10-backend.md]
- Serilog logging. [10-backend.md]
- React 19, TypeScript, Vite, Tailwind CSS, Axios, React Router, TanStack Query, React Hook Form, Zod. [20-frontend.md]
- Frontend folder structure: `src/{api,components,features,hooks,layouts,pages,routes,services,stores,types,utils}`. [20-frontend.md]
- One class/component per file; clear names, no abbreviations; no TODOs; no commented dead code; composition over inheritance; `Program.cs` kept minimal; async/await; Conventional Commits. [30-conventions.md]
- Never hardcode data in the frontend; consume only the backend API. [prompts/project-01.md]
- Ask for confirmation before creating `backend/`/`frontend/` folders (already granted by the user for this plan). [00-project.md]
- Every project must be a Git repository; generate README.md, LICENSE (MIT), .gitignore. [00-project.md, prompts/project-01.md]
- Do not implement Authentication, Hotels, Room Types, Rooms, Guests, Reservations or Dashboard yet — those are later phases. Phase 1 is scaffolding only. [prompts/project-01.md]

---

### Task 1: Initialize Git repository, .gitignore, LICENSE, README skeleton

**Files:**
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `README.md`

**Interfaces:**
- Produces: repository root ready to receive `backend/` and `frontend/` in later tasks.

- [ ] **Step 1: Initialize the repository**

Run: `git init`
Expected: `Initialized empty Git repository in C:/Prototypes/.net/hotel-booking-engine/.git/`

- [ ] **Step 2: Create the root `.gitignore`**

```gitignore
## .NET
bin/
obj/
*.user
*.suo
.vs/
*.db
*.db-shm
*.db-wal

## Node / Frontend
node_modules/
dist/
.env
.env.local
.env.*.local

## Editor / OS
.idea/
.vscode/
*.swp
.DS_Store
Thumbs.db
```

- [ ] **Step 3: Create `LICENSE` (MIT)**

```text
MIT License

Copyright (c) 2026 Marcio

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 4: Create a `README.md` skeleton**

```markdown
# Hotel Booking Engine

Portfolio project demonstrating backend architecture, business rules, API
development with ASP.NET Core and Entity Framework Core, and a React
frontend integration.

## Tech Stack

**Backend:** .NET 10, ASP.NET Core Web API, Entity Framework Core, SQLite
(Development), SQL Server (Production), JWT Authentication, Swagger, Serilog.

**Frontend:** React 19, TypeScript, Vite, Tailwind CSS, Axios, React Router,
TanStack Query, React Hook Form, Zod.

## Status

Work in progress — see `prompts/project-01.md` for the full specification and
`.ai/` for the standards this project follows.

## Getting Started

Setup instructions will be added as the backend and frontend are scaffolded.

## License

MIT — see [LICENSE](./LICENSE).
```

- [ ] **Step 5: Commit**

```bash
git add .gitignore LICENSE README.md
git commit -m "chore: initialize repository with gitignore, license and readme"
```

---

### Task 2: Backend solution and Web API project skeleton

**Files:**
- Create: `backend/HotelBookingEngine.sln`
- Create: `backend/HotelBookingEngine.Api/HotelBookingEngine.Api.csproj`
- Create: `backend/HotelBookingEngine.Api/Program.cs`
- Delete: `backend/HotelBookingEngine.Api/WeatherForecast.cs` (template sample)
- Delete: `backend/HotelBookingEngine.Api/Controllers/WeatherForecastController.cs` (template sample)
- Create: `backend/HotelBookingEngine.Tests/HotelBookingEngine.Tests.csproj`

**Interfaces:**
- Produces: `backend/HotelBookingEngine.Api` (namespace `HotelBookingEngine.Api`), `backend/HotelBookingEngine.Tests` (namespace `HotelBookingEngine.Tests`), both added to `HotelBookingEngine.sln`. Later tasks add files under `Extensions/`, `Middleware/`, `Persistence/`, `Features/` inside the Api project.

- [ ] **Step 1: Create the solution and controller-based Web API project**

```bash
mkdir backend
dotnet new sln -n HotelBookingEngine -o backend
dotnet new webapi -controllers -n HotelBookingEngine.Api -o backend/HotelBookingEngine.Api
```

Expected: both commands succeed and print the created file list. If `-controllers` is not recognized by the installed SDK, run `dotnet new webapi -h` to find the current flag for the controller-based template (older/newer SDKs have used `--use-controllers`) and use that instead.

- [ ] **Step 2: Add the Web API project to the solution**

```bash
dotnet sln backend/HotelBookingEngine.sln add backend/HotelBookingEngine.Api/HotelBookingEngine.Api.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 3: Remove template sample files**

Delete `backend/HotelBookingEngine.Api/WeatherForecast.cs` and `backend/HotelBookingEngine.Api/Controllers/WeatherForecastController.cs`.

If the generated `Program.cs` calls `builder.Services.AddOpenApi()` / `app.MapOpenApi()` (the .NET default-template OpenAPI generator), remove those two lines — this project uses Swashbuckle for full Swagger UI (added in Task 6), not the built-in OpenAPI document generator.

- [ ] **Step 4: Create the test project**

```bash
dotnet new xunit -n HotelBookingEngine.Tests -o backend/HotelBookingEngine.Tests
dotnet sln backend/HotelBookingEngine.sln add backend/HotelBookingEngine.Tests/HotelBookingEngine.Tests.csproj
dotnet add backend/HotelBookingEngine.Tests/HotelBookingEngine.Tests.csproj reference backend/HotelBookingEngine.Api/HotelBookingEngine.Api.csproj
```

Expected: all three commands succeed.

- [ ] **Step 5: Verify the solution builds**

```bash
dotnet build backend/HotelBookingEngine.sln
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "chore: scaffold backend solution with Web API and test projects"
```

---

### Task 3: Serilog logging

**Files:**
- Create: `backend/HotelBookingEngine.Api/Extensions/SerilogHostBuilderExtensions.cs`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`
- Modify: `backend/HotelBookingEngine.Api/HotelBookingEngine.Api.csproj`

**Interfaces:**
- Produces: `SerilogHostBuilderExtensions.ConfigureSerilog(this WebApplicationBuilder builder) : WebApplicationBuilder`, called from `Program.cs`.

- [ ] **Step 1: Add Serilog packages**

```bash
dotnet add backend/HotelBookingEngine.Api package Serilog.AspNetCore
dotnet add backend/HotelBookingEngine.Api package Serilog.Sinks.Console
dotnet add backend/HotelBookingEngine.Api package Serilog.Settings.Configuration
```

- [ ] **Step 2: Create the Serilog extension**

```csharp
using Serilog;

namespace HotelBookingEngine.Api.Extensions;

public static class SerilogHostBuilderExtensions
{
    public static WebApplicationBuilder ConfigureSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();
        });

        return builder;
    }
}
```

- [ ] **Step 3: Call it from `Program.cs`**

At the top of `Program.cs`, right after `var builder = WebApplication.CreateBuilder(args);`, add:

```csharp
builder.ConfigureSerilog();
```

- [ ] **Step 4: Verify logging works**

```bash
dotnet run --project backend/HotelBookingEngine.Api
```

Expected: console output shows Serilog-formatted lines (e.g. `[12:00:00 INF] Now listening on: ...`). Stop the process with Ctrl+C once confirmed.

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat: configure Serilog console logging"
```

---

### Task 4: EF Core persistence (SQLite dev / SQL Server prod)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Persistence/AppDbContext.cs`
- Create: `backend/HotelBookingEngine.Api/Persistence/AppDbContextFactory.cs`
- Create: `backend/HotelBookingEngine.Api/Extensions/PersistenceServiceCollectionExtensions.cs`
- Modify: `backend/HotelBookingEngine.Api/appsettings.json`
- Modify: `backend/HotelBookingEngine.Api/appsettings.Development.json`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`

**Interfaces:**
- Produces: `AppDbContext` (empty for now — first entities arrive in Phase 3), `PersistenceServiceCollectionExtensions.AddPersistence(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) : IServiceCollection`.

- [ ] **Step 1: Add EF Core packages**

```bash
dotnet add backend/HotelBookingEngine.Api package Microsoft.EntityFrameworkCore
dotnet add backend/HotelBookingEngine.Api package Microsoft.EntityFrameworkCore.Sqlite
dotnet add backend/HotelBookingEngine.Api package Microsoft.EntityFrameworkCore.SqlServer
dotnet add backend/HotelBookingEngine.Api package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef
```

`dotnet tool install` may report the tool is already installed — that is fine, continue.

- [ ] **Step 2: Create `AppDbContext`**

```csharp
using Microsoft.EntityFrameworkCore;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
```

- [ ] **Step 3: Create the design-time factory (so `dotnet ef` works without full DI startup)**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HotelBookingEngine.Api.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=hotelbookingengine.dev.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}
```

- [ ] **Step 4: Add connection strings**

In `backend/HotelBookingEngine.Api/appsettings.json`, add:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=HotelBookingEngine;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Cors": {
    "AllowedOrigins": []
  }
}
```

In `backend/HotelBookingEngine.Api/appsettings.Development.json`, add:

```json
{
  "ConnectionStrings": {
    "Sqlite": "Data Source=hotelbookingengine.dev.db"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

- [ ] **Step 5: Create the persistence extension**

```csharp
using Microsoft.EntityFrameworkCore;
using HotelBookingEngine.Api.Persistence;

namespace HotelBookingEngine.Api.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            if (environment.IsDevelopment())
            {
                options.UseSqlite(configuration.GetConnectionString("Sqlite"));
            }
            else
            {
                options.UseSqlServer(configuration.GetConnectionString("SqlServer"));
            }
        });

        return services;
    }
}
```

- [ ] **Step 6: Wire it into `Program.cs`**

After `builder.ConfigureSerilog();`, add:

```csharp
builder.Services.AddPersistence(builder.Configuration, builder.Environment);
```

- [ ] **Step 7: Verify migrations work against the (currently empty) model**

```bash
dotnet ef migrations add InitialCreate --project backend/HotelBookingEngine.Api --output-dir Persistence/Migrations
dotnet build backend/HotelBookingEngine.sln
```

Expected: migration files are generated under `backend/HotelBookingEngine.Api/Persistence/Migrations/` and the build succeeds.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat: configure EF Core with SQLite (dev) and SQL Server (prod)"
```

---

### Task 5: Global exception handling middleware

**Files:**
- Create: `backend/HotelBookingEngine.Api/Middleware/ExceptionHandlingMiddleware.cs`
- Create: `backend/HotelBookingEngine.Api/Extensions/ApplicationBuilderExtensions.cs`
- Create: `backend/HotelBookingEngine.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`
- Modify: `backend/HotelBookingEngine.Tests/HotelBookingEngine.Tests.csproj`

**Interfaces:**
- Produces: `ExceptionHandlingMiddleware`, `ApplicationBuilderExtensions.UseGlobalExceptionHandling(this IApplicationBuilder app) : IApplicationBuilder`.

- [ ] **Step 1: Add the TestHost package to the test project**

```bash
dotnet add backend/HotelBookingEngine.Tests package Microsoft.AspNetCore.TestHost
```

- [ ] **Step 2: Write the failing test**

```csharp
using System.Net;
using System.Text.Json;
using HotelBookingEngine.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HotelBookingEngine.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_Returns500WithJsonErrorBody()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services.AddLogging());
                webHost.Configure(app =>
                {
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.Run(_ => throw new InvalidOperationException("boom"));
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("message").GetString());
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter ExceptionHandlingMiddlewareTests`
Expected: FAIL (build error — `HotelBookingEngine.Api.Middleware.ExceptionHandlingMiddleware` does not exist yet).

- [ ] **Step 4: Implement the middleware**

```csharp
using System.Net;
using System.Text.Json;

namespace HotelBookingEngine.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Path}", context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var payload = JsonSerializer.Serialize(new { message = "An unexpected error occurred." });
            await context.Response.WriteAsync(payload);
        }
    }
}
```

- [ ] **Step 5: Create the `IApplicationBuilder` extension**

```csharp
using HotelBookingEngine.Api.Middleware;

namespace HotelBookingEngine.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter ExceptionHandlingMiddlewareTests`
Expected: PASS (1 passed).

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: add global exception handling middleware"
```

---

### Task 6: Swagger with JWT security scheme

**Files:**
- Create: `backend/HotelBookingEngine.Api/Extensions/SwaggerServiceCollectionExtensions.cs`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`

**Interfaces:**
- Produces: `SwaggerServiceCollectionExtensions.AddSwaggerWithJwt(this IServiceCollection services) : IServiceCollection`.

- [ ] **Step 1: Confirm Swashbuckle is present**

`Swashbuckle.AspNetCore` ships with the `webapi -controllers` template. Run `dotnet list backend/HotelBookingEngine.Api package` and confirm it's listed; if absent, add it with `dotnet add backend/HotelBookingEngine.Api package Swashbuckle.AspNetCore`.

- [ ] **Step 2: Create the Swagger extension**

```csharp
using Microsoft.OpenApi.Models;

namespace HotelBookingEngine.Api.Extensions;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Hotel Booking Engine API",
                Version = "v1"
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a valid JWT token.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });
        });

        return services;
    }
}
```

- [ ] **Step 3: Wire it into `Program.cs`**

Replace any existing `builder.Services.AddSwaggerGen();` call (from the template) with:

```csharp
builder.Services.AddSwaggerWithJwt();
```

Ensure the middleware pipeline still has, inside the `if (app.Environment.IsDevelopment())` block:

```csharp
app.UseSwagger();
app.UseSwaggerUI();
```

- [ ] **Step 4: Verify Swagger UI loads**

```bash
dotnet run --project backend/HotelBookingEngine.Api
```

Open the printed HTTPS URL + `/swagger` in a browser. Expected: Swagger UI loads, title reads "Hotel Booking Engine API", and an "Authorize" button is present. Stop the process once confirmed.

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat: configure Swagger with JWT bearer security scheme"
```

---

### Task 7: Health feature (backend vertical slice)

**Files:**
- Create: `backend/HotelBookingEngine.Api/Features/Health/HealthStatus.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Health/IHealthService.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Health/HealthService.cs`
- Create: `backend/HotelBookingEngine.Api/Features/Health/HealthController.cs`
- Create: `backend/HotelBookingEngine.Api/Extensions/ApplicationServicesCollectionExtensions.cs`
- Create: `backend/HotelBookingEngine.Tests/Features/Health/HealthServiceTests.cs`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks beyond DI conventions already established.
- Produces: `GET /api/health` returning `{ "status": "Healthy", "checkedAtUtc": "<ISO-8601>" }`; `ApplicationServicesCollectionExtensions.AddApplicationServices(this IServiceCollection services) : IServiceCollection`.

- [ ] **Step 1: Write the failing test**

```csharp
using HotelBookingEngine.Api.Features.Health;
using Xunit;

namespace HotelBookingEngine.Tests.Features.Health;

public class HealthServiceTests
{
    [Fact]
    public void GetStatus_ReturnsHealthyStatusWithCurrentUtcTimestamp()
    {
        var sut = new HealthService();
        var before = DateTime.UtcNow;

        var result = sut.GetStatus();

        var after = DateTime.UtcNow;
        Assert.Equal("Healthy", result.Status);
        Assert.InRange(result.CheckedAtUtc, before, after);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HealthServiceTests`
Expected: FAIL (build error — `HealthService` and `HotelBookingEngine.Api.Features.Health` do not exist yet).

- [ ] **Step 3: Implement the Health feature**

```csharp
namespace HotelBookingEngine.Api.Features.Health;

public record HealthStatus(string Status, DateTime CheckedAtUtc);
```

```csharp
namespace HotelBookingEngine.Api.Features.Health;

public interface IHealthService
{
    HealthStatus GetStatus();
}
```

```csharp
namespace HotelBookingEngine.Api.Features.Health;

public class HealthService : IHealthService
{
    public HealthStatus GetStatus()
    {
        return new HealthStatus("Healthy", DateTime.UtcNow);
    }
}
```

```csharp
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingEngine.Api.Features.Health;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public ActionResult<HealthStatus> Get()
    {
        return Ok(_healthService.GetStatus());
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test backend/HotelBookingEngine.Tests --filter HealthServiceTests`
Expected: PASS (1 passed).

- [ ] **Step 5: Register the service via a DI extension method**

```csharp
using HotelBookingEngine.Api.Features.Health;

namespace HotelBookingEngine.Api.Extensions;

public static class ApplicationServicesCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IHealthService, HealthService>();

        return services;
    }
}
```

In `Program.cs`, after `builder.Services.AddPersistence(...)`, add:

```csharp
builder.Services.AddApplicationServices();
```

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat: add Health feature vertical slice"
```

---

### Task 8: CORS policy and final Program.cs assembly

**Files:**
- Create: `backend/HotelBookingEngine.Api/Extensions/CorsServiceCollectionExtensions.cs`
- Modify: `backend/HotelBookingEngine.Api/Program.cs`

**Interfaces:**
- Produces: `CorsServiceCollectionExtensions.AddFrontendCorsPolicy(this IServiceCollection services, IConfiguration configuration) : IServiceCollection` and constant `CorsServiceCollectionExtensions.FrontendPolicyName`.
- Consumes: `Cors:AllowedOrigins` from `appsettings.Development.json` (Task 4).

- [ ] **Step 1: Create the CORS extension**

```csharp
namespace HotelBookingEngine.Api.Extensions;

public static class CorsServiceCollectionExtensions
{
    public const string FrontendPolicyName = "FrontendPolicy";

    public static IServiceCollection AddFrontendCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendPolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
```

- [ ] **Step 2: Assemble the final `Program.cs`**

```csharp
using HotelBookingEngine.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddPersistence(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();
builder.Services.AddFrontendCorsPolicy(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseCors(CorsServiceCollectionExtensions.FrontendPolicyName);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
```

(The trailing `public partial class Program { }` makes the entry point reachable if a future integration test needs `WebApplicationFactory<Program>`.)

- [ ] **Step 3: Verify the full backend pipeline**

```bash
dotnet build backend/HotelBookingEngine.sln
dotnet test backend/HotelBookingEngine.sln
dotnet run --project backend/HotelBookingEngine.Api
```

In another terminal, note the HTTP URL from the run output (also visible in `backend/HotelBookingEngine.Api/Properties/launchSettings.json` under the `http` profile's `applicationUrl`) and run:

```bash
curl http://localhost:<port>/api/health
```

Expected: `{"status":"Healthy","checkedAtUtc":"..."}`. Stop the running process once confirmed.

- [ ] **Step 4: Commit**

```bash
git add backend
git commit -m "feat: wire CORS policy and assemble final Program.cs pipeline"
```

---

### Task 9: Frontend scaffold (Vite + React 19 + TypeScript + Tailwind)

**Files:**
- Create: `frontend/` (Vite `react-ts` template output)
- Modify: `frontend/tailwind.config.js`
- Modify: `frontend/src/index.css`
- Delete: `frontend/src/App.tsx`, `frontend/src/App.css` (unused template files)

**Interfaces:**
- Produces: a buildable Vite React TS app with Tailwind enabled and all runtime dependencies installed, ready for Task 10 to wire.

- [ ] **Step 1: Scaffold the Vite app**

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
```

- [ ] **Step 2: Install Tailwind**

```bash
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

- [ ] **Step 3: Configure Tailwind content paths**

Edit `frontend/tailwind.config.js`:

```javascript
/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {},
  },
  plugins: [],
};
```

- [ ] **Step 4: Add Tailwind directives**

Replace the contents of `frontend/src/index.css` with:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

- [ ] **Step 5: Install runtime dependencies**

```bash
npm install axios react-router-dom @tanstack/react-query react-hook-form zod
```

- [ ] **Step 6: Remove unused template files**

Delete `frontend/src/App.tsx` and `frontend/src/App.css` (Task 10 replaces the app shell with `routes/AppRoutes.tsx`).

- [ ] **Step 7: Verify the project builds**

```bash
npm run build
```

Expected: build succeeds (it is fine that `main.tsx` still imports the now-deleted `App.tsx` at this point — Task 10 fixes `main.tsx`; if `npm run build` fails here because of that dangling import, proceed directly to Task 10 before treating this as a blocker).

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "chore: scaffold frontend with Vite, React 19, TypeScript and Tailwind"
```

---

### Task 10: Wire Axios, TanStack Query, React Router and a Home page consuming the Health endpoint

**Files:**
- Create: `frontend/src/api/httpClient.ts`
- Create: `frontend/src/types/health.ts`
- Create: `frontend/src/features/health/healthService.ts`
- Create: `frontend/src/pages/HomePage.tsx`
- Create: `frontend/src/routes/AppRoutes.tsx`
- Modify: `frontend/src/main.tsx`
- Create: `frontend/.env.development`

**Interfaces:**
- Consumes: backend `GET /api/health` from Task 7/8, returning `{ status: string; checkedAtUtc: string }` (ASP.NET Core's default camelCase JSON policy matches the C# `HealthStatus` record properties).
- Produces: rendered Home page showing live API status at `/`.

- [ ] **Step 1: Record the backend's dev URL**

Open `backend/HotelBookingEngine.Api/Properties/launchSettings.json` and note the `http` profile's `applicationUrl` (e.g. `http://localhost:5137`).

- [ ] **Step 2: Create the environment file**

```dotenv
VITE_API_BASE_URL=http://localhost:5137
```

Save as `frontend/.env.development`, using the actual port noted in Step 1. This file contains no secrets (just a local dev URL) and is intentionally committed.

- [ ] **Step 3: Create the Axios client**

```typescript
import axios from "axios";

export const httpClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});
```

Save as `frontend/src/api/httpClient.ts`.

- [ ] **Step 4: Create the Health type**

```typescript
export interface HealthStatus {
  status: string;
  checkedAtUtc: string;
}
```

Save as `frontend/src/types/health.ts`.

- [ ] **Step 5: Create the Health service**

```typescript
import { httpClient } from "../../api/httpClient";
import type { HealthStatus } from "../../types/health";

export async function fetchHealthStatus(): Promise<HealthStatus> {
  const response = await httpClient.get<HealthStatus>("/api/health");
  return response.data;
}
```

Save as `frontend/src/features/health/healthService.ts`.

- [ ] **Step 6: Create the Home page**

```typescript
import { useQuery } from "@tanstack/react-query";
import { fetchHealthStatus } from "../features/health/healthService";

export function HomePage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["health"],
    queryFn: fetchHealthStatus,
  });

  if (isLoading) {
    return <p>Checking API status...</p>;
  }

  if (isError || !data) {
    return <p>Unable to reach the API.</p>;
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2">
      <h1 className="text-2xl font-semibold">Hotel Booking Engine</h1>
      <p>API status: {data.status}</p>
    </main>
  );
}
```

Save as `frontend/src/pages/HomePage.tsx`.

- [ ] **Step 7: Create the route table**

```typescript
import { Routes, Route } from "react-router-dom";
import { HomePage } from "../pages/HomePage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
    </Routes>
  );
}
```

Save as `frontend/src/routes/AppRoutes.tsx`.

- [ ] **Step 8: Rewrite `main.tsx`**

```typescript
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AppRoutes } from "./routes/AppRoutes";
import "./index.css";

const queryClient = new QueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
```

Save as `frontend/src/main.tsx` (this replaces the previous `<App />`-based content and removes the dangling import from Task 9).

- [ ] **Step 9: Verify the build**

```bash
npm run build
```

Expected: build succeeds with no TypeScript errors.

- [ ] **Step 10: Verify end-to-end manually**

Terminal 1: `dotnet run --project backend/HotelBookingEngine.Api`
Terminal 2: `cd frontend && npm run dev`

Open the printed Vite URL (e.g. `http://localhost:5173`). Expected: page renders "Hotel Booking Engine" and "API status: Healthy". Stop both processes once confirmed.

- [ ] **Step 11: Commit**

```bash
git add frontend
git commit -m "feat: wire Axios, TanStack Query and React Router to the Health endpoint"
```

---

### Task 11: README setup instructions

**Files:**
- Modify: `README.md`

**Interfaces:**
- None (documentation only).

- [ ] **Step 1: Replace the "Getting Started" section**

```markdown
## Getting Started

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project HotelBookingEngine.Api
```

The API listens on the URL printed in the console (also defined in
`HotelBookingEngine.Api/Properties/launchSettings.json`). Swagger UI is
available at `/swagger` in Development.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

The app runs at `http://localhost:5173` and expects the backend URL in
`frontend/.env.development` (`VITE_API_BASE_URL`) to match the backend's
actual port.

### Running Both

Start the backend first, then the frontend, then open the frontend URL in a
browser. The home page calls `GET /api/health` to confirm the two apps are
connected.
```

- [ ] **Step 2: Update the "Status" section**

Replace the "Status" section body with:

```markdown
Phase 1 (solution structure, backend, frontend) complete. Next: Phase 2 —
Authentication.
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add backend and frontend setup instructions"
```

---

## Self-Review Notes

- **Spec coverage:** Git repo + README + LICENSE + .gitignore (Task 1); backend solution structure, feature-based folders, controllers with no business rules, manual mapping (nothing to map yet), Fluent API convention established (no entities yet, deferred to Phase 3 per MVP order), JWT-ready Swagger, SQLite/SqlServer, global exception middleware, DI via extension methods, Serilog (Tasks 2–8); frontend stack, folder structure, no hardcoded data — Home page reads live data from the API (Tasks 9–10); README setup instructions (Task 11). Authentication, Hotels, Room Types, Rooms, Guests, Reservations, Dashboard are explicitly out of scope for Phase 1 per the MVP Development Order and are not touched.
- **Placeholder scan:** no TODO/TBD markers; all code blocks are complete and runnable as written.
- **Type consistency:** `HealthStatus` (C# record: `Status`, `CheckedAtUtc`) matches the TypeScript `HealthStatus` interface (`status`, `checkedAtUtc`) via ASP.NET Core's default camelCase JSON policy. `IHealthService`/`HealthService` names are used consistently across Tasks 7 and 8.
