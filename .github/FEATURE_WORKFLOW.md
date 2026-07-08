# AI Feature Delivery Workflow

Complete AI-assisted feature delivery: Developer + AI Agent create plan → issue posted with plan → AI implements → review checkpoints → merge.

## Workflow Summary

| Phase | Owner | Action | Invocation | Tool/Prompt |
|-------|-------|--------|------------|-------------|
| **1. Plan** | Developer + AI Agent | Developer types `/plan-feature` + brief description; cooperates with AI Agent to produce plan | `/plan-feature` | Copilot Chat prompt (`plan-feature.prompt.md`) |
| **2. Issue** | Developer + AI Agent | Developer reviews plan output; confirms AI Agent creates GitHub issue with plan as body | Manual | GitHub issue |
| **3. Label** | AI Agent (automated) | *(Automated)* AI Agent classifies issue and applies `bug` or `enhancement` label | Automated | GitHub labels (bug/enhancement) |
| **4. Plan Review** | Developer | 🔵 **Checkpoint A:** Developer reads plan in issue; applies `feature-ready-for-ai` label to approve (or edits issue and re-plans if changes needed) | — | GitHub issue labels |
| **5. Implement** | AI Agent | *(Automated)* AI Agent reads issue, creates branch, implements feature, opens PR | `/issue-to-pr` | Copilot Chat prompt (`issue-to-pr.prompt.md`) |
| **6. CI** | Automation | *(Automated)* Tests, lint, and security checks run on the PR | — | GitHub Actions |
| **7\. PR Review** | Developer | 🔵 **Checkpoint B:** Developer reviews PR code and tests; approves or requests changes | — | GitHub PR review |
| **8\. Merge PR** | Developer | 🔵 **Checkpoint C:** Developer confirms CI passes and merges the PR | — | GitHub merge |

## Workflow Diagram

```mermaid
%%{init: {'theme': 'dark', 'primaryColor': '#1e3a8a', 'primaryTextColor': '#ffffff', 'primaryBorderColor': '#3b82f6', 'lineColor': '#8b5cf6', 'secondBkg': '#7c3aed', 'tertiaryBkg': '#fbbf24'}}%%

graph TD
    Z["👤 Developer ready to plan<br/>Types: <b>/plan-feature</b><br/>+ brief feature description"]
    
    Z -->|Copilot loads /plan-feature prompt| A["👤 + 🤖 Developer & AI Agent<br/><b>Prompt: /plan-feature</b>"]
    
    A -->|Cooperative planning| B["📋 Detailed Plan Generated<br/>- Current behavior<br/>- Affected layers<br/>- E2E flow<br/>- Test coverage<br/>- Assumptions"]
    
    B -->|Plan complete| C["📝 GitHub Issue Created<br/>(Plan is the issue body)<br/>🤖 AI applies label:<br/>bug or enhancement"]
    
    C -->|Plan review| D["👤 Developer<br/>🔵 Checkpoint A:<br/>Review Plan in Issue<br/>Apply label: <b>feature-ready-for-ai</b><br/>or request changes"]
    
    D -->|Request changes| E["🔄 Developer + AI Agent<br/>Refine plan"]
    E -->|Updated plan| C
    
    D -->|Applies 'feature-ready-for-ai' label| F["✅ Plan Ready<br/>Automation triggered"]
    
    F -->|Ready to code| G["🤖 AI Agent<br/>Reads Issue (with plan)<br/>& Architecture<br/><b>Prompt: /issue-to-pr</b>"]
    
    G -->|Using prompt| H["📋 issue-to-pr.prompt.md<br/>Implements:<br/>- Write code<br/>- Add tests<br/>- Run CI checks<br/>- Open PR"]
    
    H -->|Tests fail| I["❌ CI Failure<br/>AI fixes & retries"]
    I -->|Re-run| H
    
    H -->|All pass| J["✅ PR Opened<br/>with description"]
    
    J -->|Review needed| K["👤 Developer<br/>🔵 Checkpoint B:<br/>Code Review"]
    
    K -->|Request changes| L["🔄 AI revises code"]
    L -->|Re-test| H
    
    K -->|Approve| M["✅ Code Approved"]
    
    M -->|Ready to merge| N["👤 Developer<br/>🔵 Checkpoint C:<br/>Final Gate"]
    
    N -->|Merge| O["✅ PR Merged"]
    
    style Z fill:#64748b,stroke:#334155,color:#fff
    style A fill:#7c3aed,stroke:#5b21b6,color:#fff
    style B fill:#fbbf24,stroke:#b45309,color:#000
    style C fill:#10b981,stroke:#047857,color:#fff
    style D fill:#1e40af,stroke:#1e3a8a,color:#fff
    style E fill:#f97316,stroke:#92400e,color:#fff
    style F fill:#10b981,stroke:#047857,color:#fff
    style G fill:#7c3aed,stroke:#5b21b6,color:#fff
    style H fill:#fbbf24,stroke:#b45309,color:#000
    style I fill:#dc2626,stroke:#991b1b,color:#fff
    style J fill:#10b981,stroke:#047857,color:#fff
    style K fill:#1e40af,stroke:#1e3a8a,color:#fff
    style L fill:#f97316,stroke:#92400e,color:#fff
    style M fill:#10b981,stroke:#047857,color:#fff
    style N fill:#1e40af,stroke:#1e3a8a,color:#fff
    style O fill:#10b981,stroke:#047857,color:#fff
```
