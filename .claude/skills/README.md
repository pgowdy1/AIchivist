# Skills

Slash-command skills for building features in AIchivist. Each skill handles one step of the development workflow — run them in order or pick the ones you need.

## The Pipeline

```
/fresh-start  →  /plan-feature  →  /new-feature or /build-with-agent-team  →  /test  →  /review-pr  →  /commit-pr  →  /wrap-up
```

### Step 0: `/fresh-start <feature description>`

Prepares a clean branch before any work begins.

- Checks `git status` — if dirty, stops and asks you what to do (stash, commit, or abort)
- Pulls latest from `main`
- Creates `feature/<slug>` branch from main

```
/fresh-start add collection export to CSV
→ Creates branch: feature/add-collection-export-to-csv
```

### Step 1: `/plan-feature <feature description>`

Interactive requirements gathering that produces a plan file.

- Scans the codebase for relevant files
- Asks 3 rounds of adaptive questions (layers, scope, UX, edge cases)
- Writes a structured plan to `.claude/plans/<slug>.md`
- Recommends `/new-feature` (solo) or `/build-with-agent-team` (team) based on complexity

```
/plan-feature add collection export to CSV
→ Saves plan to .claude/plans/add-collection-export-to-csv.md
```

### Step 2a: `/new-feature [plan-path]`

Solo implementation for small, straightforward features.

- Reads the plan (or the most recent plan if no path given)
- Implements backend first, then frontend
- Runs tests after each significant change
- Use this when the plan says scope is **small** or **medium** with a single layer

```
/new-feature .claude/plans/add-collection-export-to-csv.md
```

### Step 2b: `/build-with-agent-team [plan-path] [num-agents]`

Multi-agent build for complex features spanning multiple layers.

- Reads the plan and determines team structure
- Spawns agents in dependency order (database → backend → frontend)
- Enforces contract-first protocol — upstream agents publish API contracts before downstream agents build
- Lead verifies and relays all contracts between agents
- Runs end-to-end validation after all agents complete
- Use this when the plan says scope is **large** or touches **3+ layers**

```
/build-with-agent-team .claude/plans/add-collection-export-to-csv.md 3
```

### Step 3: `/test`

Runs the full test suite across both stacks.

- Backend: `dotnet test backend/`
- Frontend: `npm test --run` (Vitest single-run mode)
- Reports pass/fail counts and failure details

```
/test
→ Backend: 12 passed, 0 failed
→ Frontend: 8 passed, 0 failed
→ Overall: PASS
```

### Step 4: `/review-pr [PR-number]`

Automated code review before merging.

- Reviews the diff against main (or a specific PR number)
- Checks: security, logic/correctness, architecture, test coverage, completeness
- Categorizes findings as Critical (must fix), Suggestions (nice to have), and What Looks Good
- Posts the review as a PR comment if a PR number is given

```
/review-pr        # reviews current branch diff
/review-pr 42     # reviews PR #42
```

### Step 5: `/commit-pr <title>`

Stages, commits, pushes, and creates a pull request.

- Stages all changes
- Generates a conventional commit message (`feat:`, `fix:`, `refactor:`, etc.)
- Pushes to origin
- Creates a PR with summary, grouped changes, and test notes

```
/commit-pr "Add CSV export for collections"
```

### Step 6: `/wrap-up`

End-of-session checklist. Run when you're done for the day.

- **Ship** — commits and pushes any remaining changes
- **Remember** — saves learnings to the right memory location (CLAUDE.md, auto-memory, rules)
- **Review** — identifies skill gaps and friction from the session, auto-applies improvements

```
/wrap-up
```

## Utility Skills

These can be used anytime, outside the main pipeline.

### `/optimize [file-or-scope]`

Simplifies code and enforces project conventions.

- Pass a file path, component name, or "recent" (default: all changes since branching from main)
- Flattens nested conditionals, removes dead code, enforces Angular/C# patterns
- Runs tests after changes — reverts anything that breaks

```
/optimize frontend/src/app/components/search-bar/search-bar.ts
/optimize recent
/optimize         # defaults to all changed files
```

### `/prime`

Bootstraps context by reading key project files. Useful at the start of a session when you need Claude to understand the codebase before doing anything.

```
/prime
```

## Typical Session

A full feature session looks like this:

```
/fresh-start add related collections sidebar
/plan-feature add related collections sidebar
  ... answers questions ...
/new-feature .claude/plans/add-related-collections-sidebar.md
/test
/optimize recent
/test
/review-pr
/commit-pr "Add related collections sidebar"
/wrap-up
```

For a complex feature:

```
/fresh-start redesign search results with faceted filtering
/plan-feature redesign search results with faceted filtering
  ... answers questions ...
/build-with-agent-team .claude/plans/redesign-search-results-with-faceted-filtering.md
/test
/optimize recent
/test
/review-pr
/commit-pr "Redesign search results with faceted filtering"
/wrap-up
```

For a quick fix that doesn't need planning:

```
/fresh-start fix search bar placeholder text
  ... make the change directly ...
/test
/commit-pr "Fix search bar placeholder text"
```
