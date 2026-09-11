# TUFReplay 웹 재생 구현 — 2026-09-10

TUF 프론트엔드와 web-adofai에 실제 리플레이 전달·재생 경로를 구현했다. 기존 Creplay 파서와 파일 선택 화면을 제거하고 TUFReplay 증거를 읽도록 바꿨다. **실제 화면을 조작하는 E2E는 사용자 요청에 따라 실행하지 않았다.** 아래 코드·데이터 검증과 실제 화면 검증을 구분한다.

## 구현 내용

- Load 클릭 후에만 증거·차트를 가져오고, 무결성 검사가 끝나면 iframe을 생성한다. 초기화 완료 전에는 재생·설정 버튼을 비활성화한다.
- pass/run/level 연결, 검증 당시 감사용 file ID·chart SHA, versioned gameplay hash, 증거 SHA·바이트 수를 검사한다. iframe은 증거 레코드 수도 검사한다. 현재 차트의 의미 gameplay hash가 다르면 `level_gameplay_modified`로 종료한다. file ID·파일명·VFX·전체 파일 SHA만 바뀐 경우에는 현재 archive로 재생한다.
- `origin`, source window, protocol, session ID와 메시지 schema를 검사한다. 전역 `*` 대상 postMessage는 사용하지 않는다.
- 부모의 재생·일시정지·재시작·탐색·Pitch·두 음량·VFX·기본 트랙·아이콘·판정선 설정을 iframe 엔진으로 전달한다. 재생 상태는 250 ms 간격으로 보고하고 부모가 사이 위치를 보간한다.
- 설정은 영상 영역 위에 겹치며, 몰입 화면 전환 시 iframe을 재생성하지 않는다. 닫기·pass 이동·초기화 도중 취소에서 fetch와 엔진 수명을 정리한다.
- 기록된 hit timestamp와 확정 판정을 직접 사용한다. timestamp가 없는 기록의 각도로 시간을 추정하는 fallback은 없다. 음수 native input 시각과 macOS/Windows 키 공간을 보존한다.
- 음악은 오디오 시계로 진행하고, 차트의 countdown·음악 offset 및 원래 곡 음량을 반영한다. 관람 Pitch가 원래 기록의 판정·오차를 바꾸지 않는다.
- 원본 `scrHitErrorMeter`·관련 판정 코드와 AssetRipper 프리팹 조사값을 적용했다. 실제 Straight/Curved/Tick/Hand PNG를 추출했고 2560×1440 기준 배치, 크기 단계, 최대 60개 tick, 3초 quadratic fade, 250 ms OutCubic 평균 바늘을 구현했다.

서버 API 계약은 [replay-delivery-contract.md](replay-delivery-contract.md)를 재사용한다. 이번 작업에서 TUF 백엔드 코드는 수정하지 않았다. 자동 제출 서버는 브라우저 테스트에 필요한 E2E CORS 설정만 보완했다.

## 실행

별도 터미널에서 실행한다. 원래 떠 있는 다른 web-adofai 서버는 종료하지 않았다.

```sh
cd ~/dev/src/adofai-web-editor
VITE_TUF_PARENT_ORIGINS=http://127.0.0.1:5176 bun run dev --host 127.0.0.1 --port 5177 --strictPort
```

```sh
cd ~/dev/src/t21c-web-frontend
VITE_AUTO_SUBMISSION_API_URL=http://127.0.0.1:5151 VITE_WEB_ADOFAI_URL=http://127.0.0.1:5177 bun run dev -- --host 127.0.0.1 --port 5176 --strictPort
```

TUF API·자동 제출 서버의 실행과 로컬 제출 데이터 준비는 [E2E README](../tools/auto-submission-e2e/README.md)를 따른다. 기존 E2E 프로세스를 종료한 뒤 최신 코드로 다시 시작해야 새 공개 replay API와 CORS가 반영된다. Vite 환경변수는 빌드 시 값이 고정되므로 운영 빌드에서도 별도 설정한다.

2026-09-10 재검증에서 fixture와 현재 TUF level 3072의 의미 gameplay hash v1이 모두 `a7583eb3b1b8cda23a6a529ee96837859d44c5346dc29b4c6709f90928fc0396`임을 확인했다. manifest는 gameplay hash를 포함하는 v2로 올렸고, 기존 v1 immutable 브라우저 캐시와 분리하려고 프론트엔드는 `?format=2`로 요청한다. 로컬 E2E 레코드는 실제 해시 일치가 확인된 14건만 보완했으며 업데이트 전 JSON은 `/tmp/tuf-replay-validation-before-gameplay-hash.jsonl`에 백업했다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| web-adofai 전체 Bun 테스트 | 877 통과, 기존 fixture 1개 skip, 실패 0 |
| TUFReplay 추가 테스트 | CSV 시각/키, 확정 판정, 실패→성공 전이, midspin 동시 시각, 음악 시간 변환, 판정선, 메시지 schema, 패키지 무결성 등 14개 통과 |
| web-adofai 타입 검사·빌드 | 통과 |
| 수정한 web-adofai 파일 Biome | 통과 |
| 저장소 전체 Biome | 미수정 `vite.config.ts`의 기존 포맷 차이 1건 |
| TUF 프론트엔드 테스트 | 메시지 schema와 Pitch 시간 보간 2개 통과 |
| TUF 프론트엔드 lint·Vite build | 통과. 임시 출력 `/tmp/tuf-replay-frontend-build` 사용 |
| 브라우저·게임 화면 E2E | 미실행 — 사용자가 수행 |

빌드의 기존 large chunk, static/dynamic import 혼용, 프론트엔드 `file-type` eval·Sentry 토큰 미지정 경고는 남아 있다. Sentry 업로드나 배포·push는 하지 않았다.

실제 성공 fixture `092eb0faa4694a1a82019a58fe2c113f`의 첫 hit는 233,580 µs, 기록 원점은 0.7027529014379621초다. 대응 차트 시각은 936,333 µs이며 countdown과 음악 offset을 적용한 음악 위치는 약 0.053256초다. 단순히 기록 시각이나 차트 시각을 음악 위치에 넣지 않도록 회귀 테스트했다.

## 사용자가 확인할 화면 항목과 한계

1. 제출된 pass에서 Load 전에는 iframe·레벨 ZIP·증거 요청이 없고, Load 완료 후 정지 상태로 나타나는지.
2. 실제 음원과 행성·키·판정선 동기화, 50/100/150% Pitch, pause/seek/restart, 곡 끝 정지.
3. VFX·트랙·아이콘, 판정선 직선/곡선과 네 크기, 음량 조절.
4. 몰입 화면·설정 패널에서 화면 크기와 재생이 유지되는지. Escape는 설정을 먼저 닫고 그다음 몰입 화면을 닫는다.
5. 로드 중 닫기·pass 이동·실패 후 재로드에서 이전 화면·소리가 남지 않는지.

원본 텍스처와 계산값을 적용했지만 실제 플레이 화면과 픽셀·음향까지 완전히 동일하다고 검증한 상태는 아니다. 원본 난수 상태가 없는 오버플로 tick 위치는 결정적으로 재구성한다. 난이도는 최초 settings 값을 사용하며 플레이 도중 난이도 변경의 동적 재현은 포함하지 않는다. 복잡한 hold/free-roam 사례도 실제 기록으로 추가 확인해야 한다. iframe ZIP 상한은 10,000개·압축 해제 합계 1 GiB이며 제출 허용 정책과 별개의 관람기 제한이다.

핵심 파일은 web-adofai의 `src/replay/`, `src/editor-engine/replay-player.controller.ts`, `src/web/replay-viewer/replay-{embed,package}.controller.ts`와 TUF 프론트엔드의 `PassDetailPage/replay/`다. 세부 실행 설명은 web-adofai `docs/tuf-replay-embed.md`, 원본 조사 근거는 [조사 문서](web-adofai-replay-investigation-2026-09-10.md)에 있다.
