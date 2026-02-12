# AIchivist

An AI-powered search tool for Washington State University's archival collections. Users enter natural-language queries, and the system finds relevant archival collections using a hybrid search pipeline combining Claude AI with PostgreSQL full-text search. A built-in chat interface allows follow-up questions about search results.

## Tech Stack

- **Frontend:** Angular 21 (zoneless, signal-based), SCSS, Vitest
- **Backend:** ASP.NET Core (.NET 10), C#
- **Database:** PostgreSQL 16 (Docker), Entity Framework Core
- **AI:** Anthropic Claude (Haiku 4.5 for search, Sonnet 4.5 for chat)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (with npm)
- [Docker](https://www.docker.com/get-started)
- An [Anthropic API key](https://console.anthropic.com/)

## Getting Started

### 1. Clone the repository

```bash
git clone <repository-url>
cd WSU_Archive_Search_Tool_New
```

### 2. Start the database

```bash
docker-compose up -d
```

This starts a PostgreSQL 16 container on port `5432` with:
- **User:** `archive`
- **Password:** `archive`
- **Database:** `archive_search`

### 3. Configure the API key

```bash
dotnet user-secrets set "ANTHROPIC_API_KEY" "sk-ant-..." --project backend/ArchiveSearch.API
```

### 4. Start the backend

```bash
dotnet run --project backend/ArchiveSearch.API
```

The API starts at `http://localhost:5265`. Database migrations are applied automatically on startup.

### 5. Start the frontend

```bash
cd frontend
npm install
npm start
```

The dev server starts at `http://localhost:4200`.

## Commands Reference

### Backend

```bash
dotnet build backend/ArchiveSearch.sln          # Build the solution
dotnet run --project backend/ArchiveSearch.API   # Run the API server
dotnet test backend/                             # Run tests
```

### Frontend (from the `frontend/` directory)

```bash
npm start       # Dev server at http://localhost:4200
npm run build   # Production build
npm test        # Run Vitest unit tests
```

### Database

```bash
docker-compose up -d      # Start PostgreSQL
docker-compose down       # Stop PostgreSQL
docker-compose down -v    # Stop and remove data volume
```

### EF Core Migrations

```bash
dotnet ef migrations add <Name> --project backend/ArchiveSearch.Data --startup-project backend/ArchiveSearch.API
```

## How It Works

AIchivist uses a 3-pass search pipeline:

1. **Query Expansion** (Claude Haiku) -- Expands the user's query into 6-8 synonyms and related terms
2. **Full-Text Search** (PostgreSQL) -- Runs weighted full-text search across the archival catalog using original and expanded terms
3. **AI Ranking** (Claude Haiku) -- Scores and ranks the top 10 results with relevance explanations

Results are cached for 1 hour. Users can then ask follow-up questions about results via the chat sidebar, powered by Claude Sonnet.

## API Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/api/search` | POST | Run a hybrid search query |
| `/api/chat` | POST | Follow-up chat about search results |
| `/api/admin/index` | POST | Batch index EAD XML finding aids |
| `/api/health` | GET | Health check |

## Project Structure

```
backend/
  ArchiveSearch.API/       # Controllers, services, DI setup
  ArchiveSearch.Core/      # Models, EAD XML parser, cache
  ArchiveSearch.Data/      # EF Core DbContext, repository, migrations

frontend/src/app/
  components/
    search-bar/            # Search input with example queries
    results-panel/         # Search results display
      result-card/         # Individual result card
    chat-sidebar/          # Multi-turn chat about results
  services/                # API client services
  models/                  # TypeScript interfaces
```
