# Project 01 - Hotel Booking Engine

You will develop the first project of my software engineering portfolio.

This project must follow all standards defined in the AI documentation located in the `.ai` folder.

Mandatory documents:

* `.ai/00-project.md`
* `.ai/10-backend.md`
* `.ai/20-frontend.md`
* `.ai/30-conventions.md`

Read and follow every applicable rule before generating any code.

---

# Project

Develop a complete Hotel Booking Engine.

The objective is to demonstrate backend architecture, business rules, API development, Entity Framework Core, React frontend integration and software engineering best practices.

This project is intended for portfolio purposes.

The focus is delivering a production-quality MVP before implementing optional features.

---

# Development Philosophy

* MVP First.
* Finish one feature completely before starting another.
* Reuse existing code whenever possible.
* Never duplicate functionality.
* Keep the code simple, readable and maintainable.
* Behave as a Senior Software Engineer.
* Do not interrupt for routine implementation decisions.
* Ask for confirmation only when a decision changes the project scope or requires external dependencies.

---

# Technology Stack

Backend

* .NET 10
* ASP.NET Core Web API
* Entity Framework Core
* SQLite (Development)
* SQL Server (Production)
* JWT Authentication
* Swagger

Frontend

* React 19
* TypeScript
* Vite
* Tailwind CSS
* Axios
* React Router
* TanStack Query
* React Hook Form
* Zod

---

# Project Structure

After user confirmation, create the following folders:

backend/

frontend/

Each folder must follow the standards defined in the AI documentation.

---

# MVP Scope

The first version must support:

Hotels

* Create Hotel
* Update Hotel
* Delete Hotel
* List Hotels

Room Types

* Create
* Update
* Delete
* List

Rooms

* Room Number
* Room Type
* Capacity
* Status
* Daily Rate

Guests

* Register Guest
* Update Guest
* Search Guest

Reservations

* Create Reservation
* Check Availability
* Cancel Reservation
* Reservation History

Authentication

* Login
* JWT
* Role Authorization

Dashboard

* Number of Hotels
* Number of Rooms
* Active Reservations
* Available Rooms

---

# Business Rules

A room cannot have overlapping reservations.

Check-in date must be before check-out.

Reservations cannot start in the past.

Cancelled reservations release room availability.

Only available rooms may be booked.

The reservation total must be calculated automatically.

---

# Future Features

Do NOT implement now.

Only document them.

Examples:

* Online Payments
* Coupons
* Seasonal Pricing
* Housekeeping
* Maintenance
* Reviews
* Multi-language
* Email Notifications
* SMS Notifications
* Calendar View
* Reports
* Audit Log

---

# Backend Requirements

Follow all backend rules defined in:

.ai/10-backend.md

Additionally:

* Organize by Features.
* Controllers must contain no business rules.
* Services implement all business rules.
* Manual mapping only.
* Use Fluent API.
* Swagger enabled.
* JWT configured.
* SQLite in Development.
* SQL Server in Production.
* Global exception middleware.
* Dependency Injection through extension methods.

---

# Frontend Requirements

Follow all frontend rules defined in:

.ai/20-frontend.md

Additionally:

* Responsive layout.
* Simple and modern UI.
* Reusable components.
* Feature-based organization.
* Consume only the backend API.
* Never hardcode data.

---

# MVP Development Order

Complete each phase before starting the next one.

Phase 1

* Solution structure
* Backend
* Frontend

Phase 2

Authentication

Phase 3

Hotels

Phase 4

Room Types

Phase 5

Rooms

Phase 6

Guests

Phase 7

Reservations

Phase 8

Dashboard

Do not begin the next phase until the current phase is fully functional.

---

# Git

Initialize a Git repository.

Generate:

* README.md
* LICENSE (MIT)
* .gitignore

Follow the standards defined in the AI documentation.

---

# Final Objective

When the MVP is complete, the application must allow a user to:

* Authenticate.
* Register hotels.
* Register room types.
* Register rooms.
* Register guests.
* Create reservations.
* Prevent double booking.
* Cancel reservations.
* View reservation history.
* View dashboard metrics.

The application must be completely functional, demonstrable in less than five minutes and ready to be published on GitHub as part of a professional software engineering portfolio.
