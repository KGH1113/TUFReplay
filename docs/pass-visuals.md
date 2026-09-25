# Pass visual settings

Implemented across TUFReplay, web-adofai, the TUF backend and the TUF frontend on `codex/pass-visuals`.

## User workflows

### 시청자: 키뷰어·오버레이 바꾸기

1. TUF 기록 페이지에서 리플레이를 연다.
2. 플레이어의 타임라인 줄에 있는 **Visuals**를 누른다.
3. **Keyviewer**와 **Overlay**를 각각 선택한다. 제출자의 현재 공개 프리셋과 **None**을 선택할 수 있다.
4. 재생 위치와 재생/일시정지 상태를 유지하면서 비주얼이 바뀐다. 맵·음악·입력 기록은 다시 다운로드하지 않는다.
5. **Use pass defaults**는 제출자가 저장한 현재 기본값으로 돌아간다. **Refresh list**는 목록을 다시 불러온다.

이 선택은 열린 플레이어 세션에만 적용된다. 다른 시청자나 제출자가 저장한 기본값을 바꾸지 않는다. 플레이어를 닫고 다시 열면 패스 기본값으로 시작한다.

### 제출자: 이미 제출한 기록의 기본값 바꾸기

1. TUF 웹사이트에 제출자 계정으로 로그인하고 본인의 기록 페이지를 연다.
2. 리플레이 제목 옆의 **리플레이 비주얼 설정**을 누른다.
3. 기본 키뷰어와 오버레이를 독립적으로 선택한다. **사용 안 함**도 가능하다.
4. **이 기록의 기본값 저장**을 누른다.

저장한 기본값은 이후 리플레이를 여는 시청자에게 적용된다. 같은 페이지에 열린 플레이어도 기본값을 다시 적용한다. 다른 시청자가 이미 선택한 비주얼은 기본값 수정만으로 덮어쓰지 않는다. 제출 후 기본값 편집은 TUF 웹사이트가 담당하며 게임 모드나 companion에는 편집 UI를 추가하지 않았다.

### 제출자: 프리셋 숨김·복원

1. 같은 설정창의 **내 프리셋 목록**에서 **숨기기**를 누른다.
2. 해당 프리셋은 새 제출 선택 목록과 시청자 선택 목록에서 빠진다.
3. 해당 프리셋을 기본값으로 쓰던 본인의 모든 공개 기록은 해당 종류만 **사용 안 함**으로 바뀐다. 나머지 종류는 유지된다.
4. **공개**를 누르면 목록에 다시 나타난다. 이전 기본값은 자동 복원되지 않으므로 필요한 기록에서 다시 저장한다.

숨김은 프리셋 단위의 계정 전체 설정이다. 현재 페이지에는 즉시 목록 갱신 메시지를 보내고, 다른 플레이어는 30초 간격으로 목록을 갱신하여 숨겨진 현재 선택을 해제한다. 네트워크 연결이 끊긴 플레이어는 다음 성공적인 목록 갱신 때 반영한다. 이미 발급한 CDN URL은 기존 15분 만료까지 유효하고 다운로드된 바이트를 회수하지는 않는다. 숨김은 새로운 선택·권한 발급을 막는 동작이다.

## Architecture and contract

- `visual_presets.hidden_at` is nullable and defaults to visible. Registration, names, bundles and R2 objects are unchanged.
- Owner library endpoints used by new submissions omit hidden presets. Submission validation rejects hidden IDs; existing retry selection remains fixed.
- Pass defaults live in `run_visual_selections`. Only the authenticated TUF internal service can edit published defaults. Recorded manifests, validation results, inputs and hits are unchanged.
- A defaults save verifies the run ID, external pass ID, submitter account, preset owner, kind, visibility and deletion state. It locks selected presets before saving. Hiding and clearing published defaults happen in one transaction.
- The TUF backend checks its authenticated web session against the pass’s `playerId`, derives account/run/pass IDs server-side, and forwards the request using `TUF_TO_AUTO_SUBMISSION_TOKEN`. Request bodies cannot override ownership.
- `GET /api/v1/replays/{run}/visual-options` returns visible metadata and effective defaults. Hidden/deleted/invalid defaults appear as null.
- The existing v3 manifest accepts `keyviewer_id` and `overlay_id`: omitted means default, `none` means off, UUID means a visible preset owned by this submitter. Invalid UUID syntax returns 400; unavailable/foreign/wrong-kind presets become empty slots.
- Explicit bundle/asset API routes use `preset_id`. All selections retain the existing publication/evidence checks. Signed CDN grants cover only the resolved selected bundles and assets.
- `GET/PUT /v2/database/passes/{pass}/replay-visuals` and `PUT /v2/database/passes/{pass}/replay-visuals/{preset}/visibility` are TUF web-session endpoints. TUF forwards to `/internal/tuf/replays/{run}/visuals` and its visibility subroute.
- `host.visualsChanged` extends the existing v3 trusted-parent message envelope with `{useDefaults:boolean}`. Session ID, sender window and origin checks still apply.
- The player prepares new visual resources separately, then swaps the renderer after cancellation checks. The old renderer and Blob URLs are released. Visual download errors leave the replay running and expose a separate visual error. Closing or replacing a replay cancels pending work.
- The Visuals dialog opens from the existing transport row. Toolbar and iframe sizing remain unchanged.

## Rollout

1. Deploy the replay server and run its normal migrations (adds `hidden_at`). Existing presets remain visible and old clients continue using pass defaults.
2. Deploy TUF backend and web-adofai. Existing internal credential configuration is reused; no new secret or CDN Worker change is needed.
3. Deploy TUF frontend to expose the owner settings button and same-page refresh messages.

The implementation is prepared in isolated worktrees to preserve concurrent uncommitted work in the normal source directories:

| Repository | Worktree |
| --- | --- |
| TUFReplay | `/private/tmp/tuf-pass-visuals` |
| web-adofai | `/private/tmp/web-adofai-pass-visuals` |
| TUF backend | `/private/tmp/tuf-backend-pass-visuals` |
| TUF frontend | `/private/tmp/tuf-frontend-pass-visuals` |

No production deployment, remote push, or PR merge is included in this implementation. Real game playback and visual inspection remain with the user as requested.

## Automated verification

Result: replay-server library tests **62 passed**; request integration tests **18 passed, 1 fixture-dependent test ignored**; player replay/session/download tests **79 passed**; TUF backend auto-submission tests **21 passed**; TUF frontend default-selection test **1 passed**. TypeScript checks and both frontend production compilations passed. Targeted lint passed; the existing renderer `tint!` assertion reports a pre-existing Biome warning. Production compilation also retains the existing bundle-size warnings. TUF frontend compilation used no production API environment and was a build check, not a deployed-site test.

- Replay server: `./scripts/run.sh server-check`; request integration tests through `./scripts/run.sh server-check --integration requests:: -- --test-threads=1` on disposable PostgreSQL/Redis. Covers public vs unfinished replays, owner/kind/visibility rejection, unchanged evidence, temporary viewer selection, CDN grants, clearing defaults across passes, no implicit restoration, and existing submission retry behavior.
- Player: TypeScript, targeted Biome checks, production build, and replay/download/session tests including CDN visual replacement, no evidence reload, local hidden fallback, URL validation and cancellation.
- TUF backend: locked dependencies, TypeScript build, security ESLint and auto-submission service tests, including ownership, server-derived identity and internal request/error contract.
- TUF frontend: targeted ESLint, production compilation, and default sanitization tests. English and Korean strings added; other locales use the existing fallback.
- The existing real importer/rendered-pixel integration case requires separate source fixtures and remains ignored. No interactive browser or game verification was performed.
