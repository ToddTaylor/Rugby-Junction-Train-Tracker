# AI Feature Handoff Specification

Use this document when a GitHub issue is ready for AI implementation. The issue should already contain the complete plan created cooperatively by developer + AI Agent using `/plan-feature` during the planning phase.

## Purpose

Create a consistent, auditable workflow from human planning through AI implementation and final merge—with explicit approval checkpoints at code review and merge stages.

## Workflow Summary

| Phase | Owner | Action | Invocation | Tool/Prompt |
|-------|-------|--------|------------|-------------|
| **1. Plan** | Developer + AI Agent | Developer types `/plan-feature` + brief description; cooperates with AI Agent to produce plan | `/plan-feature` | Copilot Chat prompt (`plan-feature.prompt.md`) |
| **2. Issue** | Developer + AI Agent | Developer reviews plan output; confirms AI Agent creates GitHub issue with plan as body | Manual | GitHub issue |
| **3. Label** | AI Agent (automated) | *(Automated)* AI Agent classifies issue and applies `bug` or `enhancement` label | Automated | GitHub labels (bug/enhancement) |
| **4. Plan Review** | Developer | 🔵 **Checkpoint A:** Developer reads plan in issue; applies `feature-ready-for-ai` label to approve (or edits issue and re-plans if changes needed) | — | GitHub issue labels |
| **5. Implement** | AI Agent | *(Automated)* AI Agent reads issue, creates branch, implements feature, opens PR | `/issue-to-pr` | Copilot Chat prompt (`issue-to-pr.prompt.md`) |
| **6. CI** | Automation | *(Automated)* Tests, lint, and security checks run on the PR | — | GitHub Actions |
| **7. PR Review** | Developer | 🔵 **Checkpoint B:** Developer reviews PR code and tests; approves or requests changes | — | GitHub PR review |
| **8. Merge PR** | Developer | 🔵 **Checkpoint C:** Developer confirms CI passes and merges the PR | — | GitHub merge |

---

## 1) Planning Phase (Developer + AI Agent)

### Cooperative Planning

- **Owner:** Developer
- **Tool:** Copilot Chat using `/plan-feature`
- **Prompt file:** `plan-feature.prompt.md`
- **Input:** Feature request / bug description
- **Output:** Detailed plan (to be posted as GitHub issue)

### Plan Content

The cooperatively created plan should include:

- **Current behavior:** How the system works now (if fix) or desired new behavior (if feature)
- **Affected layers:** Backend, frontend, database, API, tests, docs—what changes
- **End-to-end flow:** Step-by-step user journey or system flow after implementation
- **Test coverage:** Unit tests, integration tests, edge cases to cover
- **Assumptions:** Made during planning
- **Risks/unknowns:** Potential issues, blockers, clarifications needed

### Issue Creation

- Developer posts plan as GitHub issue (plan is the issue body)
- Issue includes all plan details in structured format
- Issue is labeled with relevant tags (`enhancement`, `bug`, etc.)

---

## 2) Plan Review (Developer) — Checkpoint A

- **Owner:** Developer (Todd Taylor)
- **Action:** Reviews plan posted in issue
- **Decision options:**
  - **Approve:** Apply label `feature-ready-for-ai` to the issue — this triggers AI implementation
  - **Request changes:** Plan has gaps or flaws; Developer + AI Agent refine and update issue, then apply label when ready
  - **Reject:** Plan is fundamentally wrong; close issue or restart

**This checkpoint is mandatory.** AI implementation does not start until the `feature-ready-for-ai` label is applied.

---

## 3) AI Implementation Phase

### AI Execution: Code Implementation

- **Owner:** AI Agent
- **Invocation:** `/issue-to-pr`
- **Tool/Prompt:** Copilot Chat prompt (`issue-to-pr.prompt.md`)
- **Input:** Issue (with plan) + architecture/area instructions
- **Output:** PR with code, tests, and documentation

### AI Implementation Steps

1. **Read issue** containing the plan
2. **Inspect existing code** and tests (no blind changes)
3. **Implement feature** based on plan
4. **Write comprehensive tests** as specified in plan
5. **Run CI checks** locally
6. **Update documentation** (if applicable)
7. **Open PR** with description linking back to issue and plan

### Human Code Review (Checkpoint B)

- **Owner:** Developer (Todd Taylor)
- **Action:** Reviews PR (code, tests, docs)
- **Decision options:**
  - **Approve:** Code meets plan and quality standards
  - **Request changes:** Code needs refinement before merge
  - **Reject:** Code doesn't match plan

**Recovery:** If changes requested, AI Agent refines code, re-runs CI, updates PR. Developer re-reviews.

### CI Checks

All automated checks must pass:

- Unit tests
- Integration/UI tests (where applicable)
- Lint/type/build checks
- Security checks
- Code coverage (if enforced)

### Merge Gate (Checkpoint C)

- **Owner:** Developer (Todd Taylor)
- **Action:** Confirms all CI checks pass and PR is approved
- **Decision:** Merge
- **Post-merge:** Close related issue

---

## 4) Quality Gates

All of these must be satisfied before merge:

- ✅ Issue created with complete plan
- ✅ Developer approved plan at Checkpoint A (`feature-ready-for-ai` label applied)
- ✅ Code reviewed and approved by Developer (Checkpoint B)
- ✅ All CI checks pass
- ✅ No merge conflicts
- ✅ Implementation matches plan
- ✅ Developer approves merge (Checkpoint C)

---

## 5) Failure Modes and Recovery

### Common Failure Modes

| Failure | Root Cause | Recovery |
|---------|-----------|----------|
| Plan is incomplete | Developer + AI Agent missed key details | Developer requests changes during plan review; Developer + AI Agent refine issue, re-apply `feature-ready-for-ai` |
| Code doesn't match plan | AI Agent deviated during implementation | Developer requests changes in Checkpoint B; AI Agent refines code |
| Tests are narrow/brittle | AI Agent wrote only happy-path tests | Developer requests additional test coverage in Checkpoint B |
| Out-of-scope changes | AI Agent changed unrelated code | Developer rejects PR in Checkpoint B |
| CI failures on merge | Environment issues | Developer investigates in Checkpoint C; if AI-caused, revert and reopen issue |

### Recovery Procedure

1. **During plan review:** Developer + AI Agent refine plan in issue and re-apply `feature-ready-for-ai` label when ready
2. **During code review:** AI Agent refines code, re-runs CI, updates PR, Developer re-reviews
3. **At merge:** Investigate CI failure; if AI Agent-caused, revert PR and reopen issue with learnings

---

## 6) Ownership

- **Workflow owner:** Todd Taylor
- **Developer (all checkpoints A, B, C):** Todd Taylor

---

## 7) Per-Feature Handoff Record (Template)

Copy this template and fill in for each feature:

```
## 🤖 AI Handoff Record

### Issue & Plan
- **Issue #:** `<number>`
- **Title:** `<text>`
- **Plan approved at:** `<timestamp>` (`feature-ready-for-ai` label applied at Checkpoint A)

### AI Execution
- **Implementation agent:** `AI Agent`
- **Slash command:** `/issue-to-pr`
- **Prompt file:** `issue-to-pr.prompt.md`
- **PR link:** `<link>`

### Human Reviews
- **Checkpoint A (Developer Plan Review):**
  - Decision: `Approved (label applied)` / `Changes requested` / `Rejected`
  - Plan iterations: `<count>`
  
- **Checkpoint B (Developer Code Review):**
  - Decision: `Approved` / `Changes requested` / `Rejected`
  - Code review rounds: `<count>`
  
- **Checkpoint C (Developer Merge):**
  - Decision: `Merged` / `Blocked`
```

---

## 8) Prompts Used

This workflow relies on two reusable prompts:

1. **`/plan-feature`** (file: `plan-feature.prompt.md`) — Used by Developer + AI Agent during planning phase
   - Guides cooperative discussion to create detailed plan
   - Covers current behavior, affected layers, E2E flow, test coverage, risks
   
2. **`/issue-to-pr`** (file: `issue-to-pr.prompt.md`) — Used by AI Agent during implementation phase
   - Guides AI to implement based on plan posted in issue
   - Includes code generation, testing, PR opening

Both prompts are stored in `.github/prompts/` and versioned with the repo.
