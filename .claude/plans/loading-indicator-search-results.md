# Feature: Loading Indicator for Search Results

**Branch:** feature/loading-indicator-search-results
**Scope:** Medium
**Layers:** Full-stack (frontend + backend)

## Description

Add a multi-step loading indicator that shows real-time progress through the 3-pass search pipeline (expanding query → searching database → ranking results). The backend streams progress events via SignalR, and the frontend replaces the results area with an animated step-by-step progress display until results arrive.

## Requirements

- Backend: Add a SignalR hub that emits progress events during each search pass
- Backend: Refactor SearchService to accept an optional progress callback
- Frontend: Connect to SignalR hub and display step-by-step progress
- Frontend: Show which step is active, completed, or pending
- Frontend: Replace the results area with the progress display while searching
- Frontend: Show which step failed if the search errors mid-pipeline
- Cached results should skip the progress display (instant return)

## Affected Files

### Existing (modify)
- `backend/ArchiveSearch.API/ArchiveSearch.API.csproj` — add SignalR package reference
- `backend/ArchiveSearch.API/Program.cs` — register SignalR services, map hub endpoint, update CORS for SignalR
- `backend/ArchiveSearch.API/Services/SearchService.cs` — emit progress events via hub during each pass
- `backend/ArchiveSearch.API/Controllers/SearchController.cs` — pass hub context to search service
- `frontend/src/app/services/search.ts` — add SignalR connection + progress signal
- `frontend/src/app/components/home/home.ts` — wire up progress state
- `frontend/src/app/components/home/home.html` — show progress indicator when loading
- `frontend/src/app/components/home/home.scss` — styles for progress display
- `frontend/proxy.conf.json` — proxy SignalR hub WebSocket in dev
- `frontend/package.json` — add `@microsoft/signalr` dependency

### New (create)
- `backend/ArchiveSearch.API/Hubs/SearchHub.cs` — SignalR hub for search progress
- `backend/ArchiveSearch.API/Models/SearchProgress.cs` — progress event model
- `frontend/src/app/components/search-progress/search-progress.ts` — inline progress display component
- `frontend/src/app/services/search-hub.ts` — SignalR connection service

## API Contract

### SignalR Hub: `/hubs/search`

**Client → Server:**
| Method | Payload | Description |
|--------|---------|-------------|
| `StartSearch` | `{ query: string }` | Initiates search and subscribes to progress |

**Server → Client:**
| Event | Payload | Description |
|-------|---------|-------------|
| `SearchProgress` | `{ step: string, status: "active" \| "completed" \| "failed", message: string }` | Progress update for each pass |
| `SearchCompleted` | `SearchResponse` (same as existing POST response) | Final results |
| `SearchFailed` | `{ error: string, failedStep: string }` | Search failed |

**Steps:** `expanding_query`, `searching_database`, `ranking_results`

## Implementation Steps

### Backend

1. Add `Microsoft.AspNetCore.SignalR` to API project (included in ASP.NET Core, just needs `AddSignalR()`)
2. Create `SearchProgress` model with `Step`, `Status`, `Message` fields
3. Create `SearchHub.cs` — hub with `StartSearch` method that:
   - Calls `SearchService.SearchAsync` with a progress callback
   - Emits `SearchProgress` events to the calling client at each pass
   - Emits `SearchCompleted` with the final response
   - Emits `SearchFailed` if an unrecoverable error occurs
4. Refactor `SearchService.SearchAsync` to accept an optional `IProgress<SearchProgress>` callback
   - Report progress before/after each pass (expand, FTS, rank)
   - Keep the existing HTTP POST endpoint working unchanged (no progress callback)
5. Register SignalR in `Program.cs`:
   - `builder.Services.AddSignalR()`
   - `app.MapHub<SearchHub>("/hubs/search")`
   - Update CORS to `.AllowCredentials()` (required for SignalR)

### Frontend

1. Install `@microsoft/signalr` npm package
2. Create `search-hub.ts` service:
   - Manages SignalR `HubConnection` to `/hubs/search` (or proxied `/hubs/search`)
   - Exposes `progress` signal (array of step states) and `searchResult` signal
   - Methods: `search(query)`, `disconnect()`
   - Auto-reconnect on connection loss
3. Create `search-progress.ts` component:
   - Receives `steps` input signal (array of `{ step, status, message }`)
   - Renders a vertical stepper: checkmark for completed, spinner for active, dot for pending
   - Displays step label + message text for each
   - Animated transitions between states
4. Update `home.ts`:
   - Inject `SearchHubService`
   - On search: use hub instead of HTTP POST
   - Expose `searchProgress` signal for the template
   - Fallback: if SignalR connection fails, fall back to existing HTTP POST (no progress)
5. Update `home.html`:
   - When `loading()` and progress steps exist, show `<app-search-progress>`
   - Otherwise show results (existing behavior)
6. Update `proxy.conf.json` to proxy `/hubs/*` to backend with WebSocket support

## Edge Cases & Error Handling

- **SignalR connection fails:** Fall back to existing HTTP POST endpoint silently — user still gets results, just no progress indicator
- **Search fails at Pass 0 (expand):** Show step as failed with message, continue to next step (existing fallback logic handles this)
- **Search fails at Pass 2 (rank):** Show step as failed, still deliver FTS-only results via `SearchCompleted`
- **Complete failure:** Show `SearchFailed` event, display error in results area with retry option
- **Cached results:** Hub detects cache hit, skips progress events, immediately sends `SearchCompleted`
- **Multiple concurrent searches:** Each SignalR connection is independent; new search cancels display of previous progress
- **Browser doesn't support WebSocket:** SignalR auto-negotiates fallback to Long Polling

## Test Plan

- [ ] Backend: Unit test `SearchHub.StartSearch` emits correct progress events in order
- [ ] Backend: Unit test progress callback fires for each pass
- [ ] Backend: Verify existing HTTP POST `/api/search` still works unchanged
- [ ] Frontend: Unit test `search-progress` component renders correct step states
- [ ] Frontend: Unit test `search-hub` service handles connect/disconnect/reconnect
- [ ] Integration: End-to-end search shows progress steps then results
- [ ] Integration: Verify fallback to HTTP POST when SignalR unavailable

## Complexity Assessment

**Recommended execution:** `/build-with-agent-team` (team)
**Rationale:** This touches both backend (SignalR hub, service refactor) and frontend (new service, new component, template changes) with independent work streams that can be parallelized across agents.
