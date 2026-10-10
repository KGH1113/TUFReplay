# 키뷰어·오버레이 시간 추상화 적용 조사

조사일: 2026-10-11. 대상은 스크린샷에 등장한 Quartz, JipperKeyViewer, JipperResourcePack, GhostifyOverlay, ImplResourcePack이다. 소스와 설치된 DLL을 읽어 적용 결과를 예상했다. 모드 소스 수정, 실제 게임 실행, 빌드, 성능 측정은 하지 않았다.

## 판단

**시간 추상화는 적용할 만하다. 다만 모드마다 이미 시간을 모아 둔 정도와 이펙트를 진행하는 방식이 달라, 같은 래퍼를 넣어도 남는 작업은 다르다.**

가장 자연스러운 목표는 모드의 표시·애니메이션이 사용하는 시간을 교체할 수 있게 하고, 입력 이벤트 전달·갱신 완료·임시 상태 관리를 작은 별도 경계로 두는 것이다. 시간 래퍼에 입력 워커 제어, 저장 차단, private 필드 복구까지 넣으면 책임이 다시 섞인다.

| 모드 | 확인한 시간 구조 | 시간 추상화의 예상 효과 | 별도로 남는 핵심 작업 | 전체 연동 부담 예상 |
| --- | --- | --- | --- | --- |
| Quartz 키뷰어 | 이미 `KvClock`과 `KvExternalDriver`가 있음 | 기존 추상화를 그대로 이용 가능. Rain·KPS·CSS 효과·JS 타이머가 같은 시간축을 따라갈 수 있음 | 이벤트 시각을 명시적으로 받기, 실제 입력 폴백과 포커스 처리, 임시 카운터·저장 격리 | 낮음∼보통 |
| ImplResourcePack 오버레이 | 상태 갱신 ticker와 콤보 애니메이션에 시간이 집중됨 | 콤보 애니메이션과 상태 갱신 주기를 정리하기 쉬움 | ticker와 실제 FPS 측정 분리, 게임의 음악·맵 시간 확인, 캡처 전 갱신 | 낮음∼보통 |
| GhostifyOverlay | `CurrentTicks`에 키뷰어 시간이 집중되고 입력 큐를 메인 스레드에서 소비함 | KPS와 시각 기반 Rain을 작은 변경으로 재생 시계에 연결 가능 | 정규화된 이벤트 주입, 실제 입력·포커스 간섭 제거, 임시 카운터와 `Flush` 격리 | 보통. 조사한 공개 소스 기준 |
| JipperKeyViewer | `Stopwatch`, `unscaledTime`, `unscaledDeltaTime`이 여러 partial 파일에 분산됨 | KPS·카운터 bounce·키 확대·Rain의 시간 진행을 일관되게 만들 수 있음 | 일반/FreeMake/ghost 입력 이벤트 처리, delta 누적 Rain의 짧은 입력 길이, 프로필 카운터·저장 격리 | 보통∼높음. 정확한 짧은 입력 이펙트까지 포함할 때 |
| JipperResourcePack | 키뷰어 `CurrentTicks`와 오버레이 콤보 `Stopwatch`에 집중됨 | 시간 자체의 교체는 작고 기존 KPS·시각 기반 Rain을 유지하기 좋음 | 입력 워커와 UI 전달 큐의 완료 보장, 이벤트 시각 변환, 임시 카운터·저장 격리 | 보통∼높음. 동기화 경계가 가장 큼 |

여기서 부담은 시간 래퍼의 구현량만이 아니라 정확한 입력·이펙트와 사용자 상태 보존까지 포함한 상대적 예상이다. 구현과 측정으로 검증한 수치가 아니다.

## 조사 기준과 버전

웹 소스는 아래 커밋에 고정했다. 기본 브랜치의 조사 시점 소스이며, 설치된 최신 릴리스와 같다고 가정하지 않았다.

| 대상 | 조사한 기준 |
| --- | --- |
| Quartz | [PrismMods/Quartz](https://github.com/PrismMods/Quartz/tree/00a74df2c152d7d4e89ce6860e436e73121568fa), `00a74df2c152d7d4e89ce6860e436e73121568fa` |
| JipperKeyViewer | [adofaiex/JipperKeyViewer](https://github.com/adofaiex/JipperKeyViewer/tree/a930b26059673511e8bcbc2edee0bad882ed4f19), `a930b26059673511e8bcbc2edee0bad882ed4f19` |
| JipperResourcePack | [Jongye0l/JipperResourcePack](https://github.com/Jongye0l/JipperResourcePack/tree/241c108724b77ea29e36622078768ff4fc9be681), `241c108724b77ea29e36622078768ff4fc9be681`. 설치된 1.5.3.0 DLL의 핵심 타입도 ILSpy로 확인 |
| GhostifyOverlay | [vista102/GhostifyOverlay](https://github.com/vista102/GhostifyOverlay/tree/7668817724318386f1aa4e9ed95a723f8132bad4), `7668817724318386f1aa4e9ed95a723f8132bad4`. 이 소스의 `Info.json`은 0.4.5, namespace/Id는 `DonQuixoteOverlay`, assembly는 `GhostifyOverlay`. 스크린샷의 0.4.7 DLL과의 일치는 미확인 |
| ImplResourcePack | `/Users/kgh/dev/src/impl-resourcepack` 작업 트리. HEAD `4a06efb74bf03949b1d020d9484bbd081c611b6b`에 기존 미커밋 변경이 있는 상태를 읽음 |
| TUFReplay-Renderer | `/Users/kgh/dev/src/TUFReplay-Renderer`, HEAD `8ea66faed7b316c19a65fbdc6adb9ccaaee0100a`. 기존 Unity 폰트 자산 변경을 보존 |
| 기본 작업 디렉토리 | `/Users/kgh/dev/src/tuf-replay-dev`, HEAD `8220504849a5f27f787f030db83f04cb9dc3fa89` |

JipperKeyViewer는 JipperResourcePack과 별도 프로젝트다. Ghostify는 현재 공개 소스에서 직접 확인한 구조를 기준으로 판단했으므로 이전 스크린샷의 난이도를 그대로 재사용하지 않았다. Ghostify의 버전·assembly 구분은 [프로젝트 파일](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/GhostifyOverlay.csproj#L9)과 [Info.json](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/Info.json#L2)에서 확인했다.

## 현재 렌더러와의 관계

현재 렌더러에는 이미 `OptionalModClock`이 있다. 발견한 모드 코드의 Unity `Time`, `Stopwatch`, `DateTime` 및 일부 큐 호출을 찾아 Harmony transpiler로 연결하고, 비동기 작업의 이벤트 시각과 완료를 추적한다. 조사 대상은 모드가 명시적으로 채택하는 시간 래퍼이며, 이 기존 자동 호환 계층을 새로 구현해야 한다는 주장은 아니다. [OptionalModClock](</Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OptionalModClock.cs:54>), [OverlayVideoClock](</Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OverlayVideoClock.cs:14>)

이전 스크린샷에서 설명한 `OptionalModReplaySupport`, `JipperResourcePackReplayAdapter`, `GhostifyOverlayReplayAdapter`, `OptionalOverlayPresentation`은 현재 프로젝트의 `Compile Remove`에 들어 있다. 남아 있는 파일을 현행 모드별 연동으로 취급하면 판단이 틀어진다. 현재 빌드는 공통 입력과 시간·큐 추적을 사용한다. [프로젝트 설정](</Users/kgh/dev/src/TUFReplay-Renderer/src/TUFReplay-Renderer.csproj:22>), [RecordedReplayDriver](</Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs:92>)

명시적인 시간 계약을 모드가 제공하면 해당 모드의 시간 호출 검색·치환에 대한 의존은 줄일 수 있다. 하지만 시간 래퍼만 채택한 모드에는 여전히 입력·큐·저장 호환 처리가 필요하다. 계약에 참여하는 모드를 기존 자동 치환 대상에서 제외하거나 우선순위를 정하는 것도 렌더러 측 작업으로 남는다.

## 시간 계층에 필요한 의미

| 시간의 용도 | 렌더 중 필요한 시간 |
| --- | --- |
| KPS, Rain, 키 확대, 카운터 bounce, 콤보 애니메이션 | 출력 영상의 진행 시간. 게임 pitch를 적용한 기록→출력 시간 매핑 사용 |
| 음악 시간, 맵 진행 시간, 게임 상태 | 렌더 시뮬레이션의 게임 상태·음악 위치 |
| CPU 성능 측정, 작업 완료 타임아웃, 통신·워커 종료 대기 | 실제 단조 증가 시간 |
| 실제 FPS 표시 | 의미를 별도로 결정. 렌더 처리 속도, 시뮬레이션 FPS, 기록 당시 FPS는 다른 값 |
| 사용자 설정 저장의 debounce, 파일 저장 재시도 | 기본적으로 실제 시간과 실제 저장 정책 |

공통 계층은 현재 시각과 갱신 간격을 제공하고, `Stopwatch` 형태의 래퍼가 필요하면 시작·정지·재시작·누적 경과 시간의 기존 의미를 보존해야 한다. 프레임 간격과 개별 타이머의 경과 시간을 동일한 값으로 치환할 수는 없다. 이미 실행 중인 타이머를 다른 시계로 옮길 때도 기준시각 변환 또는 별도 인스턴스가 필요하다.

이벤트의 시각과 최종 화면의 시각 역시 구별해야 한다. 1.000초 DOWN, 1.006초 UP을 처리한 뒤 1.016초 화면을 그릴 수 있어야 한다. `delta`로 진행하는 효과는 그 구간을 한 번만 진행해야 하며, 같은 시각의 UI 갱신 대기에서는 시간이 추가로 흐르면 안 된다. 현재 렌더러도 이벤트 처리와 갱신 대기에서 delta를 0으로 두는 개념을 갖고 있다. [OverlayVideoClock.Delta](</Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OverlayVideoClock.cs:17>)

## Quartz: 새 시간 계층보다 기존 계약 보완이 맞음

### 확인한 구조

`KvClock`은 실시간 `Stopwatch.GetTimestamp()`와 외부 `Func<double>` 시계를 지원한다. 공급자 전환 시 기준값과 offset을 조정한다. `KvExternalDriver.Begin`은 외부 시계와 입력 큐를 활성화하고 `End`는 복구한다. 따라서 제안한 시간 추상화가 이미 상당 부분 구현돼 있다. [KvClock](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KvInput.cs#L6), [KvExternalDriver](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KvExternalDriver.cs#L17)

Rain은 `StartTime`·`EndTime`과 현재 시각으로 위치와 길이를 계산한다. CSS 효과와 카운터 bounce도 `KvClock.Now`를 사용한다. JS 플러그인의 자체 타이머는 전달받은 시각과 due 시각을 비교하는 방식이어서, 이 경로의 타이머는 시스템 `Task.Delay` 교체가 필요하지 않다. 임의 사용자 스크립트 전체의 결정성까지 보장한다는 뜻은 아니다. [RainRenderer](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/RainRenderer.cs#L94), [갱신 코드](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KeyViewerOverlay.Update.cs#L39), [JS timer tick](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/Js/KvJsRuntime.PluginRuntime.cs#L124)

### 적용 예상

새 `ViewerTime`을 덧씌우기보다는 기존 `KvClock`을 외부 계약과 연결하는 게 깔끔하다. 주요 추가 작업은 다음과 같다.

1. `Key(key, down)`에 이벤트 시각을 명시적으로 받는 경로를 추가한다. 현재 큐의 `Ev.Time`은 큐에 넣는 순간의 `KvClock.Now`로 찍힌다. 외부 clock 값을 이벤트마다 바꾸는 것으로도 전달할 수 있겠지만, 이벤트 데이터에 시각을 넣는 계약이 처리 순서를 설명하기 쉽다. [입력 큐](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KvInput.cs#L105)
2. 외부 입력 활성화 시 실제 입력의 폴백·초기 held 동기화를 제외한다. `Push`가 실제 큐 입력을 막더라도 `KeyHeld`, macOS backfill, uncovered binding의 폴링 경로는 별도로 있다. [입력 폴백](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KeyViewerOverlay.Input.cs#L116)
3. 외부 구동의 포커스 정책을 정한다. 현재 `LateUpdate`는 포커스가 없으면 입력 처리를 중단한다. [포커스 조건](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KeyViewerOverlay.Update.cs#L121)
4. 렌더 카운터가 설정과 문서에 저장되지 않게 한다. `FlushCounts`는 표시 카운터를 원본 데이터로 옮기고 실제 저장을 요청한다. `Begin/End`가 이 상태를 자동 격리하지는 않는다. [카운터 저장](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/modules/KeyViewer/KeyViewerOverlay.Lifecycle.cs#L246)

**예상 결과:** 기존 입력 링 버퍼와 Rain/UI를 유지하면서 외부 계약을 보완할 수 있다. 시간 추상화의 포팅 비용은 가장 작다. 별도 범용 시계 계층을 중복으로 만드는 이득은 작다.

## ImplResourcePack: 시계와 FPS 측정을 분리해야 함

### 확인한 구조

상태 갱신의 `StatusTimeTicker`는 `Stopwatch.GetTimestamp()`를 읽고 0.1초 주기로 callback을 실행한다. 동일한 시각으로 `FrameRateSampler`도 구동한다. 샘플러는 최소 0.25초의 표본이 있어야 유효한 FPS를 내며, ticker는 이 결과가 없으면 상태 갱신까지 건너뛴다. [StatusTimeTicker](</Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Infrastructure/Game/Status/StatusTimeTicker.cs:40>), [FrameRateSampler](</Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Infrastructure/Game/Status/FrameRateSampler.cs:20>)

콤보 애니메이션은 별도 `ComboAnimationDriver`의 `Stopwatch`로 500ms 진행률을 구하고 기존 UI에 적용한다. 음악·맵 시간은 `StatusReader`가 `AudioSource.time`, `conductor.songposition_minusi` 등을 읽는다. 표시용 시계를 바꿔도 이 데이터 출처가 자동으로 교체되지는 않는다. [ComboAnimationDriver](</Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Presentation/Overlay/Combo/ComboAnimationDriver.cs:29>), [StatusReader](</Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Infrastructure/Game/Status/StatusReader.cs:33>)

### 적용 예상

콤보 `Stopwatch`는 래퍼로 교체하기 좋은 독립 지점이다. 상태 ticker는 단순 일괄 치환보다 **상태 갱신 일정에 쓰는 시간과 실제 FPS 표본 시간을 분리**하는 편이 자연스럽다. 유효한 FPS가 아직 없어도 시간 표시를 갱신할 수 있는지도 함께 정리해야 한다.

렌더 중 상태를 매 캡처 시점에 갱신하도록 작은 수동 갱신 진입점을 둘 수 있다. `OverlayCoordinator.RefreshTime` 등 기존 coordinator 경계가 있어 뷰를 다시 만들 필요는 없다. 실제 게임 시뮬레이션이 음악·맵 위치를 올바르게 제공하는지 확인하고, 맞지 않는다면 reader 경계에서 해결한다. [OverlayCoordinator](</Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Application/OverlayCoordinator.cs:101>), [ticker 연결](</Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Bootstrap/ModRuntime.cs:301>)

**예상 결과:** 변경 범위는 작고 역할도 명확하게 유지할 수 있다. 다만 ticker 전체의 시간을 가상화하면 FPS가 가상 갱신 주기에서 계산된다. 이것을 기록 당시 FPS라고 표시할 수는 없다. 이번 조사로 원래 FPS의 기록 데이터가 존재한다고 확인하지 않았으므로 렌더 중 FPS 의미는 별도 결정 사항이다.

이 판단은 Unity 오버레이에 대한 것이다. 작업 트리에 추가된 ImplDmNote 미러의 영상 전달이나 독립 데스크톱 앱까지 Unity 시간 래퍼로 구동할 수 있다는 의미는 아니다.

## GhostifyOverlay: 공개 소스 기준으로는 시간이 이미 잘 모여 있음

### 확인한 구조

키뷰어는 `CurrentTicks => Stopwatch.Elapsed.Ticks`를 공유한다. 입력 callback은 native 이벤트 시각과 도착 시각을 큐에 넣고, 메인 `Update`의 `PumpInput`이 이를 변환·소비한다. 따라서 이 공개 소스에는 JipperResourcePack처럼 입력 처리 전용 `ProcessKeyEvents` 워커를 새로 통제해야 하는 구조가 없다. 입력 수집 자체의 스레드는 별개다. [키뷰어](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/KeyViewerContents/KeyViewer.cs#L22), [입력 큐 소비](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/KeyViewerContents/KeyViewer.Input.cs#L184)

Rain은 `CurrentTicks - StartTime`으로 위치를 계산한다. `Update`는 입력, Rain, 키 표시, 카운터를 순서대로 갱신한다. 이 구조는 외부 구동을 붙이고 완료 시점을 설명하기 좋다. [RainManager](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/KeyViewerContents/RainManager.cs#L57), [Update](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/KeyViewerContents/KeyViewer.cs#L80)

### 적용 예상

`CurrentTicks` 또는 그 기반 stopwatch를 교체하면 KPS와 Rain 시간 대부분을 연결할 수 있다. 재생 이벤트는 이미 정규화된 시각으로 기존 이벤트 처리에 들어가게 하고, native 시각 변환기에서 다시 offset을 계산하지 않는 경계를 두는 게 좋다.

포커스·settings preview에 따른 suspended 상태, native hook 활성 여부, 실제 입력 폴백을 외부 구동과 분리해야 한다. 화면 갱신 마지막에는 `KeyCountData.Instance.Flush(CurrentTicks)`가 있으며 실제 파일 저장으로 이어지므로 임시 카운터와 저장 격리는 필수다. 시계만 교체하면 렌더가 진행되는 동안 저장 기한도 함께 진행된다. [입력 조건](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/KeyViewerContents/KeyViewer.Input.cs#L195), [카운터 저장](https://github.com/vista102/GhostifyOverlay/blob/7668817724318386f1aa4e9ed95a723f8132bad4/KeyViewerContents/KeyViewerStore.cs#L165)

Main의 설정 저장 debounce, native 입력 설정 동기화, 효과 복구 재시도에 사용하는 `unscaledTime`까지 일괄 교체하는 것은 권하지 않는다. 표시 애니메이션과 관리 작업의 시간이 구별되어야 한다.

**예상 결과:** 이 소스 기준이면 이전 스크린샷의 “높은 난이도”보다 부담이 작다. 시간 변경은 작고, 기존 메인 스레드 처리 단위를 외부에 연결하는 작업이 중심이다. 0.4.7 등 다른 배포 DLL에도 동일한 판단을 적용하려면 해당 버전을 다시 검사해야 한다.

## JipperKeyViewer: 시간 래퍼의 효과는 크지만 Rain 정확도에 추가 경계가 필요함

### 확인한 구조

표시 시간의 핵심 사용처는 다섯 파일 영역으로 정리된다. profiling·설정 저장·편집 UI의 시간은 별도다.

| 파일 영역 | 표시용 시간 |
| --- | --- |
| `Core/KeyViewer.cs` | KPS 등에 전달하는 stopwatch 경과 시간 |
| `Core/KeyViewerLayout.cs` | 키뷰어 stopwatch 생성·해제 |
| `Core/KeyViewerInput.cs` | 키 확대 coroutine의 `unscaledDeltaTime` |
| `Core/CustomLayout.cs` | FreeMake KPS의 stopwatch, 카운터 bounce의 `unscaledTime` |
| `Rain/RainSystem.cs` | Rain 이동·fade에 사용하는 `unscaledDeltaTime` |

일반 레이아웃은 현재 눌림 상태와 이전 상태를 비교하면서 카운터, KPS 큐, Rain, 확대 coroutine을 갱신한다. FreeMake에는 이미 `ApplyCustomKeyEdge`라는 작은 전이 처리 지점이 있지만 일반 키와 ghost 입력은 다른 경로다. `KeySource`의 실제 입력 혼합도 남는다. [일반 키 처리](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Core/KeyViewerInput.cs#L345), [FreeMake 처리](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Core/CustomLayout.cs#L1946), [KeySource](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Core/KeySource.cs#L88)

### 적용 시 중요한 점

시간 래퍼는 KPS, 확대, bounce, Rain 진행 속도를 일관되게 만드는 데 도움이 된다. 그러나 이 모드의 Rain은 `elapsedMs += deltaMs`로 이동을 진행하고 성장 상태에 따라 길이를 정한다. 입력 시작·종료 시각을 직접 사용하는 Quartz·Ghostify의 Rain과 다르다. [RainSystem](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Rain/RainSystem.cs#L274), [RawRain](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Rain/RawRain.cs#L85)

예를 들어 한 프레임 안의 DOWN/UP을 둘 다 전달해 카운터를 살려도, 두 이벤트 사이에 Rain의 성장 시간이 진행되지 않으면 Rain 길이는 입력의 실제 유지 시간을 표현하지 못할 수 있다. 시계를 교체하는 것만으로 이 문제가 없어지지는 않는다. 이는 소스 구조에서 도출한 예상이며 실제 시각 결과를 실행해 비교한 것은 아니다.

깔끔하게 연결하려면 다음 정도가 필요하다.

1. 일반 레이아웃의 전이 처리를 작은 함수로 추출하고, 기존 polling과 재생 이벤트가 함께 사용한다. FreeMake의 기존 전이 함수와 ghost 처리도 같은 외부 이벤트 경계에서 호출한다.
2. 이펙트 진행을 입력 이벤트 사이의 구간과 최종 화면 시각까지 진행시킬 수 있게 하거나, Rain의 시작·종료 시각을 저장해 현재 모습을 계산하게 한다. 전자는 기존 delta 계산을 재사용할 수 있고 후자는 시각 계약이 명확하지만 효과 상태의 변경 범위가 커진다. 비선형·프레임 의존 효과의 결과까지 같다고 보장하려면 별도 비교가 필요하다.
3. 전체 `Update`를 이벤트마다 반복 호출하는 방식은 피한다. 입력 선택·영상·저장·전체 Rain 진행까지 중복 실행될 수 있다.
4. 프로필 `Count`, `TotalCount`, FreeMake node count가 기존 `SaveSettings`로 저장되지 않도록 임시 상태를 제공한다. [프로필 저장](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Core/KeyViewer.cs#L1816)

`RunStage`의 profiling stopwatch는 실제 CPU 작업 시간을 재므로 그대로 둬야 한다. `AnimateKeyScale`의 coroutine 실행 순서도 시계 교체로 자동 제어되지는 않는다. 새 시각에서 확대 효과가 완료되었다는 보장은 갱신 경계에서 확인해야 한다. [profiling](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Core/KeyViewer.cs#L729), [확대 coroutine](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Core/KeyViewerInput.cs#L741)

FreeMake 영상 노드는 Unity `VideoPlayer.Prepare/Play`를 사용한다. 시간 래퍼가 디코더의 프레임 준비까지 제어하지는 않으므로 이 기능의 정확한 영상 동기화는 추가 미디어 계약이 필요하다. 기본 키·카운터·Rain 지원과 구분해 범위를 정할 수 있다. [영상 노드](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/KeyViewer/Rendering/KvVideoTextureManager.cs#L430)

**예상 결과:** 작은 시간 계층 도입 자체는 자연스럽다. 하지만 “그것만으로 정확한 키뷰어 재현”을 약속하면 부족하다. 특히 짧은 입력의 Rain 길이까지 지원할 때 이펙트 갱신 경계를 정리하는 작업이 필요하다. 현재 공개 소스의 Ghostify보다 항상 쉬운 대상이라고 볼 수는 없다.

## JipperResourcePack: 시간 교체보다 워커·UI 완료 경계가 핵심

### 확인한 구조

키뷰어 시간은 `CurrentTicks`로 모인다. `Stopwatch.ElapsedTicks`와 `TimeSpan` ticks의 주파수 차이를 처리하는 코드도 있다. Rain과 KPS는 이 값을 사용한다. 오버레이 콤보는 별도 stopwatch로 500ms 애니메이션을 계산한다. [CurrentTicks](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs#L17), [콤보 애니메이션](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/OverlayContents/Overlay.cs#L573)

native 입력 callback은 시각을 변환한 이벤트를 큐에 넣는다. 별도 스레드가 `SemaphoreSlim.Wait` 후 `ProcessKeyEvent`를 실행한다. 이 처리는 카운터와 Rain 큐를 갱신하고, 키와 텍스트는 `MainThread.Run`을 통해 UI에 전달된다. 설치된 1.5.3.0 DLL에서도 이 구조를 확인했다. [입력 워커](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs#L130), [키 UI 전달](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/KeyViewerContents/Key.cs#L31), [AsyncText](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/Async/AsyncText.cs#L15)

### 적용 예상

`CurrentTicks`와 콤보 stopwatch의 시계를 바꾸는 것 자체는 작다. 현재 이벤트 시각 변환기 `ToStopwatchTicks`는 native clock과 자체 stopwatch의 offset을 추정한다. 이미 재생 시간으로 정규화한 이벤트를 다시 이 변환기에 통과시키기보다는, 같은 처리 코어에 직접 전달하는 작은 경계가 필요하다.

연동 방식은 두 가지가 가능하다. 기존 워커를 재생에서도 사용하고 이벤트 처리와 UI 전달이 끝났다는 완료 신호를 제공하거나, 모드가 외부 구동 중에는 같은 이벤트 처리 코어를 메인 스레드에서 실행하도록 제공하는 방식이다. 후자는 평상시 워커를 유지하면서 렌더 경로를 명확하게 만들 수 있지만, 모드 시작·종료 시 진행 중 native 처리를 안전하게 정리해야 한다.

설치된 JALib의 `MainThread.Run`은 메인 스레드에서 즉시 실행하고 다른 스레드에서는 큐에 넣는다. 따라서 메인 스레드 구동의 가능성을 뒷받침하지만, 모드가 공식 진입점을 제공하고 스레드 안전성을 확인해야 한다. 이 조사에서는 JALib 타입을 ILSpy로 읽었으며 실제 게임 실행은 하지 않았다.

카운터 저장은 `Task.Run`과 `Task.Delay(1000)`을 사용하는 비동기 작업이다. 이 delay까지 가상화하면 저장 스케줄 의미가 바뀐다. 저장용 실제 시간은 유지하고 렌더 카운터 인스턴스가 저장 대상이 되지 않게 해야 한다. 이미 실행 중인 원본 저장 작업까지 고려해야 한다. [KeyCountData](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/KeyViewerContents/KeyCountData.cs#L44)

오버레이 콤보 위치는 `Task.Yield().OnCompleted`로 별도 갱신된다. stopwatch만 바꿔도 글자 크기와 위치가 같은 캡처에 반영된다는 보장이 생기지는 않는다. 위치 갱신을 같은 처리 단계에서 완료하거나 완료 경계를 명시해야 한다. [콤보 위치 갱신](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/OverlayContents/Overlay.cs#L592)

**예상 결과:** 시간 추상화로 시각 처리의 특례는 크게 줄일 수 있다. 정확한 렌더를 위해 가장 중요한 추가 작업은 입력 워커·UI 큐와 캡처 사이의 완료 계약이다. 워커의 수집 주기나 큐 구현까지 SDK가 강제로 바꿀 이유는 없다.

## 성능과 코드 품질에 대한 예상

시간 공급자 선택은 시작·종료 경계에서 처리하고, 평상시에는 기존 Unity 값과 stopwatch에 위임할 수 있다. 시간 조회마다 reflection, dictionary 검색, 새 객체 생성, lock을 넣을 필수 이유는 없다. 반면 현재 자동 호환 계층은 가상 stopwatch 상태를 별도 테이블과 잠금으로 관리하므로, 명시적 래퍼는 상태를 타이머 인스턴스에 두는 선택지를 제공한다. 이것이 실제로 더 빠르다는 측정 결과는 없다. [현재 가상 stopwatch 처리](</Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OptionalModClock.cs:172>)

모드별로 민감한 곳은 다르다. Quartz는 이미 시계를 매 이벤트 읽으므로 공급자 추가보다 시각 전달 방식과 링 버퍼 가정을 유지하는 것이 중요하다. JipperResourcePack은 워커당 처리를 유지하며 이벤트마다 UI 완료를 기다리는 구조를 만들지 않아야 한다. Ghostify·JipperKeyViewer·ImplResourcePack은 이펙트 수가 많은 프레임에서 clock 조회를 불필요하게 반복하지 않도록 주의할 수 있다. 프레임 시각 캐시는 프레임용 값에만 적용하고 고주기 입력의 실제 이벤트 시각을 같은 값으로 뭉개서는 안 된다.

깔끔한 결과의 기준은 다음과 같다.

- 모드의 KPS·Rain·애니메이션 코드는 TUFReplay 타입이나 전역 렌더 flag를 직접 알지 않는다.
- 각 모드가 이미 가진 `KvClock`·`CurrentTicks` 같은 시간 경계를 재사용한다. 모든 모드의 내부 클래스를 동일한 모양으로 만들지 않는다.
- 실제 시간으로 동작할 profiling·저장·관리 작업을 명확히 구별한다.
- 외부에는 시간, 입력 이벤트, 표시 갱신 완료, 실행 상태의 수명주기만 제공한다.
- 래퍼가 native 입력 스레드·큐·파일 저장·영상 디코딩을 함께 담당하지 않는다.

공통 라이브러리의 대상 프레임워크도 분리해야 한다. 조사한 JipperResourcePack·JipperKeyViewer는 `net481`, Ghostify는 .NET Framework 4.8, Quartz·ImplResourcePack은 `netstandard2.1`이다. Unity와 독립된 공통 계약을 하나의 DLL로 배포한다면 .NET Framework를 지원하는 대상이 필요하다. .NET Standard 2.1을 그대로 공유하는 것은 적합하지 않으며 .NET Standard 2.0 또는 소스 제공·다중 대상 빌드를 검토할 수 있다. Unity API 래퍼는 해당 모드의 런타임 adapter로 분리할 수 있다. [JipperResourcePack 프로젝트](https://github.com/Jongye0l/JipperResourcePack/blob/241c108724b77ea29e36622078768ff4fc9be681/JipperResourcePack/JipperResourcePack.csproj#L3), [JipperKeyViewer 프로젝트](https://github.com/adofaiex/JipperKeyViewer/blob/a930b26059673511e8bcbc2edee0bad882ed4f19/JipperKeyViewer/JipperKeyViewer.csproj#L4), [Quartz 프로젝트](https://github.com/PrismMods/Quartz/blob/00a74df2c152d7d4e89ce6860e436e73121568fa/Quartz/Quartz.csproj), [Microsoft 호환성 안내](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)

## 적용을 검증할 순서와 남은 한계

첫 검증은 기존 추상화가 있는 Quartz로 입력 시각·포커스·저장 격리의 공통 계약을 확인하는 것이 적합하다. 실제 stopwatch와 ticker 래퍼를 검증하는 대상으로는 ImplResourcePack이 작고 명확하다. 다음으로 조사한 Ghostify 소스에서 이벤트 처리와 시각 기반 Rain을 연결하고, JipperKeyViewer에서 delta 기반 효과까지 계약이 충분한지 확인하는 순서가 좋다. JipperResourcePack은 별도 워커의 완료 계약을 검증하는 대상으로 둔다. 이는 수정 승인이나 구현 일정이 아니라 조사에서 도출한 추천 순서다.

추후 구현을 검증할 때에는 같은 프레임 안의 짧은 DOWN/UP, 한 프레임의 여러 탭, 손·발·ghost·FreeMake 중복 binding, pitch 변화, 긴 재생, 포커스 상실, 취소·오류 후 원래 카운터와 설정 복구를 비교해야 한다. JipperKeyViewer에서는 카운터가 맞는지만 보지 말고 Rain 길이와 확대·fade 시작 시점도 확인해야 한다. 평상시 성능은 래퍼 도입 전후의 입력 지연, 프레임 CPU 시간, 할당·GC, 워커 대기를 같은 조건에서 측정해야 한다.

출력 FPS가 시뮬레이션 FPS보다 높을 때의 반복 프레임 문제는 시간 래퍼로 해결되지 않는다. 현재 렌더러는 같은 시뮬레이션 화면을 `TryRepeat`로 재사용한다. 오버레이만 출력 FPS로 진행시키려면 렌더러에 오버레이 갱신·캡처 단계를 추가하는 별도 변경이 필요하다. [캡처 반복 처리](</Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:654>)

이번 결과는 소스 분석과 JipperResourcePack/JALib DLL 검사에 근거한 적용 예상이다. 모드 소스와 라이브러리를 실제로 수정하거나 성능을 측정하지 않았으므로 수정 줄 수, 성능 차이, 전체 시각적 호환성은 확정할 수 없다. Ghostify의 다른 배포 버전, 임의 Quartz JS 플러그인, JipperKeyViewer 영상 노드의 정확한 디코더 동기화는 특히 추가 검증이 필요하다.
