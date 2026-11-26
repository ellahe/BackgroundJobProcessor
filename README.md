# Background Job Processor

A persistent background job processing system built with **.NET 11**, **ASP.NET Core Web API**, and **PostgreSQL**.

The project demonstrates a practical approach to building reliable background processing workflows using a lightweight Clean Architecture.

## Planned Features

- REST API for submitting and tracking background jobs
- Persistent job storage with PostgreSQL and Entity Framework Core
- Background worker based on .NET `BackgroundService`
- Job lifecycle management and execution history
- Configurable retry policies with exponential backoff
- Safe job claiming to prevent concurrent execution
- Recovery of interrupted jobs
- Unit and integration testing

## Technology Stack

- C# / .NET 11
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- BackgroundService
- xUnit

## Solution Structure

```text
src/
  BackgroundJobProcessor.API/
  BackgroundJobProcessor.Application/
  BackgroundJobProcessor.Domain/
  BackgroundJobProcessor.Infrastructure/

tests/
  BackgroundJobProcessor.UnitTests/
  BackgroundJobProcessor.IntegrationTests/
```

## Architecture

The solution follows a lightweight Clean Architecture:

- **Domain** — Core entities, business rules, and job lifecycle.
- **Application** — Use cases and application contracts.
- **Infrastructure** — Database persistence and background processing.
- **API** — HTTP endpoints and application composition.

## Project Status

The initial solution structure and project references are in place. Feature implementation is in progress.

## Getting Started

Prerequisites and setup instructions will be added as the implementation progresses.

## License

This project is currently available for portfolio and educational purposes.