---
name: test
description: Run all backend and frontend tests and report results
disable-model-invocation: true
---

# Run Tests

Run the full test suite across both backend and frontend, then report results clearly.

## Step 1: Backend Tests

```bash
dotnet test backend/ --verbosity normal
```

Capture the output. Look for:
- Total tests run
- Passed / Failed / Skipped counts
- Any failure details (test name, assertion message, stack trace)

## Step 2: Frontend Tests

```bash
cd frontend && npm test -- --run 2>&1; cd ..
```

The `--run` flag runs Vitest in single-run mode (no watch).

Capture the output. Look for:
- Total tests run
- Passed / Failed counts
- Any failure details

## Step 3: Report

Present a clear summary:

```
Test Results
────────────────────────────
Backend:  ✓ X passed, ✗ Y failed, ⊘ Z skipped
Frontend: ✓ X passed, ✗ Y failed

Overall:  PASS ✓  or  FAIL ✗
```

**If any tests failed:**
- List each failing test with its error message
- Identify the likely cause if obvious from the error
- Suggest a fix if the cause is clear

**If all tests passed:**
- Just show the summary, no extra commentary needed
