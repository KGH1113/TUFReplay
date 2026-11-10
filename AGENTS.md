# Before modifying the code...
- **DO NOT MODIFY THE CODE UNTIL I SAYS TO.**
- You are not the only one who modifies the code. Almost every code is modified by me. So please read the code often that I wrote.

# README.md and docs rule
- IF you are new to this project 'cause the session started right before, read README.md to understand this project.
- Keep README.md short. It only says what TUFReplay does, how to install it, how to build and develop it, and links to the kept docs.
- Do not put implementation details, design reasons, measurements, bug stories, or change history in README.md. They belong in code comments, commit messages, and PR descriptions.
- If something in README.md is outdated, fix that part and keep it short.
- docs/ is only for work in progress: investigations, plans, and test logs. When a feature's implementation is finished, delete its docs.
- Only these docs stay, because other mods or ongoing work use them. Update them when their contract changes:
  - `docs/replay-api.md`: the API for other mods (ReplayApi, ReplayRuntime, NativeInputPause).
  - `docs/replay-render-bundle.md`: the render bundle that OrbitRender reads.
  - `docs/i18n-game-terms.md`: the Korean game terms for the UI.

# Useful dev tool usage
- You can use `ilspycmd` and `assetripper(headless)` to inspect ADOFAI's code. Use it unless you really don't need to.
- Use `./scripts/run.sh` for building, packaging, checking, ext. instead of manually executing dotnet commands. Also maintain the scripts.

# UI/UX
- Use shadcn mcp server actively. and use shadcn components.
- Read apple-design skill.
- [Toss tech blog: 좋은 에러 메시지를 만드는 6가지 원칙](https://toss.tech/article/21021) read this blog for better error ux.

# Extre rules
- Always think about the performance and do not make ADOFAI laggy.
- Read this if you are doing something with ADOFAI-IPC: [AdofaiIpc-Documentation](https://github.com/KGH1113/adofai-ipc/tree/main/docs)
- Always think about SOLID principles
