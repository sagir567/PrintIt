# PrintIt

## Overview
PrintIt is a full-stack 3D printing store application. It includes an ASP.NET Core Web API for catalog and inventory management, and a React frontend for browsing products, viewing variant details, uploading STL files, and managing a local cart.

The backend exposes both public catalog endpoints and admin endpoints for managing materials, colors, filaments, filament spools, categories, and products. Data is stored in PostgreSQL via Entity Framework Core.

## Tech Stack
- **Backend:** .NET 8, ASP.NET Core Web API
- **Database:** PostgreSQL 16
- **ORM / Data Access:** Entity Framework Core 8, Npgsql provider
- **Frontend:** React 19, TypeScript, Vite, React Router, Three.js (`@react-three/fiber`, `@react-three/drei`)
- **Testing:** xUnit, FluentAssertions, ASP.NET Core integration testing (`WebApplicationFactory`), Testcontainers (PostgreSQL)
- **CI:** GitHub Actions (`.github/workflows/ci.yml`)

## Architecture
The solution is organized into separate projects:

- **`Api/`** – Web API entry point (`Program.cs`) and REST controllers.
- **`Domain/`** – Domain entities and domain logic (`SpoolConsumption`).
- **`Infrastructure/`** – `AppDbContext`, EF Core model configuration, and migrations.
- **`Tests/`** – Integration tests, domain unit tests, and smoke tests.
- **`frontend/`** – React + TypeScript application (catalog, STL upload flow, cart, basic admin pages).

On startup, the API:
- registers controllers, CORS, Swagger,
- configures EF Core with PostgreSQL,
- applies migrations,
- seeds initial demo data when tables are empty.

## Features
- Public catalog categories endpoint (active categories sorted by `SortOrder`, then `Name`).
- Public products endpoint with:
  - search by title/description,
  - category filter,
  - price range filter,
  - sorting (`newest`, `name_asc`, `name_desc`, `price_asc`, `price_desc`),
  - pagination (`page`, `pageSize`).
- Public product details by slug, including active categories and active variants.
- Product variant price calculation:
  - `Price = (WeightGrams / 1000) * MaterialType.BasePricePerKg + PriceOffset`
- Catalog card `PriceFrom` calculated from the lowest active variant price.
- Public material types and colors listing endpoints.
- Public filaments endpoint with computed inventory summary (`TotalRemainingGrams`, `AvailableSpools`) and in-stock filtering (`RemainingGrams > 0`).
- Admin endpoints for:
  - material types (create/list/activate/deactivate/delete),
  - colors (create/list/activate/deactivate),
  - filaments (create/list/list spools/consume inventory),
  - filament spools (create/consume),
  - categories (create/list/update/activate/deactivate/delete),
  - products (create/list/update/activate/deactivate/delete).
- Inventory consumption rules with tolerance (`10` grams) and status transitions (`New`, `Opened`, `Empty`).
- Frontend pages:
  - Home,
  - Product catalog,
  - Product details,
  - STL upload with 3D preview,
  - Cart (persisted in `localStorage`),
  - Admin materials page,
  - Admin colors page.

## API Overview

### Public Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/catalog/categories` | List active categories |
| GET | `/api/v1/catalog/products` | List products with filters/sort/pagination |
| GET | `/api/v1/catalog/products/{slug}` | Get product details by slug |
| GET | `/api/v1/material-types` | List active material types |
| GET | `/api/v1/colors` | List active colors |
| GET | `/api/v1/filaments` | List in-stock filaments with inventory summary |

`GET /api/v1/catalog/products` supports:
- `q`
- `category`
- `sort`
- `minPrice`
- `maxPrice`
- `page`
- `pageSize`

### Admin Endpoints

| Method | Endpoint |
|---|---|
| POST | `/api/v1/admin/material-types` |
| GET | `/api/v1/admin/material-types` |
| PATCH | `/api/v1/admin/material-types/{id}/deactivate` |
| PATCH | `/api/v1/admin/material-types/{id}/activate` |
| DELETE | `/api/v1/admin/material-types/{id}` |
| POST | `/api/v1/admin/colors` |
| GET | `/api/v1/admin/colors` |
| PATCH | `/api/v1/admin/colors/{id}/deactivate` |
| PATCH | `/api/v1/admin/colors/{id}/activate` |
| POST | `/api/v1/admin/filaments` |
| GET | `/api/v1/admin/filaments` |
| GET | `/api/v1/admin/filaments/{id}/spools` |
| PATCH | `/api/v1/admin/filaments/{id}/consume` |
| POST | `/api/v1/admin/filament-spools` |
| PATCH | `/api/v1/admin/filament-spools/{id}/consume` |
| POST | `/api/v1/admin/categories` |
| GET | `/api/v1/admin/categories` |
| PUT | `/api/v1/admin/categories/{id}` |
| PATCH | `/api/v1/admin/categories/{id}/deactivate` |
| PATCH | `/api/v1/admin/categories/{id}/activate` |
| DELETE | `/api/v1/admin/categories/{id}` |
| POST | `/api/v1/admin/products` |
| GET | `/api/v1/admin/products` |
| PUT | `/api/v1/admin/products/{id}` |
| PATCH | `/api/v1/admin/products/{id}/deactivate` |
| PATCH | `/api/v1/admin/products/{id}/activate` |
| DELETE | `/api/v1/admin/products/{id}` |

## Data Model
Core entities in the current domain model:

- **MaterialType**
  - `Id`, `Name`, `BasePricePerKg`, `IsActive`, `CreatedAtUtc`
- **Color**
  - `Id`, `Name`, `Hex`, `IsActive`, `CreatedAtUtc`
- **Filament**
  - `Id`, `MaterialTypeId`, `ColorId`, `Brand`, `CostPerKg`, `IsActive`, `CreatedAtUtc`
  - Relation: one material type, one color, many spools
  - Unique index: `(MaterialTypeId, ColorId, Brand)`
- **FilamentSpool**
  - `Id`, `FilamentId`, `InitialGrams`, `RemainingGrams`, `Status`, `CreatedAtUtc`, `LastUsedAtUtc`
- **Category**
  - `Id`, `Name`, `Slug`, `Description`, `SortOrder`, `IsActive`, `CreatedAtUtc`
- **Product**
  - `Id`, `Title`, `Slug`, `Description`, `MainImageUrl`, `IsActive`, `CreatedAtUtc`
  - Relations: many categories (many-to-many), many variants (one-to-many)
- **ProductVariant**
  - `Id`, `ProductId`, `SizeLabel`, `MaterialTypeId`, `ColorId`, dimensions (`WidthMm`, `HeightMm`, `DepthMm`), `WeightGrams`, `PriceOffset`, `IsActive`, `CreatedAtUtc`
  - Unique index: `(ProductId, SizeLabel, MaterialTypeId, ColorId)`

## Getting Started

### Prerequisites
- .NET SDK 8.x
- Node.js 20+ and npm
- Docker (required for local PostgreSQL container and test infrastructure)

### Database Setup
Start PostgreSQL:

```bash
docker compose up -d postgres
```

Set connection string for the API (required, because `ConnectionStrings:Postgres` is not defined in `Api/appsettings.json`):

```cmd
set "ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=printit_db;Username=printit;Password=printit"
```

### Run Backend
From repository root:

```bash
dotnet run --project Api/PrintIt.Api.csproj
```

Default local API URL (from launch settings):
- `http://localhost:5051`

Swagger (Development):
- `http://localhost:5051/swagger`

### Run Frontend
From repository root:

```bash
cd frontend
npm install
npm run dev
```

Default Vite URL:
- `http://localhost:5173`

### Run Tests
From repository root:

```bash
dotnet test PrintIt.sln
```

Test coverage in this repository includes:
- controller integration tests (catalog, products, materials, colors, filaments, filament spools),
- domain unit tests for spool consumption,
- PostgreSQL connectivity smoke test.

## Project Structure

```text
PrintIt/
├─ Api/
│  ├─ Controllers/
│  └─ Program.cs
├─ Domain/
│  ├─ Entities/
│  └─ DomainLogic/
├─ Infrastructure/
│  └─ Persistence/
│     ├─ AppDbContext.cs
│     └─ Migrations/
├─ Tests/
│  ├─ Controllers/
│  ├─ Domain/
│  ├─ Smoke/
│  └─ Infrastructure/
└─ frontend/
   └─ src/
```

## Notes
- Admin endpoints currently have no authentication/authorization in the API.
- Frontend API calls are hardcoded to `http://localhost:5051` in source files.
- `frontend/vite.config.js` defines a proxy to `http://localhost:5000`, but frontend code uses absolute URLs to `5051`.
- `Api/PrintIt.Api.http` still includes a `weatherforecast` request that is not implemented by current controllers.

## Future Improvements
- Add authentication/authorization for admin routes.
- Move frontend API base URL to environment configuration.
- Align Vite proxy target and frontend API consumption strategy.
- Expand automated endpoint coverage for remaining controller paths.