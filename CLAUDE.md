# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**AIchivist** — an AI-powered search tool for Washington State University's archival collections. Users enter natural-language queries, the system finds relevant archival collections via a 3-pass hybrid search (Claude query expansion → PostgreSQL full-text search → Claude ranking), and provides a chat interface to ask follow-up questions about results.

## Tech Stack

- **Frontend:** Angular 21 (zoneless, signal-based), SCSS, Vitest
- **Backend:** ASP.NET Core (.NET 10), C#
- **Database:** PostgreSQL 16 (Docker), Entity Framework Core, full-text search with weighted tsvector
- **AI:** Anthropic SDK v12.4.0 — Haiku 4.5 for search, Sonnet 4.5 for chat

## Commands

### Backend
```bash
dotnet build backend/ArchiveSearch.sln          # Build
dotnet run --project backend/ArchiveSearch.API   # Run (auto-applies EF migrations)
dotnet test backend/                             # Test
```

### Frontend (run from `frontend/` directory)
```bash
npm start       # Dev server at http://localhost:4200
npm run build   # Production build to dist/
npm test        # Vitest unit tests
```

### Database
```bash
docker-compose up -d                            # Start PostgreSQL
dotnet ef migrations add <Name> --project backend/ArchiveSearch.Data --startup-project backend/ArchiveSearch.API
```

### API Key Setup
```bash
dotnet user-secrets set "ANTHROPIC_API_KEY" "sk-ant-..." --project backend/ArchiveSearch.API
```

## Architecture

### Backend (3-project solution)

- **ArchiveSearch.API** — Controllers, services (ClaudeService, SearchService, IndexingService), DI setup in Program.cs
- **ArchiveSearch.Core** — Models, SearchCache, EadParser (XML parsing for EAD finding aids)
- **ArchiveSearch.Data** — EF Core DbContext (`ArchiveContext`), `CollectionRepository` (FTS queries), migrations

Services live in `API/Services/` (not Core) to avoid circular dependency with Data.

### Frontend (Angular 21, standalone components)

```
src/app/
├── components/
│   ├── search-bar/        # Search input with example queries
│   ├── results-panel/     # Search results display
│   │   └── result-card/   # Individual result card
│   └── chat-sidebar/      # Multi-turn chat about results
├── services/
│   ├── search.ts          # POST /api/search
│   └── chat.ts            # POST /api/chat
└── models/                # TypeScript interfaces
```

State management uses Angular signals (`signal()`, `computed()`, `effect()`). No external state library.

### 3-Pass Search Pipeline

1. **Pass 0 — Query Expansion** (Claude Haiku): Generates 6-8 synonyms/related terms
2. **Pass 1 — Full-Text Search** (PostgreSQL): Runs FTS for original + expanded queries, deduplicates ~30-50 candidates
3. **Pass 2 — AI Ranking** (Claude Haiku): Scores and ranks top 10 with explanations

Each pass has a fallback: expansion failure → original query only; FTS failure → skip that phrase; ranking failure → use FTS positional order.

Results are cached 1 hour by SHA256(query).

### API Endpoints

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/search` | POST | 3-pass hybrid search |
| `/api/chat` | POST | Follow-up chat about search results |
| `/api/admin/index` | POST | Batch index EAD XML files |
| `/api/health` | GET | Health check |

## Key Configuration

- Backend listens on the port shown in console output (check `Properties/launchSettings.json`)
- Frontend API URL is hardcoded to `http://localhost:5265/api` in service files
- CORS allows `http://localhost:4200` (configurable via `FrontendOrigin` in appsettings)
- Connection string: env var `CONNECTION_STRING` → appsettings `ConnectionStrings:Default` → default localhost

## Anthropic SDK Notes (v12.4.0)

- Use `TextBlockParam` (not `RequestTextContent`)
- Use `CacheControlEphemeral` (not `CacheControl`)
- Use `MessageParam` (not `RequestMessage`)
- `block.TryPickText(out var textBlock)` takes 1 argument only
- Prompt caching is via `CacheControlEphemeral` property on content blocks, no separate config needed

## Database Schema

The `collections` table stores parsed archival collection metadata with a GIN-indexed `search_vector` (tsvector). Weights: **A** = title, **B** = abstract + subjects + names + places, **C** = scope/biog/corps/genres/series. Migrations auto-apply on API startup.
