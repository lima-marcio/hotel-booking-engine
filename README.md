# Hotel Booking Engine

Portfolio project demonstrating backend architecture, business rules, API
development with ASP.NET Core and Entity Framework Core, and a React
frontend integration.

## Tech Stack

**Backend:** .NET 10, ASP.NET Core Web API, Entity Framework Core, SQLite
(Development), SQL Server (Production), JWT Authentication, Swagger, Serilog.

**Frontend:** React 19, TypeScript, Vite, Tailwind CSS, Axios, React Router,
TanStack Query, React Hook Form, Zod.

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

### Default Credentials (Development)

The database is seeded with one admin account on first run:

- Username: `admin`
- Password: `Admin123!`

### Running Both

Start the backend first, then the frontend, then open the frontend URL in a
browser. The home page calls `GET /api/health` to confirm the two apps are
connected.

## Status

Phase 1, Phase 2 (Authentication), and Phase 3 (Hotels) complete. Next: Phase 4 — Room Types.

## License

MIT — see [LICENSE](./LICENSE).
