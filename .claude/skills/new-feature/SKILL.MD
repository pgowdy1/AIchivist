---
name: new-feature
description: "Solo implementation of a small, straightforward feature — no agent team needed"
disable-model-invocation: true
argument-hint: "[plan-path]"
---

# New Feature (Solo)

You are implementing a small, well-defined feature by yourself. This skill is for simple changes that don't warrant a multi-agent team. For complex features, use `/build-with-agent-team` instead.

## When to Use This

- Single component or endpoint changes
- Small UI tweaks or additions
- Adding a simple API endpoint with frontend integration
- Bug fixes that touch a few files
- Features where the plan's "Recommended execution" says `/new-feature`

## Step 1: Read the Plan

If a plan path is provided (`$ARGUMENTS`):
```
Read the plan file at $ARGUMENTS
```

If no plan path, check for the most recent plan:
```bash
ls -t .claude/plans/*.md | head -1
```

Read and understand:
- What are we building?
- Which files to modify or create?
- What's the API contract (if any)?
- What are the edge cases?

If no plan exists, ask the user what to build or suggest running `/plan-feature` first.

## Step 2: Implement

Work through the plan's implementation steps in order. Follow these rules:

- **Read before editing** — always read a file before modifying it
- **One concern at a time** — don't mix unrelated changes
- **Follow conventions** — check CLAUDE.md and `.claude/rules/` for patterns
- **Backend first** — if full-stack, implement the API before the frontend
- **Test as you go** — run relevant tests after each significant change

### Backend changes
- Use the correct Anthropic SDK types (see CLAUDE.md)
- Services go in `backend/ArchiveSearch.API/Services/`
- Models go in `backend/ArchiveSearch.Core/Models/`
- Use `AsNoTracking()` for read-only queries

### Frontend changes
- Use signals (`signal()`, `computed()`), not RxJS subjects for state
- Use `@if`, `@for`, `@switch` — not `*ngIf`, `*ngFor`
- Standalone components only
- SCSS with BEM conventions

## Step 3: Test

Run the test suite:

```bash
dotnet test backend/ --verbosity normal
```

```bash
cd frontend && npm test -- --run 2>&1; cd ..
```

If tests fail, fix them before proceeding.

## Step 4: Summary

Output what was done:

```
Done: <feature summary>

Files changed:
- path/to/file — what changed

Tests: all passing
Next step: /review-pr or /commit-pr <title>
```
