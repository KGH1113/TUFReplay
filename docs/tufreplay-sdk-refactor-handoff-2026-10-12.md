# TUFReplay SDK 리팩터링 — 현재 코드 조사와 구현 요청

작성일: 2026-10-12. 아래 현재 상태는 작업 디렉토리의 실제 소스를 다시 읽고 확인했다.

**현재는 모드별 렌더 어댑터와 시간 wrapper 기반이다. TUFReplaySession/TUFReplayConnection과 별도 입력 이벤트는 앞으로 구현할 목표이며, 이미 적용된 구조가 아니다.**

사용자는 이 합의 내용을 메인 챗에서 구현해 달라고 요청했다. 사이드 대화에서는 소스 조사와 문서만 작성했다. 메인 챗 메시지 전송은 앱 오류로 실패했다.

## 1. 작업 위치

| 대상 | 실제 경로 | 확인한 브랜치 |
| --- | --- | --- |
| 기본 작업 디렉토리 | /Users/kgh/dev/src/tuf-replay-dev | 구현 전에 최신 상태 확인 |
| Renderer와 공용 SDK | /Users/kgh/dev/src/TUFReplay-Renderer | main |
| JRP 키뷰어 | /Users/kgh/dev/src/jipper-resourcepack | feat/tufreplay-render-support |
| Impl SDK 연동 작업 | /Users/kgh/dev/src/impl-resourcepack-tufreplay | feat/tufreplay-overlay-support |

별도 /Users/kgh/dev/src/impl-resourcepack-render에는 이번 조사에서 SDK 연동 소스가 발견되지 않았다. 이름만 보고 그 checkout을 수정하지 말 것. 위 관련 checkout에는 기존 변경이 있으므로 보존해야 한다.

## 2. 현재 실제 구현

### SDK: participant 등록과 wrapper

/Users/kgh/dev/src/TUFReplay-Renderer/sdk/TUFReplay.Overlay에 다음 파일이 있다.

- OverlayContracts.cs: IOverlayParticipant, IOverlayNativeServices, OverlayRenderContext.
- OverlayRuntime.cs: Register, BeginRender, IsRendering 등.
- OverlayRenderSession.cs: 참가자 시작·프레임 갱신·제외·종료.
- OverlayFrame.cs: 프레임 시각과 키 상태.
- OverlayWrappers.cs: OverlayTime, OverlayInput, OverlayEnvironment.
- RenderStopwatch.cs: 재생 시계에 연결하는 Stopwatch 대체 구현.

현재 IOverlayParticipant는 BeginRender / AdvanceFrame / SetPresentationEnabled / EndRender 구현을 요구한다. OverlayTime/OverlayInput은 등록한 native 서비스와 재생 상태를 내부에서 자동 선택한다.

이것이 현재 구조다. 아래 새 이름이나 완료 이벤트가 이미 구현되었다고 취급하지 말 것.

### JRP: 정상 입력 경로와 별도 렌더 경로

실제 파일:
- /Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs
- /Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs
- /Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.cs
- /Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/RainManager.cs

정상 경로:

```text
SkyHookManager.KeyUpdated
 → OnKeyEvent
 → ConcurrentQueue / 입력 작업 스레드
 → ProcessKeyEvent
 → label/native key 바인딩
 → Work
 → 키 표시·카운터·레이닝
```

KeyViewer.Update.cs:155 근처는 렌더 중 OnKeyEvent가 RememberNativeInput을 호출하고 반환한다. 작업 스레드에도 렌더 중 ProcessKeyEvent를 건너뛰는 분기가 있다. Updater.Update도 렌더 중 바로 반환한다.

반면 KeyViewer.Render.cs:64의 AdvanceFrame은 _renderKeys/_renderIndexes를 별도로 순회한다.

```csharp
// 현재 코드에서 발췌
Work(_renderIndexes[i], frame.IsPressed((int) key), ticks);
Updater.Refresh();
if(Settings.useRain) RainManager.Refresh();
```

즉 Work와 일부 UI 생성 함수는 이미 재사용하지만, 정상 입력 바인딩과 다른 렌더 입력·갱신 진입 경로가 존재한다. 모든 기능 본체가 통째로 복제되어 있다고 쓰면 부정확하다.

KeyViewer.Render.cs:22의 BeginRender:
- LiveView로 기존 상태를 보관.
- native 입력 재개용 상태 준비.
- 기존 루트 숨김 및 RainManager 자동 갱신 중지.
- KeyCountData.Instance, press queue, Stopwatch 등을 렌더용으로 교체.
- Render SizeObject와 RainManager 생성.
- Keys/Kps/Total 변경 후 기존 InitializeKeyViewer/InitializeFootKeyViewer 호출.
- 손·발·고스트 키의 렌더용 배열과 인덱스 구성.

:138의 EndRender는 루트, RainManager, Keys, Kps, Total, CountData, press queue, Stopwatch, 카운트 캐시, 키 상태, 활성 상태 등을 하나씩 복구한다. :172의 LiveView에도 이 필드 목록이 있다.

**사용자의 핵심 불만은 기능이 바뀔 때 별도 렌더 코드와 수동 저장·복구 목록도 같이 수정해야 하는 결합과 반복이다.** 파일명 변경이나 목록을 다른 클래스로 옮기는 것으로 해결하지 말 것.

### JRP 오버레이도 별도 수명 코드가 있다

/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.Render.cs는 LiveOverlay와 TextView/RectView/ObjectView를 통해 필드 및 UI 속성을 저장·복구한다. AdvanceFrame은 기존 UpdateTime/UpdateComboSize를 호출한다.

Main.cs:102의 OnUpdate는 렌더 중 정상 UpdateTime/UpdateComboSize를 건너뛰고, JMain.Update에 전달하는 delta를 OverlayTime.UnscaledDeltaTime으로 바꾼다.

### Impl은 JRP와 다른 상태 소유 구조다

실제 SDK 연동은 /Users/kgh/dev/src/impl-resourcepack-tufreplay에 있다.

- ImplResourcePack/Bootstrap/ModRuntime.cs: IOverlayParticipant 등록, 수명 호출 위임.
- Bootstrap/GameplayOverlay.cs: 기존 Coordinator/View/Canvas 생성과 소유.
- Bootstrap/OverlayRenderScope.cs: live 표시 보관, factory로 render 표시 생성, 종료 시 폐기 및 live 표시 복원.
- Infrastructure/Game/Status/StatusTimeTicker.cs: OverlayTime 사용.
- Presentation/Overlay/Combo/ComboAnimationDriver.cs: RenderStopwatch 사용.

이 구조를 JRP의 필드별 snapshot과 동일하다고 단정하지 말 것. 기존 GameplayOverlay/OverlayRenderScope의 생성·소유 경계를 살려 새 계약으로 옮긴다.

### Renderer는 현재 참가자의 AdvanceFrame을 직접 호출한다

/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs:
- BeginOverlays → OverlayRuntime.BeginRender.
- PrepareFrameClock으로 시뮬레이션 전에 재생 시각 준비.
- heldKeys로 OverlayFrame 구성 → overlays.AdvanceFrame.
- EnterRefresh로 추가 UI 갱신에서 시각 고정.
- ReadyForCapture는 현재 참가자 오류 대기 여부를 확인한다. 새 연결의 요청별 완료 대기가 구현된 상태는 아니다.

src/Engine/Renderer/RenderReplayDriver.cs에 IRenderOverlaySynchronization / ReadyForCapture / PrepareCapture 경계가 있다. 새 완료 계약은 기존 엔진 캡처 순서까지 읽고 여기에 연결해야 한다.

과거 JipperResourcePackReplayAdapter/GhostifyOverlayReplayAdapter/SharedOverlayInput 파일은 남아 있지만 src/TUFReplay-Renderer.csproj:21의 Compile Remove로 배포 어셈블리에서 제외되어 있다. 현재 실행 경로와 혼동하지 말 것.

### DLL 직접 배포는 이미 적용되어 있다

JRP의 csproj는 OverlaySdkPath DLL을 직접 참조하고 Private=true로 복사한다. DLL 존재 확인과 패키지 복사도 있다. Renderer README는 overlay-sdk-package로 standalone ZIP을 배포하는 방식을 설명한다.

이 방향은 유지한다. 외부 모드가 Renderer 소스를 ProjectReference하도록 되돌리지 않는다.

## 3. 확정한 새 설계

### 시간 wrapper를 제공하지 않는다

공개 이름은 **TUFReplaySession**. SDK는 렌더 상태와 재생 데이터를 제공하고, 모드 개발자가 직접 실제 값과 선택한다.

```csharp
// 새 설계의 예시이며 현재 API가 아니다.
float delta = TUFReplaySession.IsActive
    ? TUFReplaySession.DeltaTime
    : Time.unscaledDeltaTime;

double now = TUFReplaySession.IsActive
    ? TUFReplaySession.ElapsedTime
    : stopwatch.Elapsed.TotalSeconds;
```

IsActive는 렌더 세션 활성 여부. UI 시계는 영상 경과 시간, 판정·진행도는 시뮬레이션된 게임 상태를 따른다. 실제 작업 대기·타임아웃·성능 측정 시계는 유지한다. 정확한 타입·단위·비활성 시 조회 동작은 구현 시 문서화한다.

### TUFReplay 전용 입력 이벤트와 상태 조회

공개 이벤트 데이터 이름은 **TUFReplayKeyEvent**. 모드가 정상 입력과 재생 입력을 같은 기존 처리 본체에 연결한다.

```csharp
void OnSkyHookInput(SkyHookEvent input)
{
    if (TUFReplaySession.IsActive) return;
    HandleExistingInput(Convert(input));
}

void OnTUFReplayInput(TUFReplayKeyEvent input)
{
    HandleExistingInput(Convert(input));
}
```

위 함수 이름은 예시다. JRP의 기존 ProcessKeyEvent/Work, 바인딩, 스레드 경계를 읽고 실제 처리 본체를 공유한다. 이벤트 수신 즉시 모든 Unity 작업을 해도 된다고 가정하지 않는다.

polling 모드에는 같은 공급원의 눌림 상태 조회를 제공한다. 모든 모드를 SkyHook이나 이벤트 구조로 통일하지 않는다. 평소에는 실제 입력, 렌더 중에는 재생 입력을 선택하며 OR로 혼합하지 않는다.

이벤트는 키·눌림/뗌·기록 시각·동일 시각 순서를 담는 방향이다. 이벤트와 키 상태 조회의 관측 시점을 일치시키고 Down/Up의 유효 범위를 정한다. 현재 Unity KeyCode 기반 기록으로 native 전용 키나 장치 ID까지 지원한다고 주장하지 않는다.

### 특정 갱신 함수나 내부 구조를 강제하지 않는다

연결 타입은 **TUFReplayConnection**, 변수는 **tufReplayConnection**.

RefreshView/AdvanceFrame 같은 특정 함수가 모든 모드에 필수라는 설계는 취소한다. 기존 Update/LateUpdate, 이벤트 처리, 큐, 외부 갱신 함수를 유지할 수 있어야 한다. 계약은 해당 요청의 UI 갱신 완료를 확인하는 것이다.

```csharp
// 기존 갱신 경로의 끝에서 실제로 처리한 요청만 완료.
if (HasProcessedRequestedFrame)
    tufReplayConnection.CompleteFrame(processedFrameId);

// 외부 갱신 함수가 이미 있는 모드의 다른 연결 예.
tufReplayConnection.OnFrameRequested += frame =>
{
    ExistingUpdate(frame);
    tufReplayConnection.CompleteFrame(frame.Id);
};
```

세부 시그니처는 미확정이다. FrameId는 Unity frameCount와 독립된 요청 ID다. 세션·연결·요청을 묶어 과거/다른 세션/중복 완료가 캡처를 허용하지 않게 한다. 요청 없는 Update에서 현재 FrameId를 무조건 완료 처리하면 안 된다.

입력 → 기존 갱신 완료 → Canvas 반영 → 캡처 순서를 보장한다. 임의 sleep으로 완료를 추측하지 않는다. 추가 UI refresh는 시각과 입력을 고정하고 delta를 0으로 유지한다. 같은 Unity 프레임 안의 복수 요청·캐시·취소·연결 해제도 처리한다.

### 기능 구현과 수동 복구의 반복을 줄인다

기존 키 박스·레이닝·KPS·카운터·애니메이션·UI 생성과 갱신을 재사용한다. SDK/Renderer가 모드 내부 필드나 레이닝 풀을 반사로 조작하는 어댑터를 늘리지 않는다.

.Render.cs라는 파일명 자체가 문제는 아니다. 등록·입력 연결·시작 종료·완료 알림만 가진 얇은 연결 파일은 남아도 된다. 지금의 기능별 상태 목록과 별도 처리 경로가 바뀌어야 한다.

모드가 실행 상태를 소유하며, 같은 생성 경로로 렌더 상태를 만들어 폐기하고 live 상태로 돌아가는 방법을 우선 검토한다. 특정 ViewerRun 클래스나 백엔드 전면 교체를 강제한 결정은 아니다. Impl의 기존 소유 구조는 살린다. 단순히 LiveView 목록을 다른 파일로 옮기는 것으로 끝내지 않는다.

## 4. 역할과 작업 조건

| 구성요소 | 책임 |
| --- | --- |
| TUFReplay/Renderer | 기록 읽기, 게임 시뮬레이션, 재생 데이터 공급, 완료 대기, Canvas 캡처·인코딩 |
| 공용 DLL | 세션 공개, 재생 입력 이벤트·상태 조회, 연결·요청·완료, 시작 종료·오류 연결 |
| 모드 | 공급원 선택, 기존 기능/UI 갱신, 임시 상태와 저장 억제, 완료 알림 |

- 기존 Canvas 캡처와 높은 영상 FPS에서 같은 캡처 화면을 반복하는 정책을 유지한다.
- 모드에 이미지·영상 생성 기능을 요구하지 않는다. Canvas 등록/반환을 새 필수 계약으로 정한 것도 아니다.
- 프레임 사이의 짧은 입력을 기존 polling 모드가 놓친다면 렌더도 그 특성을 따른다. 이를 보정하려고 기능을 복제하지 않는다.
- SDK는 Renderer 모노레포에서 관리하고 각 모드에 DLL로 동봉한다. 별도 의존성 모드 설치는 없다.
- 동봉 DLL들이 같은 런타임 세션을 보도록 Unity Mono의 로딩·중복·버전 정책을 검증한다. static이면 무조건 공유된다고 추측하지 않는다.
- 기존 SDK 미사용 모드 호환 유지 요구는 없다. 새 계약으로 전환한다.
- 우선 대상은 JRP 키뷰어와 ImplResourcePack 오버레이. JRP 오버레이의 기존 SDK 참조도 빌드와 동작이 깨지지 않게 이동하되 범위를 무제한 늘리지 않는다.
- impl-dmnote는 절대 수정하지 않는다.
- JRP 브랜치는 feat/tufreplay-render-support를 유지한다. codex 접두사 금지.
- 렌더가 사용자 누적 카운터·프로필을 저장 변경하지 않게 한다. 완료·취소·오류 후 정상 사용으로 복귀한다.
- 오류 시 사용자에게 알려 해당 오버레이 제외 여부를 묻는다. 제외하지 않으면 렌더 취소. 자동 재시도하지 않는다.
- 실제 게임 검증은 사용자가 나중에 하기로 했다. 실행 요청을 반복하지 않고 미검증 범위를 명시한다.
- 저장소 README/AGENTS/RTK를 읽고 ./scripts/run.sh를 사용한다. 관련 없는 기존 변경을 보존한다.
- API 문서는 특정 모드 내부 구조가 아닌 공용 계약으로 작성한다.

## 5. 구현 순서와 검증

1. 최신 SDK/session과 캡처 순서, JRP 정상 입력·갱신·소유 구조, Impl scope를 다시 확인한다.
2. TUFReplaySession/TUFReplayConnection/입력/완료 계약을 구현하고 기존 wrapper·native getter 등록 의존을 새 방향에 맞춰 정리한다.
3. Renderer가 대상 요청의 모든 유효 연결 완료 및 Canvas 반영 후 캡처하도록 연결한다.
4. JRP의 실제 입력과 재생 입력을 기존 처리 본체에 연결하고 별도 바인딩·갱신·수동 복구의 반복을 정리한다. native 입력 복귀, 저장 억제, 기존 rain geometry 수정은 잃지 않는다.
5. Impl의 GameplayOverlay/OverlayRenderScope를 재사용하여 새 계약과 명시적 시간 선택으로 전환한다.
6. DLL 패키지와 공용 문서를 갱신하고 관련 자동 검증을 실행한다.

검증 대상: 세션 전환, 이벤트 순서·키 상태 일치, 실제 입력 혼입 억제, 요청별 완료·지연·중복·취소·제외, 0 delta refresh, 입력 복귀, 저장 억제, 배포 DLL 일관성. 기존 workflow에 맞춰 실행하고 과거 결과를 이번 변경의 검증처럼 보고하지 않는다.

완료 기준은 **새 기능을 모드의 기존 기능 코드에만 추가하면 렌더에서도 사용하는 구조**다. API 이름 변경이나 어댑터 파일 이동만으로 완료 처리하지 않는다.

## 6. 다른 모드 조사 참고

구현 대상 확대 지시가 아니라 공용 계약이 단일 입력 백엔드를 강제하지 않기 위한 참고다.

- JRP: SkyHook KeyUpdated → 작업 큐 → ProcessKeyEvent.
- JipperKeyViewer: KeySource.GetKey의 shim/Unity 입력 조회, frameCount 캐시. SkyHook 직접 소비 아님.
- Ghostify: KeyUpdated 큐 + 고스트 원시 이벤트 경로 + Unity 폴백.
- Quartz: SkyHook HookCallback → HookKeys → 큐, Unity/macOS 상태 조회 병용. KvExternalDriver 존재.
- Impl 오버레이: 게임 상태 조회.

다른 모드의 공개 소스는 앞선 대화에서 조사했다. 이번 재조사는 로컬 SDK/Renderer/JRP/Impl 연동 코드에 한정했고 실제 게임 실행이나 입력 주입 실험은 하지 않았다.

