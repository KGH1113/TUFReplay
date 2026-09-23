# 자동 제출 리플레이 전달 계약

2026-09-23부터 iframe 전달 프로토콜은 3이다. 기존 부모 다운로드·ArrayBuffer
전달 방식은 사용하지 않는다. 리플레이 파일의 format_version과 iframe 프로토콜
버전은 서로 별개다.

## 책임

- TUF frontend: 자동 제출 기록에 리플레이 버튼 표시, iframe 생성·닫기·확대,
  runId/passId/levelId 전달. 프리셋 종류·파일 형식·다운로드·재생 설정은 알지 않는다.
- web-adofai: manifest와 현재 레벨 조회, ZIP·증거·프리셋 다운로드, 무결성 검증,
  Worker 압축 해제와 임시 저장소, 로딩·오류·재시도 UI, 재생바·음량·렌더링.
- 서버/CDN: submitted run만 공개하고 정확한 player origin에 CORS 허용.

## iframe 계약

부모는 사용자가 열기를 누르면 즉시 다음 iframe을 만든다.

`/replay/embed?protocolVersion=3&sessionId=<uuid>&parentOrigin=<origin>`

플레이어의 `player.ready`를 받으면 부모는 다음 메시지를 보낸다. 바이너리와
transfer list는 없다.

```ts
{
  source: 'tuf-replay',
  protocolVersion: 3,
  sessionId: '<uuid>',
  type: 'host.open',
  payload: { runId: '<uuid>', passId: 123, levelId: 3072 }
}
```

양쪽 모두 exact origin, source window, sessionId와 protocolVersion을 검사한다.
iframe이 새로 로드되어 ready를 보내면 같은 ID를 다시 전송한다.

- `host.dispose`: 진행 중 요청을 취소하고 재생 자원을 정리한다.
- `player.close`: 부모에게 닫기를 요청한다.
- `player.loaded` / `player.error`: 상태 알림이다. 부모는 세부 오류·설정을 해석하지 않는다.
- 알 수 없는 player 이벤트는 무시한다. 새로운 플레이어 기능 때문에 호스트
  schema를 바꾸지 않도록 이벤트 payload는 호스트 계약에서 열어 둔다.

일반 보기의 부모 컨테이너는 iframe을 열기 전부터 16:9 게임 영역과 컨트롤 공간
82 CSS px를 확보한다. 컨테이너 폭이 480 px 이하이면 두 줄 음량 UI를 위해
104 px를 확보한다. 플레이어도 같은 높이를 사용하며, 로딩 완료나 높이 메시지로
iframe 크기를 바꾸지 않는다. 호스트의 container query와 플레이어의 viewport
media query가 같은 폭을 기준으로 동작한다. 전체 창 보기에서는 가용 높이에 맞춘다.

로딩 오류는 iframe에 원인과 가능한 다음 행동을 표시한다. 재시도는 iframe 내부에서
실행하고, URL·토큰·stack trace는 사용자 메시지에 넣지 않는다. 부모는 iframe 자체의
연결 실패만 처리한다. 음량은 플레이어에 저장하며 pitch·VFX·기록된 시각 프리셋은
기존 고정 presentation mode 2를 유지한다.

## 플레이어 로드 순서와 검증

1. `GET /api/v1/replays/{runId}?format=3`로 manifest를 받는다.
2. run_id, external_pass_id, tuf_level_id를 요청 ID와 대조한다.
3. TUF API `GET /v2/database/levels/{levelId}`로 현재 레벨과 다운로드 URL을 받는다.
4. ZIP 응답을 청크 단위로 Worker에 전달하고, OPFS가 가능하면 임시 파일로 저장한다.
5. manifest에 있는 증거 파일과 visual bundle을 다운로드한다. 파일 경로는 kind별
   closed mapping과 일치해야 하고 임의 manifest URL을 따라가지 않는다.
6. 크기·SHA-256·records·필수 파일을 확인한다. 선호 차트와 최대 63개 대체 후보에서
   기록과 gameplay hash가 일치하는 차트를 찾는다.
7. 음원·그래픽 준비 후 재생 UI를 활성화한다. 자동 재생하지 않는다.

official_file_id와 chart_sha256은 제출 당시의 감사 정보다. 현재 file ID나 전체 차트
해시가 달라도 versioned gameplay hash가 같으면 현재 음원·VFX로 재생할 수 있다.
게임플레이가 바뀌면 `level_gameplay_modified`로 중단한다.

공개 evidence API는 기존 경로 `/api/v1/replays/{runId}/files/{name}`를 유지한다.
허용 파일은 inputs.csv, hits.csv, metadata.json, lifecycle.jsonl, settings.jsonl,
health.jsonl이며 inputs/hits/metadata는 필수다. 서버 검증 내부 자료인 kind 6이나
Storage key·OAuth grant·업로드 토큰은 공개하지 않는다.

Visual은 `/api/v1/replays/{runId}/visuals/{keyviewer|overlay}`에서 받는다. manifest와
bundle 사이에 프리셋이 삭제되어 404가 되면 해당 slot을 비운다. 다른 파일 손상이나
해시 불일치는 조용히 무시하지 않는다.

## 메모리와 취소

- 부모는 파일을 다운로드하거나 보관하지 않는다.
- OPFS 파일은 `adofai-web-editor/replay-sessions`에 두고 정상 종료 시 제거한다.
  일반 레벨 캐시의 orphan cleanup과 분리한다.
- OPFS 경로는 압축 해제 총량 1 GiB, 파일 수 10,000개로 제한한다.
  OPFS/Worker가 불가능한 메모리 대체 경로는 256 MiB로 제한한다.
- 증거 파일은 총 128 MiB, visual bundle은 개별 64 MiB로 제한한다.
- manifest·증거·visual은 no-store로 받아 이전 부모-origin의 immutable 응답에
  남은 CORS 헤더를 재사용하지 않는다. ZIP은 HTTP 캐시를 사용할 수 있다.
- 새 기록·재시도·닫기는 이전 AbortController를 취소한다. 늦게 완료된 이전
  요청은 현재 플레이어를 덮어쓰지 못한다. Blob URL과 Worker를 정리하고
  디코딩이 끝난 원본 음원 버퍼도 해제한다.
- 브라우저 강제 종료는 임시 OPFS 파일을 남길 수 있다. 디코딩 음원·텍스처·
  시뮬레이션 데이터의 재생 메모리는 여전히 필요하며 최대 메모리 수치는 실측하지 않았다.

## 단독 베타 페이지와 실행 설정

`<player-origin>/replay?runId=<uuid>`로 같은 리플레이를 단독 실행한다.
선택적으로 passId, levelId query를 넣어 추가 대조할 수 있다. 베타 배포 주소를
별도로 사용하면 TUF frontend 재배포 없이 플레이어 패치를 확인할 수 있다.

TUF frontend에는 `VITE_WEB_ADOFAI_URL`만 필요하다. API 주소는 플레이어 빌드의
`VITE_AUTO_SUBMISSION_API_URL`, `VITE_TUF_API_URL`로 설정한다.
`VITE_TUF_PARENT_ORIGINS`는 허용하는 부모 origin의 쉼표 구분 목록이다.

서버 production CORS에는 `REPLAY_PLAYER_ORIGIN`, `REPLAY_BETA_ORIGIN`을 사용한다.
TUF backend는 기존 production player origin과 loopback을 허용한다. 새 베타 origin은
backend의 CORS_EXTRA_ORIGINS 및 CDN/스토리지에도 허용해야 한다.
브라우저 다운로드는 credentials: omit이며 부모의 로그인 토큰을 전달하지 않는다.

`bun run e2e:live`는 sibling `adofai-web-editor` 소스에서 Vite dev server를 실행한다.
최신 source 변경은 HMR로 반영되며 환경변수 변경 후에는 사용자가 스택을 다시 실행한다.
로컬 CDN relay는 player의 5190 origin으로 이동했으며 운영 프록시가 아니다.
