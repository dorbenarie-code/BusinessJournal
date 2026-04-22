# BusinessJournal

BusinessJournal is an end-to-end system for managing customers and appointments, built with a strong focus on clean architecture, solid design principles, and real-world backend practices.

---

## What this system does

- Manage customers  
- Schedule appointments  
- Prevent overlapping time ranges  
- Cancel and reschedule appointments  
- Work with authenticated API endpoints (JWT)  

The project also includes a Blazor frontend that consumes the API and demonstrates a complete flow.

---

## Tech Stack

**Backend**
- .NET 8 (ASP.NET Core Web API)
- SQL Server (manual SQL + repository pattern)
- JWT Authentication
- Rate Limiting

**Frontend**
- Blazor (Server)
- HttpClient API clients

**Testing**
- xUnit  
- 90+ tests across all layers  

---

## Architecture

The project follows a clean layered structure:

- **Domain**  
  Core entities and business rules (Customer, Appointment, TimeRange)

- **Application**  
  Use cases and orchestration (CustomerService, AppointmentService, AuthService)

- **Infrastructure**  
  SQL access, repositories, connection factory

- **API**  
  Controllers, authentication, rate limiting, request handling

- **Web (Blazor)**  
  Frontend consuming the API

The focus was to keep responsibilities clear and avoid mixing concerns between layers.

---

## SQL & Data Access

- Uses **SQL Server** with explicit SQL scripts (`.sql` files)
- No ORM abstraction layer (intentional decision)
- Direct control over queries and schema
- `SqlServerConnectionFactory` handles connection lifecycle cleanly
- Repository pattern used in a pragmatic way (not over-engineered)

---

## Business Logic

Key rules implemented:

- Appointments are validated using a **TimeRange value object**
- Overlapping appointments are prevented at the application layer
- Domain logic is not handled in controllers

---

## Authentication & Security

- JWT-based authentication
- Token generation handled by a dedicated service
- Password hashing using ASP.NET Identity hasher
- Protected endpoints require a valid token
- Frontend automatically attaches the token via HTTP handler

---

## Rate Limiting

- Request rate limiting applied at the API level
- Helps prevent abuse (e.g. login brute-force)
- Policy-based configuration
- Designed with real-world deployment in mind (proxy considerations)

---

## Testing

The project includes **91 passing tests** across multiple layers:

- **Domain tests**  
  Validate business rules (TimeRange, Appointment logic)

- **Application tests**  
  Validate services and workflows

- **Infrastructure tests**  
  Validate repository behavior

- **API tests**  
  Integration tests using WebApplicationFactory

The goal was to ensure correctness without over-testing trivial code.

---

## Running the project

### Run API

```bash
cd src/BusinessJournal.Api
dotnet run
Run frontend
cd src/BusinessJournal.Web
dotnet run
Example flow
Login
Create a customer
Create an appointment
Try to create an overlapping appointment (fails)
Cancel or reschedule
Design Goals
Keep the code clean and readable
Apply SOLID principles where they add real value
Avoid over-engineering
Build something that works end-to-end
Focus on backend correctness, not just UI


Author

Built as part of my journey toward a backend developer role (C# / .NET).


```md
> 91 tests passing | Clean Architecture | SQL Server | JWT Auth | Rate Limiting

## Screenshots

### Home
![Home](screenshots/home.png)

### Login
![Login](screenshots/login.png)

### Customers
![Customers](screenshots/customers.png)

### Appointments
![Appointments](screenshots/appointments.png)