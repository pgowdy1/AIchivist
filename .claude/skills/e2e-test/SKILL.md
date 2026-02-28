---
name: e2e-test
description: Run end-to-end UI tests using Playwright MCP — opens a real browser and walks through every UI flow
---

# E2E UI Test Suite

You are running a manual E2E test of the AIchivist application using Playwright MCP tools. Walk through each flow in order, verifying behavior with `browser_snapshot` (primary) and `browser_take_screenshot` (visual checkpoints). Track pass/fail for each flow and report results at the end.

## Prerequisites

### 1. Verify Playwright MCP is connected

Check that `mcp__playwright__browser_navigate` and other `mcp__playwright__*` tools are available. If they are NOT available, tell the user: "Playwright MCP server is not connected. Please restart Claude Code and run `/e2e-test` again." Then **STOP**.

### 2. Verify servers are running

```bash
# Frontend runs in WSL — use regular curl
curl -s -o /dev/null -w "%{http_code}" http://localhost:4200
# Backend runs on Windows — use cmd.exe curl (WSL2 can't reach Windows localhost directly)
cmd.exe /c "curl -s -o NUL -w \"%{http_code}\" http://localhost:5265/api/health"
```

If **either** returns a non-200 status or fails to connect, **start them automatically**:

1. Start the backend in the background: `cmd.exe /c "dotnet run --project backend/ArchiveSearch.API"` (use `run_in_background`)
2. Start the frontend in the background: `cd frontend && npm start` (use `run_in_background`)
3. **Kill stale processes first:** If the backend build fails due to locked DLLs (MSB3026/MSB3027), find and kill the stale AIchivist process via `cmd.exe /c "taskkill /F /IM AIchivist.exe"` and retry
4. **Poll for readiness:** Every 5 seconds (up to 60 seconds), re-run the curl checks until both return 200. Use `cmd.exe /c curl` for the backend check.
5. If either server fails to start after 60 seconds, **STOP** and report which server(s) failed

## Test Environment Setup

1. Use `browser_navigate` to open `http://localhost:4200`
2. Use `browser_snapshot` to capture the initial page state
3. Use `browser_take_screenshot` with `filename: "screenshots/initial-load.png"` — label it "initial load"
4. Determine which page loaded:
   - If the snapshot contains "Welcome to AIchivist" or an API key input → **Setup page** (run Flow 1)
   - If the snapshot contains "Search archive collections" → **Home page** (skip Flow 1)

Initialize a results tracker in your working memory:

```
Flow 1: Setup Page       — PENDING
Flow 2: Search           — PENDING
Flow 3: Result Card      — PENDING
Flow 4: Additional Results — PENDING
Flow 5: Chat Sidebar     — PENDING
Flow 6: Settings Dialog  — PENDING
Flow 7: Example Queries  — PENDING
```

---

## Flow 1: Setup Page (Conditional)

**Skip if:** Home page loaded (search bar visible). Mark as **SKIPPED**.

**Steps:**
1. `browser_snapshot` — verify: title "Welcome to AIchivist", API key input (`#apiKeyInput`), disabled submit button
2. `browser_type` into the API key input: type a test value (e.g., `sk-ant-test123`)
3. `browser_snapshot` — verify: submit button is now enabled
4. `browser_click` the "Show" toggle button next to the input
5. `browser_snapshot` — verify: input type changed to text (key is visible)
6. `browser_click` the "Save & Continue" button
7. Wait for navigation — `browser_snapshot` until the home page appears (search bar visible)
8. `browser_take_screenshot` with `filename: "screenshots/setup-complete.png"` — label "setup complete"

**Pass criteria:** Redirected to home page with search bar visible.
**Note:** This requires a REAL API key. If using a fake key, the backend will reject it. Mark as **BLOCKED** and continue.

---

## Flow 2: Search

**Steps:**
1. `browser_snapshot` — verify: search input with placeholder "Search archive collections...", disabled Search button, 4 example query chips under "Try:" label
2. `browser_click` the search input, then `browser_type` the text `WW` (only 2 chars)
3. `browser_snapshot` — verify: Search button is still disabled (minimum 3 characters required)
4. Clear the input: select all text (`browser_press_key` Ctrl+A) then `browser_type` the query: `Find collections related to World War II at Washington State`
5. `browser_snapshot` — verify: Search button is now enabled
6. `browser_click` the Search button (CSS: `.search-button`)
7. **Poll for progress:** Every 3-5 seconds, `browser_snapshot` and check:
   - Look for step indicators (`.step--active`, `.step--completed`)
   - "Expanding query" → "Searching database" → "Ranking results"
   - Continue polling until results appear or 60 seconds elapse
8. `browser_snapshot` — verify: results panel visible with text matching `Found X collections for "..."`
9. `browser_take_screenshot` with `filename: "screenshots/search-results.png"` — label "search results"
10. Verify in the snapshot: at least 1 result card with a title, relevance score (e.g., `X/10`), and relevance explanation text

**Pass criteria:** Search completed, results panel visible with at least 1 ranked result card.

---

## Flow 3: Result Card Expansion

**Steps:**
1. `browser_snapshot` — identify the first result card
2. Verify the card has: title, collection ID (`.collection-id`), relevance score bar, relevance explanation, subject chips
3. `browser_click` the "Show more details" button (CSS: `.expand-button`) on the first card
4. `browser_snapshot` — verify expanded content appeared:
   - Look for section headings: "Abstract", "Scope & Content", "Historical Note", "Series", "People", "Places" (some may be absent depending on data)
   - Look for "Related Collections" section with "Show related collections" button
5. `browser_click` "Show related collections" (CSS: `.load-related-btn`)
6. `browser_snapshot` — wait for loading to complete. Verify either:
   - Related collection cards appeared (with titles and overlap summaries), OR
   - Text "No strongly related collections found."
7. `browser_take_screenshot` with `filename: "screenshots/expanded-card-with-related.png"` — label "expanded card with related"
8. `browser_click` "Show less" (the same `.expand-button`, now showing "Show less")
9. `browser_snapshot` — verify card collapsed (expanded content no longer visible)

**Pass criteria:** Card expanded showing details, related collections loaded, card collapsed.

---

## Flow 4: Additional Results (Conditional)

**Skip if:** No additional results section visible in the snapshot. Mark as **SKIPPED**.

**Steps:**
1. `browser_snapshot` — verify: text showing "X additional matches" with a toggle button (CSS: `.show-additional-btn`)
2. `browser_click` the "Show X additional matches" button
3. `browser_snapshot` — verify: additional result cards appeared, and they do NOT have rank badges or relevance scores (they are unranked)
4. `browser_click` "Hide additional matches" button
5. `browser_snapshot` — verify: additional cards are hidden again

**Pass criteria:** Additional results toggled on and off successfully.

---

## Flow 5: Chat Sidebar

**Steps:**
1. `browser_snapshot` — verify chat sidebar is visible with:
   - Header: "Ask about these results"
   - Empty state text: "Ask a follow-up question about the search results."
   - Textarea with placeholder "Ask about these collections..."
   - Disabled Send button (empty input)
2. `browser_click` the chat textarea (CSS: `.chat-input`)
3. `browser_type` the text: `Which of these collections has the most primary source documents?`
4. `browser_snapshot` — verify: Send button is now enabled
5. `browser_click` the Send button (CSS: `.send-button`)
6. `browser_snapshot` — verify: user message appears in the chat thread
7. **Poll for response:** Every 3-5 seconds, `browser_snapshot` and look for:
   - "Thinking..." indicator (CSS: `.thinking-indicator`) during processing
   - An assistant message appearing after the user message
   - Continue polling until assistant response appears or 60 seconds elapse
8. `browser_snapshot` — verify: assistant response message visible in the thread
9. `browser_take_screenshot` with `filename: "screenshots/chat-single-turn.png"` — label "chat single turn"
10. Send a follow-up: `browser_click` the textarea, `browser_type`: `Are any of those collections digitized?`
11. `browser_click` Send
12. Poll for response (same as step 7)
13. `browser_snapshot` — verify: 4 messages total (2 user, 2 assistant) in the thread
14. `browser_take_screenshot` with `filename: "screenshots/chat-multi-turn.png"` — label "chat multi-turn"

**Pass criteria:** Single-turn and multi-turn chat working, messages display correctly.

---

## Flow 6: Settings Dialog

**Steps:**
1. `browser_click` the settings gear button (CSS: `.header-settings-btn`, aria-label: "Settings")
2. `browser_snapshot` — verify dialog opened with:
   - Title: "Settings"
   - Section: "Anthropic API Key"
   - Input field (`#settingsApiKeyInput`)
   - Disabled "Save API Key" button (empty input)
   - Close button (CSS: `.settings-close`, aria-label: "Close settings")
3. `browser_click` the API key input, `browser_type`: `test-key-value`
4. `browser_snapshot` — verify: "Save API Key" button is now enabled
5. `browser_click` the close button (CSS: `.settings-close`) — close WITHOUT saving
6. `browser_snapshot` — verify: dialog closed, home page visible with search results still intact
7. `browser_take_screenshot` with `filename: "screenshots/settings-closed.png"` — label "settings closed"

**Pass criteria:** Settings dialog opens with correct elements, closes without side effects.

---

## Flow 7: Example Queries

**Steps:**
1. Scroll up to the search bar area if needed
2. `browser_snapshot` — verify: example query chips are visible (they hide during loading)
3. `browser_click` the second example query chip (should contain text about student protests or another example query)
4. `browser_snapshot` — verify: search input now contains the example query text
5. **Poll for results:** Same as Flow 2 step 7 — wait for search to complete
6. `browser_snapshot` — verify: new results appeared (different from the first search)
7. `browser_take_screenshot` with `filename: "screenshots/example-query-results.png"` — label "example query results"

**Pass criteria:** Example query chip populated the search input and triggered a new search with results.

---

## Cleanup

1. `browser_close` to close the browser

## Test Report

Present the final report in this exact format:

```
E2E Test Report — AIchivist
═══════════════════════════════════════════════
Flow 1: Setup Page           [PASS | FAIL | SKIPPED | BLOCKED]
Flow 2: Search               [PASS | FAIL]
Flow 3: Result Card          [PASS | FAIL]
Flow 4: Additional Results   [PASS | FAIL | SKIPPED]
Flow 5: Chat Sidebar         [PASS | FAIL]
Flow 6: Settings Dialog      [PASS | FAIL]
Flow 7: Example Queries      [PASS | FAIL]
═══════════════════════════════════════════════
Overall: X/Y passed, Z skipped

Screenshots captured:
- initial load
- search results
- expanded card with related
- chat single turn
- chat multi-turn
- settings closed
- example query results
```

For any **FAIL** results, include:
- **Expected:** what should have happened
- **Actual:** what the snapshot/screenshot showed
- **Suggested investigation:** which component or service to check
