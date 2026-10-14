# 공통 시간·입력 SDK 연동 실험 작업 계획

작성일: 2026-10-11, Asia/Seoul. 상태: 사용자 답변과 구현 시작 승인 반영 완료. 구현·빌드·설치 검증을 진행하며 커밋과 push는 별도 요청 전까지 하지 않는다. impl-dmnote는 변경하지 않는다.

공용 계약은 [공통 SDK 설계](overlay-runtime-design-2026-10-11.md)에 정의한다. 이 문서는 실험 대상의 내부 파일과 작업 순서를 기록하는 개발용 계획이다. 공개 SDK 사용 문서에는 특정 모드의 구조를 전제하지 않는다.

## 확정된 선택

| 항목 | 사용자 선택 |
| --- | --- |
| 적용 범위 | 오프라인 렌더링만 |
| 기존 자동 호환 | 유지하지 않고 SDK 연동으로 전환 |
| 배포 | 각 모드 package에 공용 DLL 동봉, 별도 의존성 모드 없음 |
| 저장소 | 공용 SDK를 Renderer 모노레포에 독립 프로젝트로 배치 |
| 갱신 FPS | 현재 시뮬레이션·영상 FPS 및 이미지 반복 방식 유지 |
| 첫 검증 | 실제 JipperResourcePack 키뷰어, ImplResourcePack 오버레이 |
| 공개 문서 | 모드에 독립적인 계약과 사용 안내 |
| UI 시간 | 출력 영상 경과 시간, 게임 상태는 기존 시뮬레이션에서 읽기 |
| 시작 상태 | 렌더용 키·카운터·이펙트 초기화 |
| 저장 | 실제 사용자 데이터 보존, 렌더용 임시 상태·저장 억제 |
| 참여 모드 오류 | 사용자에게 제외 후 계속 / 렌더 취소 선택 요청 |
| 오류 시 제외 거부 | 렌더 취소, 자동 재시도 없음 |
| API | 얇은 wrapper, 렌더 밖에서는 원래 값과 backend 사용 |
| 플랫폼 | macOS 실제 렌더 먼저, Windows 후속 검증 |

모노레포 범위는 공용 SDK·Renderer·검증·공용 문서를 함께 관리하는 것으로 잡는다. 녹화기 전체와 외부 모드 포크의 Git 저장소까지 한 번에 통합하는 작업은 포함하지 않는다.

## 준비된 저장소와 보존할 상태

- 기본 조사·계획 경로: `/Users/kgh/dev/src/tuf-replay-dev`.
- Renderer: `/Users/kgh/dev/src/TUFReplay-Renderer`.
- JipperResourcePack 사용자 포크: [KGH1113/JipperResourcePack](https://github.com/KGH1113/JipperResourcePack).
- 새 clone: `/Users/kgh/dev/src/jipper-resourcepack`.
- 작업 브랜치: `feat/tufreplay-render-support`.
- 시작 commit: `ac6d6410571ee7d585aedce7f5085de34f615af2`.
- origin은 사용자 포크, upstream은 `Jongye0l/JipperResourcePack`이다. upstream 병합은 하지 않았다.

ImplResourcePack은 최신 main 커밋 `5426d52`를 기준으로 `/Users/kgh/dev/src/impl-resourcepack-tufreplay`에 독립 작업 디렉토리를 준비했다. 작업 브랜치는 `feat/tufreplay-overlay-support`다. 기존 두 checkout과 impl-dmnote의 소스·IPC·mirror 코드는 보존한다.

TUFReplay 작업 디렉토리의 기존 IPC 관련 변경과 Renderer의 `ProgressFont SDF.asset` 변경을 보존한다. 이 계획의 변경으로 섞어 취급하지 않는다. 구현 시에도 관련 파일을 다시 읽고 기존 변경을 덮어쓰지 않는다.

## 기존 Renderer에서 확인한 연결 지점

| 근거 파일 | 확인한 동작과 계획 |
| --- | --- |
| [RenderJobController.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Jobs/RenderJobController.cs:391) | 활성 root `ScreenSpaceOverlay` Canvas 중 `DontDestroyOnLoad` scene에 있는 UI를 선택하고 control Canvas를 제외한다. SDK가 새 캡처 체계를 만들 필요는 없다. |
| [FrameCapture.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/FrameCapture.cs:328) | Canvas 하위에 별도 layer를 부여하고 HUD camera로 출력 texture에 합성한다. 기존 구성을 유지한다. |
| [FrameCapture.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/FrameCapture.cs:508) | 게임 화면을 복사한 뒤 Canvas 갱신과 HUD camera 렌더를 수행한다. 모드 UI 준비 완료와 제외 상태를 이 앞에서 확정한다. |
| [RendererController.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:588) | UI 반영용 Unity 프레임을 기다리는 경로가 있다. 이 구간은 snapshot을 고정하고 delta 0, 같은 갱신 번호로 처리한다. |
| [RendererController.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:654) | 영상 FPS가 높은 경우 캡처 이미지를 반복한다. 오버레이를 영상 프레임마다 추가 진행하지 않는다. |
| [RecordedReplayDriver.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs:143) | 프레임 시작과 게임 시뮬레이션 이후 cursor 소비 경계가 있다. 최종 held 상태가 확정된 뒤 SDK snapshot을 게시하고 UI를 갱신한다. |
| [RenderTimeline.cs](/Users/kgh/dev/src/TUFReplay-Renderer/src/Media/RenderTimeline.cs:23) | 기록 시간과 출력 시간을 pitch에 맞춰 변환한다. SDK는 이 출력 시간을 사용한다. |

현재 일반 모드의 `Update`가 `AfterSimulationFrame`보다 먼저 실행될 수 있으므로, wrapper를 바꾸기만 해서는 새 입력의 UI 반영 순서까지 보장되지 않는다. SDK 연동의 명시적인 갱신 경계와 기존 UI 대기 경로를 연결한다. 렌더 중 모드의 일반 Update와 명시적 갱신이 같은 상태를 이중 처리하지 않게 한다.

현재 `OptionalModClock`의 delta는 Renderer 시뮬레이션 clock의 FPS를 사용한다. 이를 영상 FPS delta 문제로 간주하지 않는다. 공통 snapshot 시계는 기존 이벤트 dispatch 중 잠깐 바뀌는 시각에 종속시키지 않는다.

## 기존 호환 코드 전환

`src/Replay`의 `OptionalModClock`, `SharedOverlayInput`의 합성 SkyHook 전달, 모드 IL 검색·시간 accessor 재작성, queue/work 추적을 통한 자동 연동을 제거 대상으로 둔다. 관련 후보는 `OverlayClockAccessor`, `OverlayMethodDiscovery`, `ManagedInstructionReader`, `OverlayWorkFence`, `OverlayQueuePlan`이다. 실제 참조를 확인해 자동 모드 호환에만 쓰이는 부분을 정리한다.

`OverlayAssemblyDiscovery`에는 캡처가 사용하는 control Canvas 판별이 있으므로 필요한 판별 기능을 별도 경계에 남긴다. 클래스 이름만 보고 통째로 삭제하지 않는다.

게임 시뮬레이션에 필요한 ADOFAI Harmony patch·ReplayHooks, 기록 event stream, timeline, 기존 Canvas 합성은 유지한다. compile 제외된 과거 모드별 reflection adapter에 새 구현을 추가하지 않는다.

자동 호환 코드의 테스트도 새 계약 검증으로 교체한다. 원본 CSV 입력 처리와 게임 렌더의 회귀 검증은 유지한다.

## JipperResourcePack 포팅

실험 기준은 위 clone의 실제 소스다. 과거 조사에서 읽은 다른 upstream commit의 줄 번호와 installed DLL이 같다고 가정하지 않는다.

| 파일 | 작업 방향 |
| --- | --- |
| [KeyViewer.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.cs:53) | 공유 Stopwatch 접근을 wrapper로 전환하고 렌더용 모델의 생성·복구 경계를 둔다. 기존 layout과 Canvas는 재사용한다. |
| [KeyViewer.Update.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs:20) | `CurrentTicks`의 TimeSpan tick 의미를 유지하면서 렌더 시계를 사용한다. 일반 플레이에서는 원래 Stopwatch 동작을 사용한다. |
| [KeyViewer.Update.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs:135) | 기존 SkyHook listener·worker가 렌더 모델에 쓰지 않도록 세션 경계에서 멈추거나 분리한다. 큐에 남은 native 입력과 시작·종료 race를 처리한다. |
| [KeyViewer.Update.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs:174) | 기존 key binding과 `Work` 처리 함수를 재사용한다. 렌더용 held 상태의 변화만 메인 스레드에서 전달한다. |
| [KeyCountData.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyCountData.cs:44) | 카운터 데이터는 임시 렌더 상태를 사용한다. 이벤트마다 호출되는 Save가 렌더 데이터를 실제 파일에 쓰지 않도록 중앙 저장 경계에서 차단한다. |
| [Overlay.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.cs:52) | 관련 Stopwatch 기반 UI 시간이 있다. 선택한 키뷰어 UI가 사용하는 경로를 확인해 wrapper로 연결하고 비동기 UI 반영 완료 경계를 확인한다. |
| [JipperResourcePack.csproj](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/JipperResourcePack.csproj) | 현재 net481 및 game 참조에 맞춰 SDK 참조·동봉을 추가한다. 기존 빌드 방식을 먼저 확인한다. |

CSV의 native key와 이 모드의 `LabelMap`·`NativeMap`을 연결할 때 플랫폼별 key identity를 보존한다. 문자 이름만 맞춰 physical key를 추측하지 않는다. 기존 변환을 재사용할 수 있는지 확인하고 미지원 입력은 명확히 보고한다.

렌더에서는 한 논리 갱신마다 등록된 키의 최종 held 상태를 비교한다. `Work`의 카운터·KPS·rain 생성 로직을 새로 만들지 않는다. 갱신 사이에 끝난 짧은 입력의 누락은 허용한다. 일반 플레이의 SkyHook 처리 경로는 기존 backend를 유지한다.

새 adapter 파일이 수명주기를 조율하고 시간·입력·저장 경계에는 작은 명시적 변경을 둔다. 파일 하나에서 사설 상태를 reflection으로 대량 조작하는 구현은 하지 않는다. worker 중지·복구를 게임 스레드의 무제한 blocking wait로 만들지 않는다.

## ImplResourcePack 포팅: 오버레이만

다음 경로는 조사 시점의 기존 checkout을 가리킨다. 현재 구현은 `/Users/kgh/dev/src/impl-resourcepack-tufreplay`의 같은 상대 경로와 새 `GameplayOverlay`·`OverlayRenderScope`에 있으며 최신 main 구조에 맞춰 다시 검증했다.

| 파일 | 작업 방향 |
| --- | --- |
| [StatusTimeTicker.cs](/Users/kgh/dev/src/impl-resourcepack-render/ImplResourcePack/Infrastructure/Game/Status/StatusTimeTicker.cs) | Stopwatch 기반 0.1초 status 갱신을 렌더 시계에서 진행하도록 전환한다. 실제 FPS sampler의 유효성이 UI 갱신을 막는 결합도 확인한다. |
| [FrameRateSampler.cs](/Users/kgh/dev/src/impl-resourcepack-render/ImplResourcePack/Infrastructure/Game/Status/FrameRateSampler.cs) | 실제 성능 측정은 native 시계로 유지한다. 렌더용 UI scheduling은 real FPS 측정 성공 여부에 의존하지 않게 분리한다. |
| [ComboAnimationDriver.cs](/Users/kgh/dev/src/impl-resourcepack-render/ImplResourcePack/Presentation/Overlay/Combo/ComboAnimationDriver.cs) | Stopwatch 기반 combo scale/layout 애니메이션을 wrapper 시계로 진행한다. |
| [OverlayCoordinator.cs](/Users/kgh/dev/src/impl-resourcepack-render/ImplResourcePack/Application/OverlayCoordinator.cs) | 기존 표시값·Canvas 갱신을 공통 snapshot 게시 이후 호출하는 진입점을 연결한다. |
| [ModRuntime.cs](/Users/kgh/dev/src/impl-resourcepack-render/ImplResourcePack/Bootstrap/ModRuntime.cs) | session 등록·ticker 전환·종료 복구를 모듈 수명주기에 연결한다. |
| [StatusReader.cs](/Users/kgh/dev/src/impl-resourcepack-render/ImplResourcePack/Infrastructure/Game/Status/StatusReader.cs) | 노래 위치·판정·콤보 등은 기존 게임 시뮬레이션에서 읽는다. UI 시계로 일괄 대체하지 않는다. |

대상은 게임 안의 Unity status/combo 오버레이다. ImplDmNote 외부 desktop 표시·mirror 경로와 KeyLimiter는 이번 포팅 범위에서 제외한다. 이미 진행 중인 bundled IPC 작업과 충돌하지 않도록 SDK 추가와 overlay 연결만 구분해서 작업한다.

## 오류 선택 UI와 제어 경로

사용자 요구대로 참여 모드 오류는 자동 제외하지 않는다. Renderer가 다음 프레임 출력을 보류하고 companion UI에 모드 이름·이유·두 선택을 보낸다. 제외를 선택하지 않으면 렌더를 취소한다.

현재 job에는 이 선택을 기다리는 상태가 없으므로 다음 경계의 확장이 필요하다.

- Renderer `RenderJob`, `RenderJobController`와 Renderer 제어 진입점: pending decision·job/session/decision ID·응답 처리·취소·정리.
- TUFReplay [render-schema.ts](/Users/kgh/dev/src/tuf-replay-dev/web/src/schemas/render/render-schema.ts), [render-model.ts](/Users/kgh/dev/src/tuf-replay-dev/web/src/models/render/render-model.ts): 대기 상태와 선택 데이터.
- [render-api.ts](/Users/kgh/dev/src/tuf-replay-dev/web/src/api/render/render-api.ts), [create-render-api.ts](/Users/kgh/dev/src/tuf-replay-dev/web/src/api/render/create-render-api.ts), [use-render-control.ts](/Users/kgh/dev/src/tuf-replay-dev/web/src/hooks/render/use-render-control.ts): 선택 전달·응답 중복 방지·상태 반영.
- [render-dialog.tsx](/Users/kgh/dev/src/tuf-replay-dev/web/src/components/render/render-dialog.tsx)와 ko/en 번역: 상황과 선택 결과를 설명하는 오류 선택 화면.

상태 전달에 필요한 TUFReplay 서버·IPC command 경로도 현재 구조를 조사해 연결한다. 구현 전에 최신 AdofaiIpc 문서를 읽고 기존 사용자의 IPC 변경을 재확인한다. UI 작업에는 저장소의 shadcn 구성과 apple-design 지침을 적용한다.

결정 대기 중 시간·입력은 고정하고 인코더에 미완성 프레임을 보내지 않는다. 제외 시 실패한 모드의 render contribution을 비활성화하고 남은 UI를 캡처한다. 모드별 SDK 경계에서 식별할 수 없는 Unity 전체 오류는 기존 job 실패로 처리한다.

## 구현 순서와 검증

1. **배포 계약 확인:** 실제 UMM loader와 Mono에서 동봉 DLL의 assembly identity·공통 공급자 공유·로드 순서를 먼저 검증한다. 현재 loader에는 여러 assembly load 경로가 확인됐으므로 파일명만으로 공유를 단정하지 않는다.
2. **공용 SDK:** Renderer 모노레포에 독립 프로젝트, native wrapper, session별 virtual watch, immutable frame snapshot과 수명주기 계약을 구현한다. 실제 빌드에서 Unity 참조가 없는 netstandard2.0으로 확정했으며 소비 모드가 native API를 등록한다.
3. **Renderer 공급자:** timeline/cursor 이후 held 상태 게시, 모드 갱신 완료와 기존 Canvas 캡처를 연결한다. 자동 호환 경로는 참조를 확인해 제거한다.
4. **첫 실제 포팅:** JipperResourcePack에 wrapper·session adapter·임시 카운터·native worker 전환을 붙인다. 실제 렌더로 계약의 부족한 부분을 수정한다.
5. **두 번째 포팅:** ImplResourcePack의 Unity 오버레이를 같은 SDK에 연결한다. 키뷰어 전용 계약이 되지 않았는지 확인한다.
6. **오류 제어:** companion UI와 job 제어를 연결하고 제외 후 계속 / 취소 / 중복·늦은 응답을 검증한다.
7. **실제 검증과 문서:** macOS 게임에서 두 모드 동시·개별 렌더를 확인하고, 공용 설치·포팅 문서와 build/package script를 정리한다. Windows 검증 항목은 남겨 두고 결과를 구분해 보고한다.

구현 시작 후 빌드·패키징·check는 각 저장소의 기존 진입점을 확인해 사용한다. TUFReplay와 Renderer는 `./scripts/run.sh`를 사용한다. Jipper에 공통 작업 진입점이 없다면 기존 build 조건을 유지하는 재현 가능한 script를 준비한다.

검증은 실제 실패 가능성이 있는 경계에 집중한다.

| 검증 | 확인할 결과 |
| --- | --- |
| Renderer 없음 / 렌더 전후 일반 플레이 | native 입력 backend와 Stopwatch 동작 복구 |
| 느린 렌더 / 빠른 렌더 | 동일 출력 시간에서 동일 UI 시간·키 상태 |
| 짧은 탭 / 여러 키 / 홀드 | 프레임 최종 상태 정책과 모드 기존 처리 유지 |
| UI 대기 / 높은 영상 FPS | 중복 카운트·KPS·animation 진행 없음 |
| 반복 job / 시작 실패 / 취소 | 상태와 listener 누수, 남은 render flag 없음 |
| 사용자 파일 전후 비교 | 렌더로 누적 카운터·프로필 변경 없음 |
| 두 모드 동시 / 로드 순서 변경 | SDK 공급자와 session 하나를 공유 |
| 참여 모드 오류 | 선택 대기, 제외 후 캡처, 취소 복구 |
| 비호환 SDK DLL | 명확한 진단과 정상 정리 |
| native backend 부담 | getter 할당·worker 전환·게임 프레임 부담 확인 |

## 구현과 검증에서 확인할 경계

추가 사용자 선택은 현재 없다. 아래 항목은 코드와 실제 런타임에서 확인한다.

- 동봉 DLL의 실제 UMM load/resolve 경로와 ABI 충돌 처리.
- Jipper native key mapping과 입력 worker의 안전한 전환·복구.
- 각 UI의 갱신 진입점, focus/editor gate, 비동기 완료 처리.
- 모드 제외 시 capture가 UI를 다시 활성화하지 않는 수명주기 처리.
- Impl overlay ticker와 real FPS sampler의 실제 결합.
- 새 대기 상태를 전달할 최신 서버·IPC command 경로.

사용자가 “작업 시작해줘”로 구현을 승인했다. 이 계획은 구현 기준이며 실제 완료 범위와 남은 검증은 작업 결과 문서에 기록한다.
