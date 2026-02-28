---
name: full-feature
description: "End-to-end automated feature pipeline: branch, plan, build, test, optimize, commit, review, wrap-up — all in one command"
argument-hint: <feature-description>
---

# Full Feature Pipeline

You are running the complete feature development pipeline from start to finish. Execute each phase in order, transitioning automatically between phases. The only user interaction is answering planning questions in Phase 2.

**Feature:** `$ARGUMENTS`

**Pipeline:**
```
1. Fresh Start → 2. Plan → 3. Build → 4. Test → 5. Optimize → 6. Test → 7. Commit & PR → 8. Review → 9. Wrap-Up
```

**CRITICAL RULES:**
- After completing each phase, **immediately proceed to the next phase**. Do NOT stop to say "Next step: run /skill-name." Do NOT wait for the user to invoke the next skill.
- The only time you STOP is when a phase **fails** (tests fail, merge conflicts, build errors). Report the issue, fix it, and then continue.
- Phase 2 (Plan) requires user answers to questions — that's expected. After the user answers, continue automatically.

---

## Phase 1: Fresh Start

### 1.1 Check Git Status

```bash
git status
```

If there are **any** uncommitted changes:
1. List the dirty files
2. **STOP** — ask the user via AskUserQuestion:
   - **Stash** the changes (`git stash push -m "WIP before feature"`)
   - **Commit** them to the current branch first
   - **Abort** — let the user handle it
3. Execute the user's choice before continuing.

If clean, proceed.

### 1.2 Sync with Main

```bash
git checkout main
git pull origin main
```

If merge conflicts or pull failures, **STOP** and report.

### 1.3 Create Feature Branch

Generate a URL-safe slug from `$ARGUMENTS`:
- Lowercase, hyphens for spaces/special chars, collapse multiple hyphens, ~40 chars max

```bash
git checkout -b feature/<slug>
```

Output status, then **immediately proceed to Phase 2**.

---

## Phase 2: Plan Feature

### 2.1 Codebase Reconnaissance

Before asking questions:
1. Read `CLAUDE.md` for architecture and conventions
2. Use Grep/Glob to scan for files related to `$ARGUMENTS`
3. Identify relevant components, services, endpoints, models

### 2.2 Round 1 Questions (Foundational)

Use AskUserQuestion to ask 3-4 questions:

1. **Layers** — "Which layers does this feature touch?"
   - Options: Frontend only, Backend only, Full-stack (frontend + backend), Full-stack + database changes
2. **Scope** — "How would you describe the scope?"
   - Options: Small (single component/endpoint tweak), Medium (new component or endpoint), Large (multiple components, endpoints, and/or data model changes)
3. **UX intent** — "What should the user experience look like?" (open-ended)
4. **Existing vs new** — "Are we modifying existing functionality or building something net new?"
   - Options: Modify existing, Build new, Both

### 2.3 Round 2 Questions (Targeted)

Based on Round 1, ask 2-4 follow-ups relevant to the layers/scope:
- **Frontend:** Component placement, UI patterns (dialog, inline, sidebar, page)
- **Backend:** New endpoints, AI/Claude integration needs
- **Database:** Data shapes, FTS5 impact
- **AI/Search pipeline:** Which pass affected, prompt changes
- **Large scope:** Milestones, MVP, dependencies

### 2.4 Round 3 Questions (Edge Cases)

1. **Error scenarios** — what should happen when things go wrong?
2. **Installer impact** — only if new files/deps/config are involved

### 2.5 Produce the Plan

Write plan to `.claude/plans/<slug>.md` with the standard structure:
- Feature name, branch, scope, layers
- Description, requirements, affected files (existing + new)
- API contract (if backend), data model changes (if DB)
- Implementation steps ordered by layer (backend first)
- Edge cases & error handling
- Test plan
- **Complexity assessment:** recommend solo (`/new-feature`) or team (`/build-with-agent-team`)

**Immediately proceed to Phase 3.**

---

## Phase 3: Build

Read the plan from `.claude/plans/<slug>.md`. Check the complexity assessment.

### Path A: Solo Build (plan says small/medium, single layer, or recommends `/new-feature`)

Work through the plan's implementation steps in order:

- **Read before editing** — always read a file before modifying it
- **Backend first** — if full-stack, implement the API before the frontend
- **Follow conventions** — CLAUDE.md and `.claude/rules/`
- **One concern at a time** — don't mix unrelated changes

Backend rules: Services in `API/Services/`, models in `Core/Models/`, `AsNoTracking()` for reads, correct Anthropic SDK types per CLAUDE.md.

Frontend rules: Signals (`signal()`, `computed()`), `@if`/`@for`/`@switch`, standalone components, SCSS+BEM.

### Path B: Agent Team Build (plan says large, 3+ layers, or recommends `/build-with-agent-team`)

1. Determine team structure from plan (2-5 agents based on layers)
2. Define agent roles, ownership boundaries, and cross-cutting concerns
3. Map the **contract chain**: Database → Backend → Frontend
4. **Spawn upstream agents first** — their first task is publishing their contract
5. **Receive and verify each contract** — exact URLs, JSON shapes, status codes, SSE format
6. **Forward verified contracts to downstream agents** with "build to this exactly"
7. Agents build in parallel once contracts are verified
8. Run **contract diff** before integration (compare backend endpoints vs frontend fetch URLs)
9. Run end-to-end validation after all agents complete

**After build completes, immediately proceed to Phase 4.**

---

## Phase 4: Test

### 4.1 Backend Tests

```bash
cmd.exe /c "cd /d C:\Users\pgowd\Documents\WSU_Archive_Search_Tool_New && dotnet test backend\ArchiveSearch.Tests --verbosity normal"
```

### 4.2 Frontend Tests

```bash
cd /mnt/c/Users/pgowd/Documents/WSU_Archive_Search_Tool_New/frontend && npm test 2>&1
```

### 4.3 Evaluate

Report results:
```
Phase 4: Test Results
Backend:  X passed, Y failed
Frontend: X passed, Y failed
```

**If any tests FAIL:** Fix the failures, re-run tests, repeat until all pass. Do NOT proceed until all tests pass.

**When all pass, immediately proceed to Phase 5.**

---

## Phase 5: Optimize

### 5.1 Determine Scope

```bash
git diff --name-only main...HEAD
```

### 5.2 Read Conventions

Read: `CLAUDE.md`, `.claude/rules/backend.md` (if backend files changed), `.claude/rules/frontend.md` (if frontend files changed), `.claude/rules/testing.md` (if test files changed)

### 5.3 Analyze & Apply

For each changed file, check for and fix:
- Deeply nested conditionals → flatten with early returns/guard clauses
- Unused imports, variables, parameters
- Over-engineered abstractions for one-time operations
- Convention violations (signals vs subjects, `@if` vs `*ngIf`, `AsNoTracking()`, etc.)

**Do NOT change:** Working clear logic, "why" comments, test assertions, public API contracts.

Use Edit tool for targeted modifications. One concern per edit.

**Immediately proceed to Phase 6.**

---

## Phase 6: Test (Verify Optimization)

Run the **exact same test commands** as Phase 4.

**If any test fails due to an optimization change:** Immediately revert that optimization and re-run tests. Note the reverted change as "skipped — would break tests."

**When all pass, immediately proceed to Phase 7.**

---

## Phase 7: Commit & Create PR

### 7.1 Stage All Changes

```bash
git add -A
```

### 7.2 Analyze Changes

```bash
git diff --cached --stat
git diff --cached
```

### 7.3 Commit

Generate a conventional commit message (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`) summarizing the feature.

Write the commit message to a file and commit:
```bash
echo "<type>: <description>" > .git/COMMIT_MSG
cmd.exe /c "cd /d C:\Users\pgowd\Documents\WSU_Archive_Search_Tool_New && git commit -F .git\COMMIT_MSG"
rm .git/COMMIT_MSG
```

### 7.4 Push

```bash
git push -u origin HEAD
```

### 7.5 Create PR

Write the PR body to a temp file with:
- **Summary**: 2-3 sentences on what this PR does
- **Changes**: Bullet list grouped by area (Backend, Frontend, Tests, Database)
- **Testing**: What was tested and how

Then create the PR:
```bash
powershell.exe -Command "gh pr create -t '<title>' --body-file .git/PR_BODY.md --base main"
```

**Capture the PR number** from the output — you'll need it for Phase 8.

Clean up temp file:
```bash
rm .git/PR_BODY.md
```

**Immediately proceed to Phase 8.**

---

## Phase 8: Review PR

### 8.1 Get the Diff

```bash
cmd.exe /c "gh pr diff <pr-number>"
```

### 8.2 Review Checklist

1. **Security** — SQL injection, XSS, auth gaps, secrets in code, insecure deserialization, missing input validation
2. **Logic & Correctness** — null refs, race conditions, missing error handling, edge cases, async patterns
3. **Architecture** — follows CLAUDE.md patterns, justified dependencies, proper separation of concerns
4. **Test Coverage** — public methods tested, edge cases covered, meaningful assertions
5. **Completeness** — migrations included if schema changed, docs updated if API changed, TODOs resolved

### 8.3 Write Review

Structure:
- **Summary** — merge-ready, needs minor fixes, or needs rework
- **Critical Issues (Must Fix)** — blocking problems with file:line references
- **Suggestions (Nice to Have)** — non-blocking improvements
- **What Looks Good** — acknowledge good work

### 8.4 Post Review to PR

Write review to a file, then post:
```bash
powershell.exe -Command "gh pr comment <pr-number> --body-file .git/REVIEW.md"
rm .git/REVIEW.md
```

**If critical issues are found:** Fix them, commit with `fix:` prefix, push, and re-review. Repeat until the review passes.

**When review passes, immediately proceed to Phase 9.**

---

## Phase 9: Wrap-Up

### 9.1 Ship It

Verify everything is committed and pushed:
```bash
git status
git log origin/HEAD..HEAD
```

Push any remaining changes.

### 9.2 Remember It

Review what was learned during this pipeline run. Save to the appropriate location:
- **Auto memory** (`~/.claude/projects/.../memory/`) — debugging insights, patterns, project quirks
- **CLAUDE.md** — permanent project rules, conventions, architecture decisions
- **`.claude/rules/`** — topic-specific rules scoped to file types
- **`CLAUDE.local.md`** — private per-project notes

### 9.3 Review & Apply

Analyze the session for self-improvement:
- **Skill gaps** — things that needed multiple attempts
- **Friction** — repeated manual steps that should be automatic
- **Knowledge** — facts that should be remembered
- **Automation** — patterns that could become skills or hooks

Auto-apply all actionable improvements. Present a summary of what was applied.

---

## Final Report

```
Feature Pipeline Complete
═══════════════════════════════════════
Feature:  $ARGUMENTS
Branch:   feature/<slug>
PR:       #<number> — <url>
Tests:    All passing
Review:   Posted

Phases completed:
  1. Fresh Start
  2. Plan Feature
  3. Build
  4. Test
  5. Optimize
  6. Verify Tests
  7. Commit & PR
  8. Review
  9. Wrap-Up
═══════════════════════════════════════
```
