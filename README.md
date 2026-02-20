# AIchivist

Search Washington State University's archival collections using natural language, powered by AI.

## The Problem

University archives hold thousands of collections — letters, photographs, research papers, institutional records — but finding relevant material means wrestling with rigid keyword searches across catalog systems designed for librarians, not researchers. You have to already know the right terminology to find what you're looking for.

AIchivist lets you search the way you think. Ask a question in plain English, and the system combines AI query expansion with full-text search to surface the most relevant collections, ranked and explained.

## Quick Start

You'll need [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/), and an [Anthropic API key](https://console.anthropic.com/).

```bash
# 1. Set your API key
dotnet user-secrets set "ANTHROPIC_API_KEY" "sk-ant-..." --project backend/ArchiveSearch.API

# 2. Start the backend (auto-creates SQLite database and applies migrations)
dotnet run --project backend/ArchiveSearch.API

# 3. In a new terminal — start the frontend
cd frontend && npm install && npm start
```

Open **http://localhost:4200**. Try searching: *"What materials exist about student protests in the 1970s?"*

## How It Works

AIchivist uses a 3-pass search pipeline to go from a natural-language question to ranked, explained results:

```
User Query
    │
    ▼
┌─────────────────────────────────────┐
│  Pass 1: Query Expansion            │
│  Claude Haiku expands your query    │
│  into 6-8 synonyms & related terms  │
│  Detects date ranges automatically  │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Pass 2: Full-Text Search           │
│  SQLite FTS5 searches the catalog   │
│  using original + expanded terms    │
│  Weighted by title, abstract,       │
│  subjects, names, places            │
│  → ~30-50 candidate collections     │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Pass 3: AI Ranking                 │
│  Claude Haiku reads full metadata   │
│  for all candidates, selects and    │
│  ranks top 10 with explanations     │
│  → Scored 1-10 with relevance notes │
└─────────────────────────────────────┘
```

Each pass has a fallback — if query expansion fails, the original query is used. If ranking fails, results fall back to search-engine ordering. The pipeline degrades gracefully instead of breaking.

Results are cached for 1 hour, so repeated searches are instant.

## Features

- **Natural-language search** — ask questions the way you'd ask a librarian, not a database
- **AI-ranked results** — top 10 results scored and explained by relevance, not just keyword frequency
- **Follow-up chat** — ask questions about your results in a chat sidebar, powered by Claude Sonnet
- **Related collections** — discover similar collections based on shared subjects, people, and places
- **Detailed metadata** — expand any result to see abstracts, scope notes, biographical history, series info, and more
- **One-time setup** — first-run wizard walks you through API key configuration with no terminal required

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21 (zoneless, signal-based), SCSS, Vitest |
| Backend | ASP.NET Core (.NET 10), C# |
| Database | SQLite (FTS5), Entity Framework Core |
| AI | Anthropic Claude — Haiku 4.5 (search), Sonnet 4.5 (chat) |

## Commands

### Backend

```bash
dotnet build backend/ArchiveSearch.slnx         # Build
dotnet run --project backend/ArchiveSearch.API   # Run API on http://localhost:5265
dotnet test backend/                             # Run tests
```

### Frontend (from `frontend/`)

```bash
npm start       # Dev server on http://localhost:4200
npm run build   # Production build
npm test        # Run Vitest tests
```

### Database

The SQLite database file (`archive.db`) is created automatically on first run -- no setup needed. To reset the database, simply delete the `archive.db` file and restart the backend.

## API Endpoints

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/search` | POST | Run a search query |
| `/api/chat` | POST | Chat about search results |
| `/api/admin/index` | POST | Index EAD XML finding aids |
| `/api/health` | GET | Health check |
| `/api/setup/status` | GET | Check if API key is configured |
| `/api/setup/save` | POST | Save API key (first-run) |

## Project Structure

```
backend/
  ArchiveSearch.API/         # Controllers, services, DI config
    Services/
      SearchService.cs       # 3-pass search pipeline orchestration
      ClaudeService.cs       # Anthropic SDK wrapper (expand, rank, chat)
  ArchiveSearch.Core/        # Models, EAD XML parser, search cache
  ArchiveSearch.Data/        # DbContext, repository (FTS queries), migrations
  ArchiveSearch.Tests/       # Integration tests (xUnit + WebApplicationFactory)

frontend/src/app/
  components/
    home/                    # Main layout — search + results + chat
    search-bar/              # Search input with clickable example queries
    results-panel/           # Result list with expandable details
      result-card/           # Individual result — score, metadata, related collections
    chat-sidebar/            # Multi-turn follow-up chat
    setup/                   # First-run API key configuration
  services/                  # HTTP clients for search, chat
  models/                    # TypeScript interfaces
```

## Desktop Installer

AIchivist can be packaged as a standalone Windows application with an embedded SQLite database -- no Docker, no terminal, no technical setup required.

```powershell
.\build-installer.ps1    # Builds frontend → runs tests → publishes → compiles installer
```

The installer bundles everything into a single `AIchivist-Setup.exe` that handles database initialization and configuration automatically. See [installer/](installer/) for details.

## Contributing

Contributions are welcome. Please open an issue to discuss what you'd like to change before submitting a pull request.
