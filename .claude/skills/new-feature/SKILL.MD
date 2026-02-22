---
name: new-feature
description: "Orchestrate full feature implementation with parallel agents"
disable-model-invocation: true
argument-hint: <feature-description>
---

# New Feature Orchestration

You are the lead orchestrator for implementing a new feature. Follow these steps precisely.

## Phase 1: Plan

1. Read and understand the feature description: $ARGUMENTS
2. Analyze the existing codebase to understand current patterns and relevant files
3. Create a detailed implementation plan with clearly separable work streams
4. Identify which parts can be parallelized (e.g., backend API, frontend UI, tests)

## Phase 2: Branch & Worktree Setup

Create an isolated worktree for this feature:
```bash
# Create feature branch and worktree
FEATURE_BRANCH="feature/$ARGUMENTS_SLUG"
git worktree add .worktrees/$ARGUMENTS_SLUG -b $FEATURE_BRANCH main

# Initialize the worktree environment
cd .worktrees/$ARGUMENTS_SLUG
# Run project setup (npm install, dotnet restore, etc.)
```

## Phase 3: Delegate to Subagents

Spawn background subagents for each parallel work stream. Each subagent works in the same worktree but on different files.

For each work stream, use the Task tool with `run_in_background: true`:

**Backend Agent**: "Implement the backend API changes for [feature]. Follow all rules in .claude/rules/backend.md. Only modify files in src/Api/ and tests/Api.Tests/. When complete, run `dotnet test` and write a summary to .agent-status/backend.json"

**Frontend Agent**: "Implement the Angular frontend for [feature]. Follow all rules in .claude/rules/frontend.md. Only modify files in src/Web/. When complete, run `npm run test` and write a summary to .agent-status/frontend.json"

**Test Agent**: "Write comprehensive integration tests for [feature]. Follow all rules in .claude/rules/testing.md. Focus on end-to-end test scenarios. When complete, write a summary to .agent-status/tests.json"

## Phase 4: Monitor & Integrate

1. Wait for all agents to complete (check .agent-status/*.json files)
2. Review the combined changes for consistency
3. Run the full test suite: `dotnet test && cd src/Web && npm run test`
4. Fix any integration issues between the work streams
5. Commit all changes with a descriptive message

## Phase 5: Create PR

Once all tests pass:
```bash
git add -A
git commit -m "[FEATURE] $ARGUMENTS"
gh pr create --title "[FEATURE] $ARGUMENTS" --body "$(cat PR_BODY.md)" --base main
```

Write the PR body (PR_BODY.md) including:
- Summary of changes
- Files modified per work stream
- Test coverage added
- Any open questions or known limitations

Then invoke `/review-pr` as a subagent to get an automated review before requesting human review.