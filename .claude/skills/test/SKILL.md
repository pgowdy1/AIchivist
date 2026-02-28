---
name: test
description: Run all backend and frontend tests and report results
disable-model-invocation: true
---

# Run Tests

Run the full test suite across both backend and frontend, then report results clearly.

## Step 1: Backend Tests

```bash
cmd.exe /c "cd /d C:\Users\pgowd\Documents\WSU_Archive_Search_Tool_New && dotnet test backend\ArchiveSearch.Tests --verbosity normal"
```

Capture the output. Look for:
- Total tests run
- Passed / Failed / Skipped counts
- Any failure details (test name, assertion message, stack trace)

## Step 2: Frontend Tests

```bash
cd /mnt/c/Users/pgowd/Documents/WSU_Archive_Search_Tool_New/frontend && npm test 2>&1
```

`npm test` already runs in single-run mode (`ng test --no-watch`). Do NOT pass `--run` — it is not a valid Angular CLI flag.

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
