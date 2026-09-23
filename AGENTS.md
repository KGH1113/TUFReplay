# TUFReplay Agent Instructions

These instructions apply to work performed in this repository. User requests take
precedence over these instructions when they conflict.

## Before changing code

- Do not modify source code until the user explicitly asks for a code change.
- The worktree is shared with the user and other contributors. Inspect the relevant
  files often and preserve unrelated existing changes.
- Before implementing a feature in a new session, read `README.md` to understand the
  repository and its current architecture.
- If `README.md` is out of date, update it when the requested work changes the documented
  behavior or workflow.
- Follow SOLID principles and keep feature boundaries clear.

## Repository workflow

- Use `./scripts/run.sh` as the entry point for builds, packaging, checks, and related
  workflows. Keep the scripts up to date when changing their behavior.
- Use `ilspycmd` or `assetripper(headless)` to inspect ADOFAI code and assets when the
  implementation depends on game behavior. Do not guess at runtime contracts that can be
  inspected.
- Use the repository's existing test and validation commands after changes. Match the
  scope of verification to the affected component and report any checks that could not run.

## ADOFAI and IPC

- Read the [AdofaiIpc documentation](https://github.com/KGH1113/adofai-ipc/tree/main/docs)
  before changing ADOFAI-IPC integration.
- Keep game-thread work and recording/replay work efficient. Consider allocations,
  blocking I/O, frame-time impact, and large-file behavior before implementing a change.

## UI and UX

- Use the project's shadcn components and shadcn MCP tooling for web UI work.
- Read and apply the `apple-design` skill for interaction, motion, accessibility, and
  visual hierarchy decisions.
- Use the principles in [Toss's error-message guide](https://toss.tech/article/21021):
  explain the situation, give the reason when useful, and tell the user what they can do
  next. Prefer clear, user-facing language over implementation or error-code jargon.

## Orchestration and delegation

- For complex work, use the `astra-orchestrator` skill when its trigger conditions match.
- If the user explicitly mentions `astra-orchestrator` or `$astra-orchestrator`, apply the
  skill for that request even when the normal complexity trigger would not apply. Read the
  skill instructions before taking task actions and follow its required delegation workflow.
- The root agent owns architecture, decomposition, integration, and final verification.
- Delegate bounded exploration, implementation, testing, review, or research tasks when
  doing so materially improves the result. Do not delegate trivial work merely to create
  parallel activity.
- Give each implementation agent clear file or subsystem ownership. Do not let multiple
  implementation agents edit the same files without explicit coordination.
- Before finishing, inspect the final diff, integrate material findings, and verify the
  requested behavior.
