# TUFReplay SDK v2 리팩터링 결과

작성일: 2026-10-12

합의한 `TUFReplaySession` / `TUFReplayConnection` / `TUFReplayKeyEvent` 계약으로 SDK, Renderer, JRP 키뷰어 및 기존 JRP 오버레이, Impl 오버레이를 전환했다. 시간 wrapper와 native getter 등록을 제거했고, 외부 모드는 동봉된 SDK DLL을 직접 참조한다. 자동 검증과 배포물 생성은 완료했다. 실제 ADOFAI에서의 로딩·화면·성능 검증은 사용자가 나중에 하기로 한 범위로 남아 있다.

## 작업 위치

| 대상 | 경로 | 브랜치 |
| --- | --- | --- |
| 기본 작업 디렉토리·결과 문서 | `/Users/kgh/dev/src/tuf-replay-dev` | `dev` |
| Renderer·공용 SDK | `/Users/kgh/dev/src/TUFReplay-Renderer` | `main` |
| JRP | `/Users/kgh/dev/src/jipper-resourcepack` | `feat/tufreplay-render-support` |
| Impl SDK 연동 checkout | `/Users/kgh/dev/src/impl-resourcepack-tufreplay` | `feat/tufreplay-overlay-support` |

기존 작업 변경을 보존했다. `impl-resourcepack-render`, `impl-dmnote`, DmNote 및 IPC 기능 소스는 수정하지 않았다. 기존 웹 오류 선택 UI 변경도 이번 작업에서 수정하지 않았다. 게임 설치 파일을 갱신하거나 게임을 실행하지 않았으며, 커밋·push·PR 생성도 수행하지 않았다.

## SDK 계약

공용 API 사용 문서는 [SDK README](/Users/kgh/dev/src/TUFReplay-Renderer/sdk/README.md)에 있다. 특정 모드의 클래스나 갱신 함수에 의존하지 않는다.

- `TUFReplaySession.IsActive`로 공급원을 명시적으로 선택한다. UI 시계는 `ElapsedTime`(double, 영상 경과 초), `DeltaTime`(float, 이번 요청의 경과 초)을 사용한다. 비활성일 때 시간·입력 데이터 조회는 예외를 발생시킨다. `Id`는 비활성 시 0, `Context`는 null이다.
- 실제 대기·타임아웃·성능 측정은 native 시계를 사용한다. 판정·진행도 등은 기존 게임 시뮬레이션 상태에서 읽는다.
- `TUFReplayKeyEvent`는 Unity KeyCode 정수, 눌림/뗌, 기록 시간, 영상 시간, 같은 기록 시간에서의 순서를 전달한다. 기록에는 native 전용 키나 장치 ID가 없으므로 그 지원을 주장하지 않는다.
- 이벤트 콜백에서는 해당 전이까지 반영된 눌림 상태를 관측한다. 프레임 요청 콜백과 polling 조회에는 모든 전이가 반영된 최종 상태를 제공한다. 같은 요청 안의 짧은 탭은 Down/Up 둘 다 참일 수 있고 최종 held 상태는 false일 수 있다. polling 모드가 그 탭을 놓치는 동작은 보정하지 않는다.
- 입력 정렬은 기록 시간 우선, 같은 시간에서는 Sequence 순이다. 서로 다른 시간에 대해 Sequence가 전역적으로 증가해야 한다는 제약은 추가하지 않았다.
- `TUFReplayConnection`은 시작·종료·입력·표시 여부·프레임 요청을 연결한다. 특정 UI 갱신 함수 구현은 요구하지 않는다. 기존 Update에서 `PendingFrame`을 처리하거나 외부 갱신 콜백에서 처리할 수 있다.
- `CompleteFrame(request)`은 이 연결에 실제로 배정된 요청 객체만 받는다. 연결마다 같은 논리 FrameId를 가진 별도 요청 객체를 배정한다. 과거 세션·다른 연결·중복·취소·입력 전송 중의 성급한 완료는 캡처를 허용하지 않는다. FrameId는 Unity frameCount와 독립적이다.
- 지연 작업에는 선택적으로 `EnterFrame(request)`을 사용한다. 해당 요청의 delta와 입력 snapshot을 노출하므로 Renderer의 0 delta 대기 상태에서도 기존 갱신을 처리할 수 있다. 같은 스레드에서 scope를 종료해야 한다.
- 추가 refresh는 시간·held 상태를 고정하고 delta를 0, 입력 edge를 비운다. 임의 sleep으로 UI 완료를 추정하지 않는다.
- 지연 작업의 오류는 `ReportFailure(request, exception)`으로 해당 요청에 묶는다. 이미 완료되었거나 취소된 요청, 이전 요청·세션의 오류를 무시한다. 완료와 오류의 경쟁은 요청의 원자적 상태 전이로 결정한다. 동기적인 게임 hook에는 `ReportFailure(exception)`도 제공한다.
- 외부 소비자를 위한 기존 `IOverlayParticipant`, `IOverlayNativeServices`, `OverlayTime`, `OverlayInput`, `OverlayEnvironment`, `RenderStopwatch`는 제거했다. SDK v1 소비자는 v2 계약으로 이동해야 한다.

## Renderer의 캡처 경계

[RecordedReplayDriver](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs)는 시뮬레이션 전 UI 시각을 준비하고, 기존 CSV의 입력 순서를 유지해 재생 이벤트와 held snapshot을 공급한다. 게임 입력 재생 경로는 계속 기존 경로를 사용한다.

모든 유효 연결이 해당 요청을 완료해야 `ReadyForCapture`가 열린다. 기존 [RendererController](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs)의 Unity Update/LateUpdate/EndOfFrame 순서에 연결했고, 기존 Canvas 반영 및 overlay camera 캡처를 유지했다. 입력 처리 → 모드의 기존 갱신과 텍스트 반영 → 정확한 요청 완료 → Canvas 반영 → 캡처 순서다. 높은 영상 FPS에서 같은 캡처 화면을 반복하는 정책도 유지했다.

완료 타임아웃은 실제 Stopwatch 시간으로 10초다. 오류가 발생하면 기존 job의 사용자 선택 흐름으로 연결한다. 사용자가 제외를 선택하면 해당 표시를 숨기고 나머지 연결을 계속 기다린다. 제외하지 않으면 렌더를 취소하며 자동 재시도하지 않는다. 오류 선택 대기 동안 다른 연결의 타임아웃이 쌓이지 않도록 처리했다. 제외에 필요한 숨김 자체가 실패하면 캡처를 열지 않는다.

과거 반사 기반 모드 어댑터 파일 일부는 기존 저장소에 남아 있지만 csproj의 `Compile Remove`로 배포 어셈블리에서 제외된다. 이 파일들을 현재 실행 경로로 사용하지 않는다.

## JRP: 같은 기능 코드와 소유 상태 사용

핵심은 수동 `LiveView` 필드 목록을 옮기는 것이 아니라, 평소 실행과 렌더 실행이 같은 상태 생성 및 처리 경로를 사용하는 구조다.

[ViewerState](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/ViewerState.cs)가 root, rain manager, 키 표시, 카운터, KPS 큐, 바인딩, 레이아웃 캐시 등 실행 상태를 소유한다. 기존 코드의 접근 지점을 이 상태로 연결했다. 평소 활성화와 렌더 시작 모두 같은 factory 및 기존 키뷰어 초기화 함수를 사용한다. 렌더는 저장되지 않는 새로운 카운터 상태로 생성되고, 종료 때 상태 전체를 폐기한 뒤 live 상태 객체로 돌아간다.

[KeyViewer.Update](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs)는 native SkyHook 이벤트와 재생 이벤트 모두 기존 작업 큐 → `ProcessKeyEvent` → label 바인딩 → `Work` 경로를 사용한다. 별도 `_renderKeys` / `_renderIndexes` 순회와 렌더 전용 입력 바인딩을 제거했다. Unity KeyCode 기록을 정상 label 경로로 변환하며 native 코드 0을 임의로 합성하지 않는다.

프레임 요청은 같은 큐에 barrier로 들어간다. 입력 작업 스레드가 앞선 이벤트를 처리한 후, 평소의 KeyViewerUpdater가 KPS·레이닝·키 표시·AsyncText를 반영하고 완료를 알린다. KPS를 표시하지 않는 레이아웃에서도 이 공통 갱신 경로를 유지한다. RainManager의 별도 자동 Update를 제거해 중복 이동을 막았다. 기존 레이닝 parent plane 수정(`SetParent(..., false)`)을 유지했다.

[KeyViewer.Render](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs)는 연결, 공통 factory, 시작·종료와 입력 복귀를 담당한다. 기능별 필드 snapshot을 저장·복구하지 않는다. 키 박스·레이닝·카운트 처리의 새 기능은 기존 처리 본체와 소유 상태에 추가하면 두 실행에서 사용된다.

렌더 동안 실제 입력을 UI에 혼합하지 않고 복귀용 상태만 기억한다. 종료 후 현재 native 눌림 표시를 되돌리되 새 카운트·KPS·레이닝 입력으로 다시 처리하지 않는다. 렌더 상태는 누적 카운터를 저장하지 않는다. 시작 실패·제외·취소 때도 렌더 상태를 정리한다.

기존 JRP 오버레이도 새 SDK로 이동했다. 개별 TextView/RectView/ObjectView snapshot 대신 같은 생성 경로로 만든 Overlay 전체를 소유·폐기한다. 상태 조회와 combo 애니메이션을 기존 공통 `UpdatePresentation`에서 처리한다. 기존 VersionSafe 갱신 예약 경로 끝에서 요청을 완료하며 `JOverlay` 타입도 유지한다. 범위 밖 기능이나 동영상 생성 기능을 추가하지 않았다.

## Impl: 기존 scope와 생성 경계 유지

[ModRuntime](/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/ModRuntime.cs)의 connection이 기존 [GameplayOverlay](/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/GameplayOverlay.cs) 및 [OverlayRenderScope](/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/OverlayRenderScope.cs)에 위임한다. 기존 factory로 렌더 표시를 만들고 폐기하는 소유 구조를 유지했다.

StatusTimeTicker와 ComboAnimationDriver는 재생 시간 또는 기존 Stopwatch를 명시적으로 선택한다. 실제 FPS 측정 시계는 그대로 사용한다. scope 시작 실패 때 live 표시가 캡처에 다시 섞이지 않도록 숨김과 session 소유권을 종료까지 유지하고, 종료 후 native 표시로 복귀한다.

## DLL 배포 및 Mono 검증

SDK는 Renderer 모노레포의 독립 프로젝트 `netstandard2.0`, 버전 `2.0.0`, assembly version `2.0.0.0`이다. Renderer 내부에는 ProjectReference가 있고, **JRP와 Impl에는 고정된 DLL Reference만 있다**. 각 모드 패키지에 같은 DLL을 동봉한다. 외부 모드 빌드에 Renderer 소스 checkout이나 별도 의존성 모드가 필요하지 않다.

최종 SDK DLL SHA-256:

```text
3d2db92252c90feac410790b11dfbb5dced52a67e743a4822860632778cc763e
```

SDK 출력, Renderer 빌드·stage·ZIP, 두 모드의 libs와 stage, Impl ZIP 및 standalone SDK ZIP 안의 DLL이 같은 hash임을 확인했다. SDK ZIP의 문서·라이선스·checksum 검증도 통과했다.

Mono 테스트는 Renderer/키뷰어/오버레이 폴더에 복사된 DLL과 FullName 기반 resolver로 두 로딩 순서를 실행했다. 동일 assembly 객체와 공유 세션·입력·요청 완료가 유지되는 것을 확인했다. 별도 assembly instance 또는 혼합 버전이 이미 로드되면 렌더 시작 전에 명시적으로 거부한다. 이 테스트는 설치된 게임의 JALib/UMM loader 실험을 대신하지 않는다.

배포 시 Renderer와 모든 참여 모드를 같은 SDK DLL로 함께 갱신하고 게임을 재시작해야 한다. 이미 실행 중인 SDK v1 소비자의 호환성은 제공하지 않는다.

## 생성한 배포물

| 산출물 | 경로 |
| --- | --- |
| 독립 SDK ZIP | [TUFReplay.Overlay-2.0.0.zip](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.Overlay-2.0.0.zip) |
| SDK ZIP checksum | [TUFReplay.Overlay-2.0.0.zip.sha256](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.Overlay-2.0.0.zip.sha256) |
| Renderer ZIP | [TUFReplay-Renderer.zip](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay-Renderer.zip) |
| Renderer 다운로드 manifest | [TUFReplay-Renderer.download.json](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay-Renderer.download.json) |
| JRP 갱신 payload | [artifacts/render-support/JipperResourcePack](/Users/kgh/dev/src/jipper-resourcepack/artifacts/render-support/JipperResourcePack) |
| Impl ZIP | [ImplResourcePack.zip](/Users/kgh/dev/src/impl-resourcepack-tufreplay/build/ImplResourcePack.zip) |

JRP payload는 기존 전체 JRP 배포본 위에 프로그램 파일을 갱신하는 형태다. 원래 bootstrap, bundle, 폰트, localization 자산은 유지해야 한다. 실제 설치에는 적용하지 않았다.

## 이번 변경에서 실행한 검증

각 저장소의 `./scripts/run.sh`를 사용했다.

| workflow | 결과와 범위 |
| --- | --- |
| Renderer `overlay-sdk-check` | 24개 검증 그룹 및 Mono forward/reverse 로딩 검증 통과. 세션 전환, 입력 관측 시점·순서·snapshot, polling, 지연 worker, 다중 요청, 잘못된 완료, 취소·해제·제외, 지연 오류, 타임아웃, 0 delta, 혼합 버전 거부, 조회 allocation, 실제 엔진 동기화 경계 포함. |
| Renderer `mod-check` | Release 빌드 0 warning/0 error. parser·timeline·실제 FFmpeg 미디어 및 엔진 검증, SDK v2 검증, 기존 bridge 검증, 임시 경로의 설치·패키지 보존 검증 통과. 실제 게임 설치는 skip. |
| JRP `check` | production Rain/RainPool의 parent plane 회귀 검증 통과. production 입력·상태·Key/AsyncText 코드를 연결한 3개 시나리오 그룹 통과: 손·발·고스트 공통 처리, worker/UI 완료, 레이닝 시간, 저장 억제·native 복귀, 제외·취소·factory 실패. Unity/SkyHook/layout setup은 테스트 대역이므로 실제 화면 검증은 아님. |
| JRP `package` | 본체와 VersionSafe R136/R141/R146/R149 빌드 및 payload 생성 통과, 0 warning/0 error. 기존 선택적 KeyboardChatterBlocker DLL은 compile 참고 경로로만 제공. |
| Impl `mod-check` | Debug 빌드 0 warning/0 error, 17개 기존 검증 그룹 통과. 기존 scope 검증에 SDK v2 연결·완료 및 실패 시 native 표시 격리를 포함. |
| SDK·Renderer·Impl `package` | 배포물 생성 및 해당 workflow의 integrity 검증 통과. |
| 저장소·스크립트 확인 | 관련 저장소 `git diff --check` 통과. 수정 대상 branch와 보호 경로 확인. Renderer/Impl의 shell syntax 검증 통과; shellcheck가 없어 해당 정적 lint는 실행하지 못함. |

과거에 남아 있던 legacy overlay kernel fixture의 통과를 현재 SDK 연동 증거로 사용하지 않았다. 현재 SDK와 실제 모드 처리 코드를 별도 검증했다. 기존 웹 테스트 결과도 이번 변경의 신규 검증 결과로 보고하지 않는다.

## 남은 실제 환경 검증

사용자가 나중에 게임에서 검증하기로 한 범위는 남아 있다. 게임 실행을 다시 요청하지 않았다.

- 설치된 JALib/UMM loader에서 세 모드의 SDK DLL이 같은 assembly/session으로 연결되는지.
- 실제 Unity Canvas에 키 박스·손/발/고스트 레이닝·KPS·카운터·JRP/Impl 오버레이가 표시되는지.
- 출력 FPS가 시뮬레이션 FPS보다 높은 경우, pause·취소·오류 제외·native 복귀 및 저장 파일 불변 여부.
- Windows 실제 실행 및 플랫폼별 native 입력 복귀.
- 실제 게임에서의 CPU·allocation·프레임 시간 변화. SDK 조회 allocation 검증은 전체 모드 성능 benchmark가 아니다.

따라서 소스 구조·자동 검증·배포물 생성은 완료했지만, 실제 게임의 시각적·플랫폼 호환성을 검증했다고 주장하지 않는다.
