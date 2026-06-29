---
name: issue-to-pr
description: Implement the work described in a GitHub issue, run tests, and open a PR
mode: "agent"
---

You are implementing the work described in this issue. Read the full issue body — it contains the complete plan including current behavior, affected layers, end-to-end flow, test coverage requirements, assumptions, and risks.

Goal:
- Implement only what is required by the issue plan and acceptance criteria.
- Prefer the smallest safe change that fully satisfies the issue.

Process:
1. Inspect the relevant code and identify the best implementation path.
2. Make the code changes.
3. Add or update unit tests for the behavior.
4. Run the relevant test suite(s) and fix failures.
5. If needed, update documentation or comments directly tied to the change.
6. Prepare the branch for review and open a PR.

Rules:
- Do not ask me for clarification unless a requirement is truly ambiguous and blocks progress.
- Do not broaden scope beyond the issue.
- Keep the implementation consistent with existing patterns in the codebase.
- Ensure the final PR description includes:
  - what changed
  - tests run
  - any follow-up notes
- Link the PR to this issue with "Closes #<issue-number>".

Definition of done:
- Code implemented
- Tests added or updated
- Relevant tests pass
- PR opened
- Issue linked in PR