# web-adofai 자동 제출 리플레이 조사

조사일: 2026-09-10. 아래는 구현 전 조사 기록이다. 이후 TUFReplay 전환을 구현했으며 현재 변경·실행법·검증 범위는 [구현 보고](tuf-replay-embed-implementation-2026-09-10.md)를 따른다. 실제 게임 실행·브라우저 리플레이 재생 검증은 사용자가 수행한다.

## 결론

사용자 결정: 기존 web-adofai의 creplay 재생 코드를 개조하여 **TUFReplay 전용 재생기로 전환한다. creplay 지원은 제거한다.** 기존 타임라인·행성 렌더링·판정선 계산 중 재사용 가능한 로직을 유지하고, 입력 모델과 파싱 경로를 TUFReplay 증거 형식에 맞게 바꾼다. 두 포맷을 병행하는 어댑터 구조나 creplay 호환 계층은 만들지 않는다. 우리 기록의 실제 타격 시각과 확정 판정을 직접 사용한다.

판정선은 원본 코드뿐 아니라 실제 프리팹·텍스처까지 확인했다. 현재 웹 판정선의 색상과 기본 단위는 상당 부분 맞지만, 도형·배치·페이드·평균 바늘·표시 개수 제한이 다르다. 원본 좌표계와 스프라이트 배치를 재현하면 외형을 훨씬 정확히 맞출 수 있다. 다만 녹화 당시의 난수와 렌더 프레임까지 없으므로 모든 상황에서 당시 화면과 픽셀 단위로 동일하다고 보장할 수는 없다.

## 조사한 소스

- web-adofai: `/Users/kgh/dev/src/adofai-web-editor`, HEAD `0792176`와 현재 작업 파일.
- TUFReplay: `/Users/kgh/dev/src/tuf-replay`, HEAD `3a3e5606`와 현재 작업 파일.
- 원본 게임: `/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app/Contents/Resources/Data`.
- `Managed/Assembly-CSharp.dll` SHA-256: `d0cab90275486f57b2bf6349ef672c7fd56ad631fb74b0fa4129af093995ce54`.
- ILSpy CLI 10.0.1.8346으로 `scrHitErrorMeter`, `scrMisc`, `scrPlanet`, `scrController`를 확인했다.
- AssetRipper headless HTTP 서버로 Data 폴더를 읽고 `resources.assets`의 JSON과 PNG를 확인했다. 처음 resources.assets만 읽었을 때 MonoBehaviour 구조가 비어 있었지만, Data 폴더 전체를 읽으면 Managed 어셈블리가 함께 해석되어 필드가 복원됐다.
- AssetRipper의 collection index는 로드 순서에 따라 달라진다. 아래 PathID는 `resources.assets` 기준이다.

## 기존 웹 재생 경로

아래 파일명과 동작은 조사 당시의 현황이다. creplay 이름과 포맷을 앞으로 유지한다는 뜻은 아니다.

| 파일 (web-adofai 기준) | 확인한 역할 |
|---|---|
| `src/web/pages/replay.page.tsx` | 로컬 차트·리플레이 선택, Clock, RAF, 재생·정지·seek, 오버레이 |
| `src/replay/replay-viewer-data.factory.ts` | 차트 파싱·컴파일 → creplay 파싱 → 재생 타임라인 생성 |
| `src/replay/creplay.schema.ts` | creplay 입력·타격·메타데이터 모델 |
| `src/replay/creplay-playback.evaluator.ts` | 각도에서 타격 시각 역산, 판정 재계산, 다음 행성 keyframe 생성 |
| `src/replay/creplay-hit-error-meter.evaluator.ts` | 오차 정규화와 판정선 색상 구간 |
| `src/editor-engine/replay-viewer-runtime.controller.ts` | 기존 Evaluator·RenderingRuntime을 이용한 행성·트랙·배경·카메라 렌더링 |
| `src/web/replay-viewer/lib/hit-error-meter.evaluator.ts` | 현재 시각의 평균·카운트·남아 있는 tick 계산 |
| `src/web/replay-viewer/components/hit-error-meter-overlay.component.tsx` | CSS 판정선·바늘·카운트 |

`replayCompiled`는 컴파일된 차트의 `planetKeyframes`를 리플레이 keyframe으로 교체한 구조다. 렌더러 전체를 다시 만들 필요는 없다.

하지만 현재 replay runtime은 초기화와 준비 시 `setDecorationsVisible(false)`를 호출한다. VFX 전체 지원이라고 간주하면 안 된다. 현재 replay page는 별도 Clock으로 움직이며 음악 재생을 연결하는 코드가 없다. 일반 EditorEngine에 있는 pitch·음량 기능이 이 별도 재생기에 자동으로 적용되지 않는다.

일반 엔진의 `playbackPitchPercent`는 1–1000%, 정수 반올림·클램프 정책이 있다. 이 정책과 오디오 구현을 공유하되 replay runtime에도 명시적인 연결이 필요하다. 현재 확인한 embedded 메시지는 에디터 닫기 요청이며, TUF용 재생 제어 계약은 별도 작업이다.

## 우리 증거를 연결하는 방법

`TUFReplay/Submission/Protocol/EvidenceRecordWriter.cs`가 보내는 포맷:

- nativeInput: `timeUs,key,flags,nativeCode,nativeFlags` 5열.
- hitContext: `currentFloorId,currAngle,overloadCounter,noFailHit,isAuto,nextFloorAuto,cachedAngle,targetExitAngle,midspinInfiniteMargin,rdcAuto,curFreeRoamSection,resolvedHitMargin,timeUs` 13열.
- lifecycle/settings/health: 버전·state·time_us·rate·no_fail·difficulty·dropped·unmapped·hold_behavior 등의 JSONL.

기존 creplay 모델·파서·타임라인을 TUFReplay 입력에 맞게 직접 전환한다. TUFReplay 모델에 실제 타격 시각, 확정 판정, 입력 키 공간, 시간 원점과 기록 조건을 명시한다. 파일 파서, 시간 변환, key mapping, 타임라인 계산, 화면 렌더링, iframe 통신은 각각 역할을 분리하되 여러 리플레이 포맷을 지원하기 위한 추상화는 추가하지 않는다.

### 전환 범위

- 재사용할 타임라인·키 뷰어·판정선 로직과 타입은 TUFReplay 의미에 맞게 수정하고 `creplay-*` 파일명과 `Creplay*` 타입명을 함께 정리한다. 공통 렌더러처럼 포맷과 무관한 이름은 유지한다.
- creplay 전용 컨테이너·바이너리·레거시 JSON 파서, 복호화, magic/version 처리와 전용 의존성을 제거한다. 공유 코드·의존성은 다른 사용처를 확인한 뒤 필요한 것을 유지한다.
- 파일 선택 UI, 허용 확장자, factory 진입점, export, 안내 문구도 TUFReplay에 맞춘다. `.creplay` 업로드·자동 감지·호환 fallback은 남기지 않는다.
- 타격 시각 역산 경로는 실제 `TimeUs`를 사용하는 경로로 교체한다. 필수 시각이 없는 기록은 명시적으로 지원하지 않는다고 처리하고 creplay 방식으로 조용히 역산하지 않는다.
- 기존 테스트에서 재사용 가능한 행성·판정선 사례는 TUFReplay 모델로 옮긴다. creplay 포맷 전용 테스트·fixture는 제거하고 실제 TUFReplay 증거의 시간·판정·키 매핑 검증으로 교체한다.
- 이번 결정은 구현 방향의 확정이다. 이 문서 수정 시점에는 제품 코드의 전환·삭제를 실행하지 않았다.

| TUF 필드 | 재생에서 사용할 곳 | 주의점 |
|---|---|---|
| hitContext `TimeUs` | 타격 이벤트 시각 | 기존 `solveHitTime_us`의 역산값으로 대체하지 않는다 |
| `CurrentFloorID`, 각도, free-roam section | 행성 전이와 오차 | 타일 인덱스·다중 행성·hold·midspin·free-roam 대응 확인 필요 |
| `ResolvedHitMargin` | 기록된 판정 표시 | 게임 enum과 웹 enum을 명시적으로 매핑; 숫자를 그대로 캐스팅하지 않는다 |
| nativeInput `TimeUs` | 키 누름/뗌 표시 | 타격 시각과 입력 시각을 합치지 않는다 |
| nativeInput key/nativeCode/flags | 키 뷰어 | 플랫폼별 키 공간과 Down 비트를 해석한다 |
| 런타임 settings | 원래 플레이 pitch·난이도·hold 정책 | 관람자가 조절한 pitch와 원래 판정 계산 조건을 분리한다 |
| chart/evidence digest, file ID | 정확한 재생 자료 묶음 | 최신 레벨 URL만 참조하지 않는다 |

기록 재생은 서버 검증을 대체하지 않는다. 저장된 판정을 표시하는 것과 서버가 플레이를 검증했다는 것은 별개다. 관람 pitch를 바꿔도 이미 기록된 판정이나 판정선의 원래 오차가 달라져서는 안 된다.

### 시간축과 실제 fixture

`RecordingClock.ToRecordTimeUs`는 `(songposition_minusi - GameplayStartSongPosition) * 1_000_000`이다. 입력은 native timestamp를 conductor anchor에 보간하며 클리어 이후에는 unscaled 시간으로 이어진다. 단순히 `TimeUs / 1e6`를 음악의 절대 재생 위치로 쓰면 안 된다.

현재 fixture `092eb0faa4694a1a82019a58fe2c113f`:

| 항목 | 실제 확인값 |
|---|---:|
| 곡 / 레벨 | The Limit Does Not Exist / 3072 |
| hitContext | 1,360행, 모두 13열 |
| nativeInput | 2,762행, 모두 5열 |
| 첫 입력 | −746,398 µs |
| 첫 타격 | 233,580 µs |
| 마지막 타격 / clear | 113,733,580 µs |
| gameplayStartSongPosition | 0.7027529014379621초 |
| inputKeySpace / platform | os-native-key-code / macos |

로컬 fixture 메타데이터에는 시작 song position·플랫폼·원래 pitch가 있다. 하지만 현재 EvidenceRecordWriter의 전송 내용에는 이 메타데이터 전부가 들어 있지 않는다. 자동 제출 서버에 저장된 자료만으로 재생 패키지를 만들 때 무엇을 복원할 수 있는지 확인하고, 필요한 time origin·key space·버전 필드를 버전 있는 메타데이터로 보완해야 한다. 임의로 0초·Windows 키 코드라고 가정하면 안 된다.

첫 구현은 이 fixture와 로컬 차트로 진행할 수 있다. 노래 시작 오프셋·카운트다운·conductor 시간축을 실제 차트 컴파일 결과와 대조한 뒤 음악에 연결해야 한다. 음수 입력은 삭제하거나 0초로 몰지 않는다.

## 원본 판정선: 코드와 자산에서 확인한 값

### 위치와 크기

| 항목 | 값 / 근거 |
|---|---|
| Error Meter GameObject | PathID 7513 |
| scrHitErrorMeter 컴포넌트 | PathID 14386 |
| CanvasScaler | PathID 14166; reference 2560×1440, ScaleWithScreenSize, MatchWidthOrHeight=0 |
| 기본 anchor/pivot | (0.5, 0.03) |
| wrapper | PathID 12101; size 334×135, 기본 anchoredPosition (0,−48) |
| Small | scale 0.75, wrapper y −30 |
| Normal | scale 1, wrapper y −48 |
| Large | scale 1.5, wrapper y −71 |
| ExtraLarge | scale 2, wrapper y −94 |
| StraightMeter | GO 7642, RectTransform 12239, size 400×200, anchor/pivot (0.5,0), position (0,−12) |
| StraightMeter texture/sprite | Texture2D 304 / Sprite 3820; texture 400×200, bilinear filtering |
| Tick | GO 5989, RectTransform 12665, size 8×182, pivot (0.5,0), sprite 4494 → texture 1030 |
| 평균 바늘 Hand | Image 13625, RectTransform 11945, size 30×140, pivot (0.5,0), sprite 5483 → texture 2101 |

위 숫자는 화면의 CSS px가 아니라 Unity 기준 좌표다. 기본 scaler를 재현하면 배율은 게임 표시 영역의 너비/2560이다. 예를 들어 너비 1280에서 0.5배다. iframe 문서 전체 대신 실제 게임 표시 viewport를 기준으로 계산해야 한다. CanvasScaler에 직렬화된 scaleFactor=2.05를 고정 배율로 쓰면 안 된다.

PNG를 직접 확인했으며 배경 선과 중앙 표시는 텍스처에 그려져 있다. Hand도 단순 V자 outline이 아니라 흰색 채움과 투명 여백이 있는 이미지다. Tick 8×182 전체가 화면에 꽉 찬 세로선이라는 뜻이 아니다. sprite rect, 투명 여백, pivot을 함께 보존해야 한다. StraightMeter의 sprite rect는 약 333.84778×69.847755이며 원본 400×200 이미지 내부에 위치한다. UI Image는 Simple, UseSpriteMesh=false이므로 Unity의 sprite padding 처리도 반영해야 한다.

### 오차·색·애니메이션

`scrHitErrorMeter.AddHit` 기준:

1. 전달받은 angleDiff(rad)에 −57.29578을 곱한다.
2. 실제 floor speed·conductor pitch·marginScale의 Counted 각도 경계로 나눠 ±60 단위로 정규화한다.
3. ±60 밖이면 ±(60.0001 + Random.value×3)으로 표시한다.
4. ±60 안의 값만 `average += (value - average) × 0.2`로 평균에 반영한다.
5. 직선형 tick 위치는 `x = −value×2.5`, `y = −62`다. 평균 바늘도 같은 좌표식이다.
6. 평균 바늘은 0.25초 OutCubic으로 목표까지 이동한다. 현재 표시 위치에서 이어지는 tween 동작까지 맞춰야 한다.
7. tick은 최대 60개 순환 캐시다. 수명은 기본 3초이며 InQuad로 알파가 0까지 감소한다. 단순 페이드에서 α=1−(age/3초)²에 해당한다.
8. 협동 모드는 수명 배율 0.25(0.75초)이고 행성색을 쓰는 분기가 있다. 현재 TUF 단일 플레이의 기본 대상과 구분한다.
9. 곡선형은 같은 정규화 값을 위치가 아닌 회전각으로 사용한다.

RDConstants PathID 12887의 `hitMarginColoursUI`를 직접 확인했다:

| 구간 | 색 |
|---|---|
| Too Early / Too Late | #FF0000 |
| Very Early / Very Late | #FF6F4D |
| Little Early / Little Late | #FCFF4D |
| Perfect | #5FFF4E |

웹의 현재 색상 상수는 이 UI 색상과 일치한다. `hitMarginColours`는 별도 팔레트이므로 섞지 않는다. 고정된 배경 텍스처와 판정별 tick 색상 경계 계산도 구분한다.

`scrMisc.GetAdjustedAngleBoundaryInDeg`는 단순 고정 ms가 아니다. PC의 Counted 시간 기준은 Lenient 91ms / Normal 65ms / Strict 40ms이며 speed trial로 나눈다. Perfect 30ms, Pure 20ms 역시 speed trial로 나누고 최소 25ms가 적용된다. 이를 BPM·speed·pitch로 각도로 바꾼 값과 각도 기준을 비교해 큰 값을 쓴다. Pure 30°, Perfect 45°, Counted는 `GCS.HITMARGIN_COUNTED`, 각각 margin multiplier가 적용된다. 따라서 저 BPM과 고 BPM의 표시를 둘 다 비교해야 한다.

### 현재 웹 구현과 차이

| 항목 | 현재 웹 | 필요한 변경 |
|---|---|---|
| 모양 | CSS 막대와 V자 바늘 | 원본 sprite/padding/pivot 재현 |
| 배치 | bottom-64 / sm:bottom-28, 고정 CSS 크기 | 원본 viewport 기준 scaler와 anchor |
| tick fade | 3초 선형 | 3초 InQuad |
| tick 수 | 최근 3초 전체 | 60개 순환 캐시 동작 |
| 평균 바늘 | 기록된 평균값으로 즉시 위치 변경 | 250ms OutCubic |
| miss 평균 | 범위 밖 값도 평균에 반영 | ±60 안에서만 평균 갱신 |
| 범위 밖 위치 | hit index 기반 결정적 값 | 원본은 랜덤; 완벽한 과거 프레임 복원 불가능 |
| 타격 시각 | 각도에서 역산 | TUF TimeUs 직접 보존 및 시간 원점 변환 |
| 판정 | 웹에서 재계산 | 기록된 확정 판정 표시, 계산값은 진단용 구분 |

추가로 원본 scrPlanet에는 free-roam 진입/이탈 중 판정선 표시를 바꾸는 분기가 있다. 모든 hit가 무조건 tick 하나가 된다고 단정하지 말고 실제 AddHit 호출 조건과 midspin/auto/hold/free-roam 사례를 검증해야 한다. seek 때도 tick·평균 tween을 목표 시각에 맞게 복원해야 한다.

## TUF에서 로드하고 조작하는 구조 제안

1. TUF pass 페이지는 기존 가벼운 플레이스홀더만 렌더링한다.
2. 사용자가 로드를 누를 때 replay 전용 iframe을 생성한다. 일반 에디터 페이지를 숨기는 방식 대신 게임 viewport와 HUD만 가진 route를 둔다.
3. iframe ready 후 버전 있는 replay manifest를 전달한다. 검증된 chart snapshot, 음악/장식 asset 목록, evidence 또는 파싱된 timeline, 시간·플랫폼·기록 조건이 포함된다. 내부 서버 토큰을 브라우저에 전달하지 않는다.
4. play/pause/seek(timeUs)/restart/setPitch/setVolume/setVfx/setMeterSettings 명령과 ready/state/time/error 이벤트를 명시적인 타입으로 정의한다.
5. 양쪽에서 exact origin, event.source, 메시지 스키마, 세션 ID를 확인한다. 빠른 pass 이동에서 이전 iframe 메시지는 무시한다.
6. 음악 clock과 replay clock을 하나의 coordinator가 관리한다. 화면 seek·음악 seek·tick 평균 복원·key down 상태가 같은 목표 시각으로 움직여야 한다.
7. 설정은 TUF overlay에 두고 iframe viewport를 줄이지 않는다. 몰입형 전환 시 가능하면 iframe을 유지해 재다운로드와 WebGL 재초기화를 피한다.
8. 닫기/페이지 이동 시 RAF, audio nodes, renderer, asset URL, listeners를 해제한다. 낮은 빈도의 상태 이벤트만 parent에 전송하고 렌더링은 iframe 내부에서 처리한다.

차트가 변경되어도 제출 시 검증한 차트·자료로 재생하는 것이 일관된다. 현 storage가 재생에 필요한 음악과 장식까지 보존하는지는 별도 확인해야 한다. 차트 JSON만 있으면 원본 VFX와 소리를 완전히 재생할 수 없다.

## 권장 구현 순서와 완료 기준

1. 기존 creplay 모델·파싱·타임라인을 TUFReplay 전용으로 전환하고 creplay 지원 경로를 제거한다. 로컬 성공 fixture를 기존 renderer까지 연결해 timestamp/enum/key mapping과 음악 offset부터 검증한다.
2. replay runtime에 음악·pitch·장식 resource loading을 연결한다. 기록 당시 pitch와 관람 pitch를 분리한다.
3. 원본 판정선 straight/Normal부터 재현한다. 캡처된 sprite 좌표·색·수명·평균 애니메이션을 적용한다.
4. TUF replay manifest 및 on-demand iframe bridge를 연결한다.
5. 정상 재생, pause/resume, 앞뒤 seek, pitch 변경, VFX 변경, 몰입형/설정 열기, pass 이동/취소를 실제 브라우저에서 검증한다.

전환 완료 시 활성 코드·UI·export·테스트에 creplay 지원 경로가 남지 않았는지 확인한다. 조사 문서에 남은 과거 파일명은 이력으로 유지할 수 있다. TUFReplay 실제 fixture 재생과 기존 공통 렌더링 회귀 검증을 모두 통과해야 한다.

원본 비교는 같은 해상도·같은 판정선 크기·같은 기록 조건으로 한다. 단일 타격 후 0/0.125/0.25/1.5/3초, 60개 초과 연타, ±60 밖 판정, BPM/방향/마진 변경, midspin/auto/hold/free-roam이 핵심 사례다. 실제 게임 캡처와 비교하기 전에는 “완전히 동일” 완료로 표시하지 않는다.

현재 조사에서 아직 하지 않은 것: 원본 게임 실행, 실제 리플레이 재생 연결, 오디오 동기화 실측, 모든 AddHit 호출 경로 검증, snapshot asset 보존 범위 확인. 실행 중인 제품 서버·DB·기록은 변경하지 않았다.
