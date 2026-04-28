# PatchPlatform

A platform for managing software patches, built with **Node.js**, **Express**, **TypeScript**, **Prisma ORM**, and **MySQL**.

## Tech Stack

| Layer        | Technology                        |
|--------------|-----------------------------------|
| Runtime      | Node.js 20                        |
| Language     | TypeScript                        |
| Framework    | Express 5                         |
| ORM          | Prisma 7 (with MariaDB adapter)   |
| Database     | MySQL 8.0                         |
| Migrations   | Prisma Migrate                    |
| Tests        | Jest + Supertest                  |
| Local DB     | Docker Compose                    |

## Domain Model

```
User ──< Patch >── Release
              |
              └── PatchTag >── Tag
```

- **User** – people who create and manage patches (roles: ADMIN, DEVELOPER, VIEWER)
- **Patch** – a software patch with title, version, description, and status lifecycle (PENDING → IN_REVIEW → APPROVED/REJECTED → DEPLOYED)
- **Release** – a versioned software release that groups patches
- **Tag** – labels for categorising patches (many-to-many via PatchTag)

## API Endpoints

| Method | Path                   | Description                 |
|--------|------------------------|-----------------------------|
| GET    | /health                | Health check                |
| GET    | /api/v1/users          | List all users              |
| POST   | /api/v1/users          | Create a user               |
| GET    | /api/v1/users/:id      | Get a user                  |
| PATCH  | /api/v1/users/:id      | Update a user               |
| DELETE | /api/v1/users/:id      | Delete a user               |
| GET    | /api/v1/patches        | List patches (filterable)   |
| POST   | /api/v1/patches        | Create a patch              |
| GET    | /api/v1/patches/:id    | Get a patch                 |
| PATCH  | /api/v1/patches/:id    | Update a patch              |
| DELETE | /api/v1/patches/:id    | Delete a patch              |
| GET    | /api/v1/releases       | List all releases           |
| POST   | /api/v1/releases       | Create a release            |
| GET    | /api/v1/releases/:id   | Get a release               |
| PATCH  | /api/v1/releases/:id   | Update a release            |
| DELETE | /api/v1/releases/:id   | Delete a release            |
| GET    | /api/v1/tags           | List all tags               |
| POST   | /api/v1/tags           | Create a tag                |
| GET    | /api/v1/tags/:id       | Get a tag                   |
| DELETE | /api/v1/tags/:id       | Delete a tag                |

## Prerequisites

- Node.js 20+
- Docker & Docker Compose (for local MySQL)

## Quick Start

### 1. Clone and install

```bash
git clone https://github.com/AaronSena2/PatchPlatform.git
cd PatchPlatform
npm install
```

### 2. Configure environment

```bash
cp .env.example .env
# Edit .env if you need non-default values
```

### 3. Start MySQL via Docker Compose

```bash
docker compose up db -d
```

This starts MySQL 8.0 on `localhost:3306` with:
- Database: `patchplatform`
- User: `patch_user` / Password: `patch_password`

### 4. Run migrations

```bash
npm run db:migrate:deploy
```

Or during active development (creates migration files automatically):

```bash
npm run db:migrate
```

### 5. (Optional) Seed the database

```bash
npm run db:seed
```

### 6. Generate Prisma client

```bash
npm run db:generate
```

### 7. Start the development server

```bash
npm run dev
```

The API will be available at `http://localhost:3000`.

## Scripts

| Script                  | Description                                |
|-------------------------|--------------------------------------------|
| `npm run dev`           | Start dev server with hot reload           |
| `npm run build`         | Compile TypeScript to `dist/`              |
| `npm start`             | Start compiled server                      |
| `npm test`              | Run tests                                  |
| `npm run typecheck`     | TypeScript type check without emitting     |
| `npm run db:generate`   | Regenerate Prisma client                   |
| `npm run db:migrate`    | Create + apply migrations (dev)            |
| `npm run db:migrate:deploy` | Apply existing migrations (prod/CI)   |
| `npm run db:seed`       | Seed database with sample data             |
| `npm run db:studio`     | Open Prisma Studio UI                      |
| `npm run db:reset`      | Drop + recreate database (dev only)        |

## Environment Variables

| Variable          | Default                                    | Description                    |
|-------------------|--------------------------------------------|--------------------------------|
| `DATABASE_URL`    | `mysql://patch_user:...@localhost:3306/...`| Full MySQL connection URL      |
| `MYSQL_HOST`      | `localhost`                                | MySQL host (fallback)          |
| `MYSQL_PORT`      | `3306`                                     | MySQL port (fallback)          |
| `MYSQL_USER`      | `patch_user`                               | MySQL username (fallback)      |
| `MYSQL_PASSWORD`  | `patch_password`                           | MySQL password (fallback)      |
| `MYSQL_DATABASE`  | `patchplatform`                            | MySQL database name (fallback) |
| `PORT`            | `3000`                                     | HTTP server port               |
| `NODE_ENV`        | `development`                              | Environment (development/production/test) |

> **Note**: `DATABASE_URL` takes precedence. If not set, the individual `MYSQL_*` variables are used to build the URL.

## Docker Compose (Full Stack)

To run both the app and database in containers:

```bash
docker compose up --build
```

## Running Tests

```bash
npm test
```

Tests use Jest + Supertest and do not require a running database (app-level tests only). For full integration tests against MySQL, set up the database first and run with `DATABASE_URL` pointing to your test database.

## Project Structure

```
PatchPlatform/
├── prisma/
│   ├── schema.prisma          # Prisma schema (domain models)
│   ├── migrations/            # SQL migration files
│   └── seed.ts                # Database seed script
├── prisma.config.ts           # Prisma v7 configuration (datasource URL)
├── src/
│   ├── app.ts                 # Express app setup
│   ├── server.ts              # HTTP server entry point
│   ├── db/
│   │   └── client.ts          # Prisma client singleton (MariaDB adapter)
│   ├── routes/
│   │   ├── index.ts           # Route aggregator
│   │   ├── users.ts           # User CRUD routes
│   │   ├── patches.ts         # Patch CRUD routes
│   │   ├── releases.ts        # Release CRUD routes
│   │   └── tags.ts            # Tag CRUD routes
│   ├── middleware/
│   │   └── errorHandler.ts    # Global error + 404 handlers
│   └── __tests__/
│       └── app.test.ts        # App-level tests
├── docker-compose.yml         # Local MySQL + app containers
├── Dockerfile                 # Production container image
├── .env.example               # Example environment variables
└── README.md
```
