# 레벨 5510·3132 제출·미리보기 실패 조사와 수정

후속 수정 완료: 사용자 승인 후 서버·웹 차트 파서, 검증 오류 분류, 메타데이터 gateway 라우팅을 수정하고 로컬 검증했다. 아래 원인 조사 내용은 수정 전 운영 상태를 기록한 것이다. 운영 배포와 기존 제출 재시도는 수행하지 않았다. 마지막 절에 수정·검증 결과를 정리했다.

조사일: 2026-09-25. 운영 서버와 DB는 읽기 전용으로 조사했다. 설정 변경, 데이터 수정, 재제출, 재배포는 하지 않았다. 인증 정보와 증거 원문은 보고서에 포함하지 않았다.

## 결론

**Neon Paradise의 제출 실패와 차트 미리보기 실패는 같은 맵 문법 호환성 문제다.** 공식 ZIP의 `.adofai`에는 객체 속성 사이에 쉼표가 하나 더 있다. 실제 게임의 `GDMiniJSON`은 이를 허용하지만, 웹과 서버의 JSON5 파서는 거부한다. 업로드 실패나 R2 파일 유실은 확인되지 않았다.

**메타데이터 요청 404는 별개의 Nginx 라우팅 문제다.** `/api/tuf/*`가 웹의 TUF 프록시로 가야 하는데 운영 gateway의 `/api/` 규칙이 자체 Rust API 서버로 보내고 있다. 이것이 JSON 파싱 오류를 발생시킨 것은 아니다.

| 증상 | 확인된 원인 | 확인 방법 |
|---|---|---|
| 제출 대기 후 실패 | 공식 맵의 중복 쉼표 → Rust JSON5 파싱 실패 → 공식 차트 획득 실패 → 재시도 소진 | 운영 DB·작업 큐, 공식 ZIP, 배포와 동일한 해시 코드 재현 |
| 차트 미리보기 실패 | 게임이 허용하는 중복 쉼표를 웹 JSON5 파서가 거부 | HAR의 실제 LevelText, 웹 파서 재현, 운영 번들, 게임 디컴파일 |
| `/api/tuf/.../5510` 404 | gateway의 광범위한 `/api/` 라우팅이 웹 프록시를 가로챔 | 운영 Nginx 설정, 공개 주소와 컨테이너 내부 응답 비교 |

## 1. 제출은 어디까지 진행됐나

대상 실행은 `9dbfe46f-89c3-4808-998e-6444a5a3cb6c`이다.

운영 DB 조회 결과:

| 항목 | 값 |
|---|---|
| TUF 레벨 | 5510 |
| 공식 파일 ID | `f9cd92a3-e69f-42ae-a159-b8144c8f9f57` |
| 게임 / 모드 | `3.4.0` / `0.2.0-auto-submission.3` |
| 실행 생성 | 2026-09-24 11:53:57.853653 UTC |
| 전송 봉인 완료 | 11:56:03.271231 UTC |
| 저장 후 ingest 해제 | 11:56:16.205828 UTC |
| 최초 제출 요청 기록 | 11:56:30.095062 UTC |
| 현재 실행 상태 | `sealed` |
| 현재 제출 상태 | `validation_error` |
| 사용자용 reason | `submission_temporarily_unavailable` |
| 현재 retry_count | 4 |
| manifest / 검증 산출물 | 있음 / 없음 |
| TUF pass ID | 없음 |

R2에는 manifest가 가리키는 **7개 증거 객체가 모두 존재**했다. HEAD로 확인한 객체 크기도 DB 값과 모두 일치했다.

| stream kind | DB 크기 | R2 크기 |
|---|---:|---:|
| 0 | 18,919 | 18,919 |
| 1 | 39,704 | 39,704 |
| 2 | 1,888 | 1,888 |
| 3 | 407 | 407 |
| 4 | 140 | 140 |
| 5 | 144 | 144 |
| 6 | 17,754 | 17,754 |

증거 내용은 다운로드하지 않았다. 따라서 위 확인은 객체 존재·크기 확인이며, 이번 조사에서 증거 전체의 내용 해시를 재검증했다는 뜻은 아니다.

컨테이너 stdout의 해당 시간 구간에는 스케줄러 INFO만 있었지만, PostgreSQL 작업 큐 `pg_loco_queue`의 `task_data.error`에는 실제 작업 오류가 보존돼 있었다. 이 실행의 실패한 Submission 작업 8개 모두 **`official_chart_unavailable`**이었다. 첫 실패 작업 생성은 11:56:30 UTC, 마지막 실패 갱신은 14:03:21 UTC였다. 현재 retry_count 4와 누적 실패 작업 8개는 서로 다른 집계다.

HAR에서는 14:00:03 UTC부터 `validation_pending`이 보이다가 14:02:29 UTC 응답에서 `validation_error`를 확인했다. HAR 종료 이후에도 DB 큐에는 후속 작업 갱신이 남아 있다. 네트워크의 HTTP 200은 IPC 상태 조회 성공을 뜻하며, 제출 성공을 뜻하지 않는다.

## 2. 공식 맵의 정확한 실패 지점

공식 ZIP 다운로드:

- [공식 레벨 메타데이터](https://api.tuforums.com/v2/database/levels/byId/5510)
- [공식 ZIP](https://api.tuforums.com/cdn/f9cd92a3-e69f-42ae-a159-b8144c8f9f57)
- [공식 CDN 메타데이터](https://api.tuforums.com/cdn/f9cd92a3-e69f-42ae-a159-b8144c8f9f57/metadata)

ZIP 크기는 3,272,151바이트, SHA-256은 `ae5cabf2e19c0fe21057cc106a396fdb278e811bb280133a1719b7b90b36e19a`이다.

ZIP의 `backup.adofai`와 `megawolf77 - Neon Paradise.adofai`는 모두 10,964바이트이고 같은 SHA-256을 가진다:

`1cf53995c60d4ab9ad0b20b33cc4000cd04c5387d0f4a94caed20ea1fab4db2b`

현재 확보한 원문 63행은 다음과 같다.

```text
"useLegacyFlash": "Disabled", ,
```

두 번째 쉼표가 문제다. HAR의 `activity.logical-level.chart.get` 응답 `LevelText`에도 동일한 문장이 있다. BOM을 제외하고 줄바꿈을 정규화하면 HAR 맵과 공식 ZIP 맵이 일치한다. 원본 배포물 자체에 있는 문법이므로, 전송 중에 새로 손상된 것으로 보이지 않는다.

재현 결과:

| 입력 | 웹 `parseADOFAIJsonText` | Rust `compute_gameplay_hash` |
|---|---|---|
| HAR 원문 | `JSON5: invalid character ',' at 63:33` | `Err(InvalidJson)` |
| 공식 ZIP과 같은 `.source` 원문 | 동일한 중복 쉼표 포함 | `Err(InvalidJson)`, JSON5 `expected identifier or string` |
| 해당 쉼표 하나만 메모리에서 제거 | 파싱 성공, 각도 446개·이벤트 118개 | 파싱·해시 계산 성공 |

수정 입력의 서버 해시는 `2005201d453b2ec0b8bd0cba3eb79da8656f099bc7c2faf5566c328bf1e8248d`였다. 실제 파일이나 공식 ZIP을 수정하지 않고 메모리 입력만 바꿔 비교했다.

사이드 대화에서 전달받은 오류 위치는 `60:33`이었지만, 이번 HAR와 현재 공식 ZIP에서 직접 재현한 위치는 **`63:33`**이다. 과거 화면의 정확한 입력 바이트는 확보하지 못했으므로 행 번호 차이까지 동일하다고 단정하지 않는다.

### 게임에서는 왜 열리는가

설치된 게임의 `Assembly-CSharp.dll` 및 `Assembly-CSharp-firstpass.dll`을 확인했다. `LevelData`는 `GDMiniJSON.Json.Deserialize`를 사용한다. `ilspycmd`로 확인한 `GDMiniJSON.Json.Parser.ParseObject()`에는 다음 동작이 있다.

```csharp
switch (NextToken)
{
    case TOKEN.COMMA:
        continue;
    // ...
}
```

객체 속성 사이의 연속 쉼표를 건너뛴다. 따라서 이 맵은 엄격한 JSON/JSON5 문법에는 맞지 않지만, 게임이 실제로 받아들이는 입력이다. 웹과 서버가 게임의 이 허용 동작을 구현하지 않은 것이 호환성 문제다.

### 제출 서버에서 오류가 일반적인 실패로 바뀌는 경로

운영 버전 `93bfea475eb939dcd6d9a71e915d80730abcb541` 기준:

1. `services/tuf/catalog/validation_chart.rs`가 공식 ZIP에서 클라이언트 상대 경로에 해당하는 차트를 선택한다.
2. `domain/gameplay_hash.rs`의 `json5::from_str`가 중복 쉼표를 거부한다.
3. 차트 획득 코드는 이 오류를 `CatalogError::UnsafeArchive`로 바꾼다.
4. `services/submission/trusted_tester.rs`는 획득 오류의 세부 종류를 버리고 `official_chart_unavailable`로 바꾼다.
5. `models/run_submission_records/retry.rs`가 일반 재시도를 예약하고 `submission_temporarily_unavailable`을 노출한다. 해당 재시도 주기가 소진되면 `validation_error`가 된다.

운영 큐에서 확인한 것은 4번의 오류다. 당시 각 시도의 하위 파서 예외/스택은 이 변환으로 보존되지 않는다. 하위 원인은 **공식 원문과 배포 당시와 동일한 해시 소스를 이용한 결정적 재현**으로 확인했다. 로컬 해시 소스는 운영 커밋과 차이가 없었다.

최근 로컬에 추가한 `official_chart_unsupported` 및 제출 전용 해시 계약은 이 운영 버전에 들어 있지 않다. 이번 실패를 새 검증 계약의 거절로 설명하면 안 된다. 새 코드 또한 JSON5 파싱 호환성 자체를 고치지는 않았으므로, 새 커밋 배포만으로 이 문법 문제가 해결되지는 않는다.

## 3. 미리보기의 입력 경로

동반 웹의 차트 iframe은 부모가 전달한 `chart.load`의 `levelText`를 파싱한다. 이번 HAR에는 게임 IPC가 반환한 그 맵 내용이 들어 있고, 위의 중복 쉼표가 그대로 있다. 따라서 메타데이터 404가 없어도 이 입력의 파싱은 실패한다.

독립 `/levels/5510` 로딩도 확인했다. 현재 공개 CDNData를 실제 다운로드 계획 함수에 넣으면 원본 ZIP을 우선 선택한다. ZIP에 목표 `.adofai`가 있으면 `resolveMissingTufPackageFiles`가 같은 경로의 개별 CDN 파일을 다시 받지 않도록 한다.

이 차이가 중요하다. 개별 CDN `.adofai`는 현재 22,502바이트의 정상 JSON으로 정규화되어 있지만, 원본 ZIP과 `.source`는 중복 쉼표를 유지한다. **개별 CDN 파일이 정상이라는 사실만으로 ZIP 경로와 iframe 입력까지 정상이라고 볼 수 없다.**

운영 플레이어 컨테이너의 실제 정적 파일 `floor-icons.compiler-j0ivegt3.js`에서 파서를 직접 읽었다. JSON → JSON5 → BOM/문자열 제어문자/최상위 섹션 누락 쉼표 보정 순서이며, 객체 속성 사이 중복 쉼표를 처리하는 로직은 없다.

배포 식별 참고:

- 자동제출 gateway/web/API 이미지: 모두 `93bfea475eb939dcd6d9a71e915d80730abcb541` 태그.
- web-adofai 운영 체크아웃 HEAD: `4d75f24e3d4a5d2735fe783aa91b07d7da5d3ec9`.
- 해당 체크아웃과 로컬 JSON 파서 파일의 SHA-256이 `05a27c14444b85dd95dd939e5e73b942691d4dda00ecf96d64fcec590fc73318`로 일치.
- web-adofai 이미지의 OCI revision 라벨은 `a04817ce2b7f1a1e8b7cbf8af8f2c027ab072f1d`로 체크아웃과 다르다. 라벨만으로 정확한 빌드 커밋을 단정하지 않았고, 위 파서 판단에는 실제 운영 정적 번들도 확인했다.

## 4. 메타데이터 404의 독립 원인

HAR에는 `/api/tuf/v2/database/levels/byId/*` 요청 17개가 404로 기록됐다. 5510만 없는 문제가 아니다.

운영 gateway에서 확인한 라우팅:

```nginx
location ^~ /api/ {
    proxy_pass http://tuf-replay-server:5150;
}
```

`/api/tuf/v2/database/levels/byId/5510`까지 자체 Rust 서버로 보내지만 그 서버는 TUF의 `/v2/database/...` 프록시가 아니다.

반면 동반 웹의 `web/vite.config.ts`는 `/api/tuf`를 제거하고 `https://api.tuforums.com`으로 보내는 프록시를 개발 서버와 preview 서버 양쪽에 설정하고 있다.

실제 비교:

| 경로 | 응답 |
|---|---:|
| 공개 `tufreplay-auto.impl1113.dev/api/tuf/v2/database/levels/byId/5510` | 404 |
| 웹 컨테이너 내부 `127.0.0.1:4173/api/tuf/v2/database/levels/byId/5510` | 200 |
| 공식 `api.tuforums.com/v2/database/levels/byId/5510` | 200 |

따라서 수정 지점은 gateway의 경로 우선순위다. `/api/tuf/`를 웹 프록시로 보내는 더 구체적인 규칙이 필요하다. 제출 서버가 공식 맵을 취득하는 경로는 별도이므로 이 404와 공식 차트 파싱 실패는 구분해야 한다.

## 필요한 수정과 이번 조사 범위

1. 웹 및 서버 맵 파서에 게임과 일치하는 중복 구분자 호환 처리를 추가한다. 문자열 안의 쉼표까지 지우는 전역 치환은 피하고, 객체/배열과 문자열 경계를 구분해야 한다.
2. 서버가 결정적인 차트 파싱 오류를 일반적인 일시적 다운로드 실패로 숨기지 않도록 오류 분류와 진단 정보를 보존한다.
3. 자동제출 gateway에서 `/api/tuf/*`가 웹 프록시에 도달하도록 라우팅을 수정한다.
4. web-adofai 이미지의 revision 라벨과 실제 빌드 커밋이 일치하도록 배포 식별도 정리한다.

이번 요청은 조사이므로 위 수정은 적용하지 않았다. 게임·브라우저를 실행해 새 플레이를 만들거나 서버 작업을 재실행하지 않았으며, 운영 조회는 SELECT/READ ONLY 트랜잭션, 로그·설정 읽기, 공개 GET, R2 HEAD로 한정했다.

## 후속 조사: 레벨 3132는 웹 재생만 성공한 이유

레벨 3132(`3:03 PM`)의 제출 실행 `2cc174d2-e086-4801-b677-24638f0fad77`도 `validation_error`였으며, 작업 큐의 실패 16건 모두 `official_chart_unavailable`이었다. 검증 산출물과 TUF pass ID는 없었다.

공식 파일 ID는 `afd03f87-899d-4a15-9211-2172362e90ec`이며 ZIP에 `main.adofai`가 있다. 원본 차트는 115,259바이트, SHA-256은 `98ee34177046cd969d37d9c2104b36d3b680d6dbc1a3cac9fdf467dcf0c3bdcd`다. BOM을 제외한 문자열은 HAR의 미리보기 차트와 일치했다.

503행 `AddText.decText`의 일본어 감사 문구 다음에 실제 CR 문자가 들어 있다. 웹 파서는 기존 제어 문자 보정 덕분에 성공하지만, 서버 JSON5 파서는 503:62에서 `expected char_literal`로 실패했다. 문자열 내부의 해당 CR 하나만 메모리에서 이스케이프하면 서버 해시 계산이 성공했다. 따라서 중복 쉼표가 원인인 5510과 입력 문제는 다르지만, 서버의 네이티브 차트 문법 호환성 부족이라는 수정 지점은 같다.

## 적용한 수정과 검증

- Rust의 기존 게임플레이 해시와 제출용 해시가 공통 차트 파서를 사용한다. 일반 JSON/JSON5를 먼저 읽고 필요한 경우에만 호환 처리를 한다. 객체·배열의 중복/선행 쉼표와 문자열 내부 제어 문자를 처리하며 문자열·주석·이스케이프된 줄 연속은 보존한다.
- web-adofai에도 같은 중복 쉼표 처리를 추가했다. 기존 섹션 사이 누락 쉼표 보정도 구조 토큰을 확인하도록 바꿔, 비슷한 장식 텍스트나 주석을 변경하지 않는다.
- 원본 차트 바이트와 SHA-256은 보존한다. 메모리에서 파싱할 때만 호환 처리를 하며 게임플레이 해시 계약과 버전은 변경하지 않는다.
- 공식 차트의 해시 계산 실패를 `UnsupportedChart`로 분류하고 두 검증 모드 모두 결정적인 차트 형식/모호성 문제를 `validation_rejected`로 처리한다. 취득 실패는 레벨 ID와 분류를 서버 로그에 남긴다. 네트워크·타임아웃 등 일시 장애는 기존 재시도를 유지한다.
- 자동제출 gateway의 `/api/tuf/` 전용 규칙이 웹 프록시로 전달한다. `/api/` 제출 API와 `/internal/` 경로는 Rust 서버로 전달한다.

최종 검증:

| 검사 | 결과 |
|---|---|
| `./scripts/run.sh server-check` | Rust 포맷·모든 타깃 검사 및 라이브러리 테스트 62개 통과 |
| web-adofai 관련 파서 테스트 4개 파일 | 51개 통과 |
| web-adofai 타입 검사·변경 파일 Biome·프로덕션 빌드 | 통과, 기존 번들 크기 경고 있음 |
| `./scripts/run.sh check` | 셸 스크립트 33개 구문 검사·배포 stdin 테스트 통과; shellcheck 미설치 |
| 로컬 임시 Nginx 컨테이너 | `nginx -t` 통과, 메타데이터 2개·API·internal·일반 웹 경로의 upstream 및 경로/쿼리 보존 검증 통과 |
| 실제 3132·5510 차트 | 기존 해시와 제출 해시 계산 성공; 웹 일반/compact 프로젝트 파싱과 음악 파일명 추출 성공 |

실제 차트의 수정 후 제출 해시:

- 3132: `0e30bc105dcd92dae378c894e60f85a049db2b73dcefa669b52a26d2afb5290b`
- 5510: `4fe41f25b885b012fd598e48b8e1bf113f40dea71ee83654e320b36bf57cc8d5`

첫 샌드박스 검사에서는 로컬 Redis 연결과 모의 HTTP 서버 바인딩이 권한 오류로 실패했다. 로컬 네트워크 권한을 허용한 동일 검사에서 모두 통과했다. 실제 게임·브라우저 플레이, TUF 등록, 운영 배포, 기존 실패 기록 재처리는 하지 않았다. 따라서 파서와 로컬 라우팅 회귀 수정이 검증된 상태이며 운영 제출 완료를 검증했다는 뜻은 아니다. 배포 이미지 라벨과 서버 체크아웃의 일치 여부도 다음 실제 배포 때 별도로 확인해야 한다.

제출 진행 UI는 앞서 `d0e05ecd`에 이미 커밋됐다. 이번 수정에서는 공유 작업 트리의 활동 DB 마이그레이션·README 변경과 다른 키뷰어/리플레이 작업을 포함하지 않는다.
