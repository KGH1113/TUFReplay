# 자동 제출 리플레이 전달 계약

manifest 계약 버전: 2. iframe 메시지 protocol은 1이다. 이 문서는 자동 제출 서버, TUF 프론트엔드, replay 전용 web-adofai iframe의 경계를 정의한다. web-adofai와 TUF 프론트엔드의 구현·실행법·검증 현황은 [구현 보고](tuf-replay-embed-implementation-2026-09-10.md)를 참조한다.

## 책임

- 자동 제출 서버는 제출 완료된 run의 리플레이 증거만 공개한다.
- TUF 프론트엔드는 pass의 `autoSubmissionRunId`로 증거를 받고, TUF API에서 공식 레벨 archive를 받는다. 모든 hash와 pass 연결을 확인한 뒤 사용자가 로드를 선택했을 때만 iframe을 만든다.
- web-adofai iframe은 외부 URL을 직접 가져오지 않는다. 부모가 전달한 레벨 archive와 리플레이 증거만 파싱·재생하고, 조작 UI는 최소화한다.
- 차트, 음원, VFX 자산은 자동 제출 서버가 중복 저장하거나 제공하지 않는다. TUF 레벨 archive에 포함된 자료를 사용한다.

## 메인 구현에 넘길 요구사항

- replay iframe route는 작은 embed viewport에 맞춘 canvas와 게임 HUD만 둔다. 로컬 파일 picker, 에디터 panel, 자체 transport와 일반 web-adofai 설정 UI는 표시하지 않는다. 재생·timeline·pitch·음량·VFX·track·icon·판정선 설정은 부모 TUF UI가 담당한다.
- iframe 로드 자체가 무거우므로 pass 페이지 진입만으로 iframe, WebGL renderer, archive download를 만들지 않는다. 사용자의 명시적인 로드 동작이 시작점이다.
- 판정선은 임의 CSS 막대가 아니라 실제 ADOFAI 자산 좌표, CanvasScaler, tick 수명과 InQuad fade, 평균 바늘의 250ms OutCubic 이동을 재현한다. 자세한 실측값은 [web-adofai 리플레이 조사](./web-adofai-replay-investigation-2026-09-10.md)에 있다.
- 기존 creplay 전용 파서·타입·파일 선택 흐름은 유지하지 않고 TUFReplay evidence 전용 모델로 전환한다. `hits.csv`의 실제 `timeUs`와 확정 판정을 사용한다.
- TUF FE와 web-adofai 양쪽 구현은 아래 API·메시지 이름과 단위(µs, percent)를 그대로 공유하는 타입 패키지 또는 복제 검증 schema를 둔다. 구현 편의로 필드 이름이나 시간 단위를 한쪽에서만 바꾸지 않는다.

## 자동 제출 서버 API

### Manifest

`GET /api/v1/replays/{run_id}`는 인증 없는 공개 API다. `run_submission_records.state='submitted'`이고 manifest, validation, TUF pass ID가 모두 존재하는 run만 `200`으로 반환한다. 나머지는 존재 여부를 구분하지 않고 `404`로 반환한다.

응답 예시:

```json
{
  "format_version": 2,
  "run_id": "092eb0fa-a469-4a1a-8201-9a58fe2c113f",
  "tuf_level_id": 3072,
  "chart_path": "level.adofai",
  "external_pass_id": 123,
  "official_file_id": "tuf-file-id-at-validation",
  "chart_sha256": "64 lowercase hex characters",
  "gameplay_hash_version": 1,
  "gameplay_hash": "64 lowercase hex characters",
  "evidence_digest": "64 lowercase hex characters",
  "recorded_speed": 1.0,
  "files": [
    {
      "name": "inputs.csv",
      "kind": "inputs",
      "media_type": "text/csv; charset=utf-8",
      "url": "/api/v1/replays/{run_id}/files/inputs.csv",
      "sha256": "64 lowercase hex characters",
      "bytes": 1234,
      "records": 200
    }
  ]
}
```

파일 목록은 실제 저장된 stream만 포함하며 순서는 `inputs`, `hits`, `metadata`, `lifecycle`, `settings`, `health`다. `inputs.csv`, `hits.csv`, `metadata.json`은 필수다. 서버 수신 순서인 kind 6은 검증 내부 자료이며 공개하지 않는다. manifest에는 Storage key, owner, OAuth grant, 업로드 token을 넣지 않는다.

Manifest와 파일 응답은 `Cache-Control: public, max-age=31536000, immutable`이다. 제출된 증거와 run UUID는 불변이라는 계약이다.

### 파일

`GET /api/v1/replays/{run_id}/files/{name}`은 manifest에 포함된 파일을 Storage에서 스트리밍한다. 허용 이름은 다음뿐이다.

| name | kind | 내용 |
|---|---|---|
| `inputs.csv` | inputs | `timeUs,key,flags,nativeCode,nativeFlags` |
| `hits.csv` | hits | 13열 hit context와 마지막 `timeUs` |
| `metadata.json` | metadata | 완료 시점 run 메타데이터 |
| `lifecycle.jsonl` | lifecycle | 플레이 상태 전이 |
| `settings.jsonl` | settings | 재생에 영향을 주는 런타임 설정 |
| `health.jsonl` | health | recorder 상태와 누락 계수 |

응답에는 정확한 `Content-Type`, `Content-Length`, attachment `Content-Disposition`, SHA-256을 값으로 쓰는 `ETag`가 있다. 클라이언트는 HTTP 성공만 신뢰하지 않고 manifest의 byte length와 SHA-256을 직접 확인한다. 파일 이름은 closed mapping이라 임의 Storage path로 사용할 수 없다.

브라우저 호출은 `credentials: 'omit'`을 사용한다. 운영 자동 제출 서버는 CORS의 `TUF_WEB_ORIGIN`을 정확한 TUF 프론트엔드 origin으로 설정한다. wildcard origin은 사용하지 않는다.

## TUF 프론트엔드의 로드 순서

1. pass가 `submissionSource === 'auto_submission'`이고 `autoSubmissionRunId`가 있을 때만 replay 로드 버튼을 활성화한다.
2. 사용자가 버튼을 누르면 자동 제출 API manifest와 TUF API의 현재 레벨 metadata/archive를 받는다.
3. manifest의 `run_id`가 pass의 run ID, `external_pass_id`가 현재 pass ID, `tuf_level_id`가 pass의 level ID인지 확인한다.
4. 현재 TUF archive에서 `chart_path`를 먼저 찾는다. 이름이 바뀌었으면 TUF의 기존 ZIP 선택 규칙과 같이 가장 큰 `.adofai`를 우선 후보로 삼고 현재 bytes의 SHA-256을 계산한다. iframe은 이 SHA-256으로 부모가 전달한 archive가 전송 중 바뀌지 않았는지 확인한다.
5. replay 파일을 병렬 다운로드하고 각각 `bytes`와 `sha256`을 확인한다. 필수 세 파일이 없으면 로드를 중단한다.
6. archive 검증이 끝난 뒤 replay 전용 iframe을 생성한다. iframe의 `ready`를 받은 뒤 archive와 replay buffers를 한 번에 전달한다. iframe은 우선 후보와 최대 63개의 다른 `.adofai` 후보에서 의미 해시가 manifest의 `gameplay_hash`와 같은 차트를 찾는다.
7. iframe의 `loaded`를 받은 뒤에만 play, timeline과 설정 조작을 활성화한다.

`official_file_id`와 `chart_sha256`은 제출 검증 당시 공식 파일을 식별하는 감사 정보다. 현재 file ID나 전체 차트 SHA-256이 달라도 `gameplay_hash_version`이 지원되고 현재 차트의 의미 해시가 같으면 현재 archive의 음원·VFX로 재생한다. BPM, 타일 경로, Hold·Pause·Twirl·SetSpeed 등 플레이에 영향을 주는 값이 달라 의미 해시가 다르면 `level_gameplay_modified`로 중단한다. 의미 해시는 VFX 이벤트와 배경·카메라 표현을 제외하고, 각도 표현과 동일한 절대 BPM 표현을 정규화한다.

취소·pass 이동·오류가 발생하면 진행 중 fetch를 `AbortController`로 취소하고 iframe에 `dispose`를 보낸 뒤 제거한다. transfer가 끝난 ArrayBuffer는 부모에서 detached되므로 동일 세션을 다시 초기화하려면 새로 다운로드한다.

## iframe 메시지 계약

iframe URL은 replay 전용 route이며 다음 값을 query로 받는다.

```text
/replay/embed?protocolVersion=1&sessionId={uuid}&parentOrigin={percent-encoded-origin}
```

web-adofai는 허용된 parent origin인지 확인한 뒤에만 메시지를 받는다. 양쪽은 모든 메시지에서 다음 envelope를 사용한다.

```ts
type ReplayEnvelope<TType extends string, TPayload> = {
  source: 'tuf-replay';
  protocolVersion: 1;
  sessionId: string;
  type: TType;
  payload: TPayload;
};
```

부모는 `event.origin === WEB_ADOFAI_ORIGIN`과 `event.source === iframe.contentWindow`를 확인한다. iframe도 `event.origin === parentOrigin`과 `event.source === window.parent`를 확인한다. `postMessage`의 `targetOrigin`에 `*`를 쓰지 않는다. schema가 틀리거나 다른 session의 메시지는 무시한다.

### 초기화

iframe은 엔진이나 레벨을 로드하기 전에 다음 ready를 보낸다.

```ts
type PlayerReady = ReplayEnvelope<'player.ready', {
  capabilities: {
    pitch: true;
    volumes: true;
    vfx: true;
    trackAppearance: true;
    icons: true;
    hitErrorMeter: true;
  };
}>;
```

부모는 ready를 받은 뒤 transferable ArrayBuffer와 함께 한 번만 init을 보낸다.

```ts
type HostInit = ReplayEnvelope<'host.init', {
  level: {
    levelId: number;
    fileId: string;
    chartPath: string;
    chartSha256: string; // 현재 TUF archive 안 chart bytes의 SHA-256
    archive: ArrayBuffer;
  };
  replay: {
    manifest: ReplayManifestResponse;
    files: Array<{
      kind: 'inputs' | 'hits' | 'metadata' | 'lifecycle' | 'settings' | 'health';
      name: string;
      sha256: string;
      data: ArrayBuffer;
    }>;
  };
  initialSettings: ReplaySettings;
}>;
```

`archive`와 각 `data`를 `postMessage`의 transfer list에 넣는다. iframe은 부모가 검증했더라도 현재 후보 chart hash, versioned gameplay hash, replay file hash와 필수 파일을 다시 확인한다. 제출 당시 file ID와 전체 chart hash는 감사 정보이며 현재 archive의 호환성 조건으로 쓰지 않는다. init은 자동 재생하지 않는다.

성공하면 `player.loaded`를 보낸다.

```ts
type PlayerLoaded = ReplayEnvelope<'player.loaded', {
  durationUs: number;
  positionUs: number;
  paused: true;
  appliedSettings: ReplaySettings;
}>;
```

### 조작

부모가 보내는 모든 조작에는 session 안에서 단조 증가하는 `commandId`가 있다.

```ts
type HostCommand = ReplayEnvelope<'host.command',
  | { commandId: number; command: 'play' }
  | { commandId: number; command: 'pause' }
  | { commandId: number; command: 'restart' }
  | { commandId: number; command: 'seek'; positionUs: number }
  | { commandId: number; command: 'setSettings'; settings: Partial<ReplaySettings> }
  | { commandId: number; command: 'dispose' }
>;

type ReplaySettings = {
  pitchPercent: number;                 // 정수 1..1000
  songVolumePercent: number;            // 정수 0..100
  hitSoundVolumePercent: number;        // 정수 0..100
  vfxEnabled: boolean;
  forceDefaultTrackAppearance: boolean;
  showAllIcons: boolean;
  hitErrorMeterVisible: boolean;
  hitErrorMeterSize: 'small' | 'normal' | 'large' | 'extra_large';
  hitErrorMeterShape: 'straight' | 'curved';
};
```

pitch는 관람 속도와 음정에 적용하지만 기록된 판정·오차·판정선 위치를 다시 계산하지 않는다. seek는 정수 µs이고 `[0,durationUs]`로 clamp한다. 설정 창은 TUF 쪽 overlay이므로 열고 닫아도 iframe 크기를 바꾸지 않는다.

iframe은 적용 결과를 다음 상태로 알린다. 재생 중에는 최대 4Hz로 보내며 play/pause/seek/settings/ended/error에는 즉시 보낸다.

```ts
type PlayerState = ReplayEnvelope<'player.state', {
  appliedCommandId: number;
  positionUs: number;
  durationUs: number;
  paused: boolean;
  ended: boolean;
  settings: ReplaySettings;
}>;
```

부모는 오래된 `appliedCommandId` 상태가 최신 명령의 UI를 되돌리지 않게 한다. timeline의 부드러운 화면 갱신은 마지막 state와 monotonic clock으로 보간하되 seek 기준값은 iframe state를 따른다.

### 오류

```ts
type PlayerError = ReplayEnvelope<'player.error', {
  stage: 'protocol' | 'level' | 'replay' | 'audio' | 'renderer';
  code: string;
  recoverable: boolean;
}>;
```

오류 메시지에는 URL, Storage key, token, stack trace를 넣지 않는다. 부모가 처리할 기본 code는 `unsupported_protocol`, `invalid_message`, `level_revision_unavailable`, `level_archive_invalid`, `chart_not_found`, `chart_hash_mismatch`, `level_gameplay_modified`, `replay_file_missing`, `replay_hash_mismatch`, `replay_parse_failed`, `audio_decode_failed`, `renderer_failed`다.

## 구현 완료 조건

- iframe은 사용자가 로드를 누르기 전 생성되지 않고 레벨/replay fetch도 시작하지 않는다.
- 자동 제출 API는 submitted run만 공개하고 경로 조작, kind 6 노출, Storage key 노출을 막는다.
- 부모와 iframe 양쪽이 origin/source/session/schema/hash를 검사한다.
- loading 취소, 빠른 pass 이동, init 중 dispose, play/pause, 앞뒤 seek, pitch·음량·VFX·track·icon·판정선 설정을 검사한다.
- 같은 pass의 manifest와 파일은 immutable cache에서 다시 사용할 수 있으며 다른 pass/run과 섞이지 않는다.
