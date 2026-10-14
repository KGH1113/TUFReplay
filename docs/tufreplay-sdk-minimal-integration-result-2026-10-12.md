# TUFReplay SDK 최소 변경 연동 결과

## 2026-10-14 후속 수정과 현재 검증

추가 요청으로 SDK의 소스·문서·독립 배포물을 MIT로 변경했다. 원문은 `/Users/kgh/dev/src/TUFReplay-Renderer/sdk/LICENSE.md`이며 SDK 프로젝트에 `PackageLicenseExpression=MIT`를 선언했다. Renderer 본체의 GPL과 각 모드 본체의 라이선스는 유지했다. SDK ZIP에서 GPL linking exception을 제거하고, JRP·Impl의 `libs/LICENSE.md`도 동일 MIT 원문으로 교체했다. JRP 출력에는 `TUFReplay.SDK-LICENSE.md`, Impl ZIP에는 `Notices/TUFReplay.SDK-LICENSE.md`, Renderer ZIP에는 `sdk/LICENSE.md`를 동봉한다. 기존 패키징 명령과 JRP 빌드가 통과했고 ZIP·DLL 출력의 라이선스 원문 일치와 SDK ZIP의 예외 문서 제거를 확인했다. SDK DLL의 내용과 SHA-256은 변경되지 않았다.

이 절은 2026-10-14의 실제 작업 디렉토리를 기준으로 한다. 아래 2026-10-12 기록의 임시 UI 생성·snapshot 설명과 당시 검증 결과는 현재 구조를 설명하지 않는다.

- JRP는 기존 키뷰어·Canvas·레이닝·오버레이 객체를 그대로 사용한다. 별도 ViewerState나 렌더용 UI를 만들지 않는다. 키 카운터 하나만 임시 `KeyCountData`로 분리해 기존 `Work`와 화면 조회가 사용하도록 했다. native singleton을 교체하지 않으므로 렌더 전에 시작한 비동기 저장도 원래 카운터를 읽는다. 종료 시 임시 카운터를 버리고 기존 키의 숫자만 native 값으로 돌린다.
- 재생 이벤트는 저장을 예약하지 않는다. `KeyCountData.Save`도 active 세션과 singleton이 아닌 객체를 차단한다. 종료 후 키 상태 복귀는 기존 `Work`를 카운트 없이 호출하므로 추가 카운트·KPS·저장을 발생시키지 않는다.
- `PlayCount.PlayData`의 attempt 추가·삭제와 best 변경을 렌더 중 차단했다. 처음 보는 맵을 조회해도 실제 기록 사전에 넣지 않고 `Save`도 차단한다. Overlay의 재생 판은 저장 대기 progress를 만들지 않으며, 세션 종료 뒤 `Hide`가 이어져도 정상 attempt를 차감하지 않는다.
- 공용 이름은 namespace·assembly·DLL·프로젝트 경로 모두 `TUFReplay.SDK`로 변경했다. Renderer와 JRP·Impl의 프로젝트 참조, 기존 빌드·배포 스크립트, 환경 변수 예제, README와 XML 문서를 함께 갱신했다. 외부 모드는 계속 고정 DLL을 직접 참조하고 동봉한다.
- 각 파일의 기존 들여쓰기·괄호·catch·cast·XML 스타일을 따랐다. JRP에는 별도 formatter나 빌드 스크립트를 추가하지 않았다. Installer 파일 41개를 HEAD와 동일하게 복구하고 솔루션의 Installer 등록과 네 가지 구성 항목을 되살렸다. 솔루션의 나머지 기존 수정은 보존했다.
- 기존 `Rain.SetParent(..., false)` geometry 수정, Renderer의 정확한 요청 완료 대기, 실제 UI 반영 뒤 완료 알림, 입력 큐·worker·native 복귀 경계는 유지했다. KPS를 숨기는 레이아웃에서도 updater를 유지해 rain 갱신과 완료 알림을 수행한다. impl-dmnote 소스와 설치된 게임 파일은 변경하지 않았다.

검증 결과:

- `TUFReplay-Renderer`: `./scripts/run.sh overlay-sdk-check`의 24개 검사와 Mono DLL 공유 양방향 검사 통과. `mod-check`의 빌드·Unity/Mono·기존 자동 검사 통과. `overlay-sdk-package`와 `package`도 통과했다.
- `ImplResourcePack`: `./scripts/run.sh mod-check`의 기존 16개 검사와 오버레이 6개 검사 통과. 빌드 경고·오류 0개. 기존 `package`로 새 이름의 SDK를 동봉한 배포물을 생성했다.
- `JipperResourcePack`: 삭제한 로컬 보조 파일을 복원하거나 새로 만들지 않고 기존 csproj에 게임·모드·선택 의존성 경로를 인자로 전달해 Release 빌드했다. 게임 설치·배포 복사 경로는 비웠다. 본체와 VersionSafe 버킷 빌드 성공, 오류 0개. 최초 전체 빌드에는 macOS에 없는 기존 참조 DLL과 기존 obsolete API 경고 32개, 최종 증분 빌드에는 참조 경고 31개가 있었다. 경고를 숨기거나 가짜 DLL로 해결하지 않았다. Installer의 Windows 빌드는 검증하지 않았다.
- 저장소 밖 임시 Mono 검사에서 새로 빌드한 JRP의 실제 `BeginRender`, `Work`, `EndRender`, `ResumeNativeInput`, 카운터·PlayData 저장/변경 메서드를 호출했다. native singleton 유지, native 누적 값 보존, 렌더 숫자 증가, 저장 작업 미예약, 기존 attempt·best 보존, 맵 사전 미변경, 종료 후 임시 카운터 저장 차단 등 12개 검사를 통과했다. Unity UI와 게임 자체를 실행한 검사는 아니다. 임시 검사 코드는 저장소에 추가하지 않았다.
- SDK 배포 DLL과 Renderer·JRP·Impl의 소비 DLL SHA-256이 모두 `69cb1bff41713d891c2f2dbb477a9c241205b6370e41cbe53bf07dd84249c672`로 동일하다. 관련 저장소의 `git diff --check` 통과. 실제 게임 검증은 예정대로 나중에 수행한다.

주요 파일:

- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyCountData.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/PlayCount.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.cs`
- `/Users/kgh/dev/src/TUFReplay-Renderer/sdk/TUFReplay.SDK/TUFReplay.SDK.csproj`
- `/Users/kgh/dev/src/TUFReplay-Renderer/sdk/README.md`
- `/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.SDK-2.0.0.zip`
- `/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay-Renderer.zip`
- `/Users/kgh/dev/src/impl-resourcepack-tufreplay/build/ImplResourcePack.zip`

## 2026-10-12 당시 구현 기록

작성일: 2026-10-12

아래는 당시 SDK v2 리팩터링을 줄인 작업의 기록이다. 이후 추가 변경이 있으므로 현재 구조와 검증은 위 2026-10-14 절을 참고한다.

## 현재 구조

### JipperResourcePack 키뷰어

- `ViewerState.cs`를 제거했다. `Keys`, `Kps`, `Total`, `RainManager`, `Stopwatch`, 키 상태, KPS 큐, 바인딩, 레이아웃 캐시는 기존 `KeyViewer` 필드로 돌아왔다.
- 실제 Stopwatch 객체와 기존 native 조회의 빠른 경로를 유지했다. `CurrentTicks`에서 렌더 중에는 SDK의 영상 경과 시간을 같은 TimeSpan tick 단위로 읽고, 평소에는 기존 Stopwatch를 읽는다.
- SkyHook 실제 입력은 렌더 중 기존 처리 본체에 전달하지 않는다. 복귀할 때 필요한 native held 상태와 원래 이벤트 시각만 기억한다.
- SDK 재생 이벤트는 같은 FIFO, worker, label 바인딩, `ProcessKeyEvent → Work`로 전달한다. 키 박스·손/발 카운트·ghost/normal rain·KPS의 별도 렌더 기능 구현은 없다.
- 입력 뒤에 넣은 요청 barrier를 worker가 통과해도 바로 완료하지 않는다. 기존 Unity updater가 키 표시·TMP 텍스트·KPS·rain을 적용한 뒤 정확한 요청을 완료한다. 캡처 전 실제 Unity Canvas 반영은 Renderer의 기존 경계가 담당한다.
- 임시 키/rain UI는 기존 초기화 함수로 만들고 같은 Canvas에 둔다. 기존 `OnEnable`의 UI 생성 부분만 작은 `InitializeViewerPresentation` 함수로 공유했다. 새로운 입력 backend나 실행 상태 소유 계층은 없다.
- `NativePresentation`은 실제 바뀌는 UI 참조, held 배열, KPS 큐만 보관하는 연동 전용 snapshot이다. 설정·레이아웃 캐시·입력 바인딩·native 시계는 별도 상태 클래스로 옮기지 않는다.
- 렌더 카운터는 별도의 비영속 `KeyCountData`다. 디스크에 저장하는 singleton을 교체하지 않는다. 완료·취소·시작 실패 후 native UI와 누적 값을 복구하고, held 표시만 조정해 복귀 때문에 새 count/KPS/rain이 생기지 않게 한다.
- 기존 `Rain.SetParent(..., false)` geometry 수정과 pool 정리는 유지했다.

주요 소스:

- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs`

### JipperResourcePack 오버레이

- 같은 `Overlay`/`JOverlay` 인스턴스, Canvas, text/RectTransform을 사용한다. 임시 Overlay 생성, 전체 객체 교체, `_renderOwner`, 파생 클래스의 별도 render factory를 제거했다.
- `Status`, `Bpm`, `Judgement`, `Combo`, `TimingScale`, `Attempt`의 static 필드를 원래 형태로 복구했다. 이 필드를 Overlay 소유 속성으로 바꾸는 연동용 변경은 제거했다.
- 기존 game hook과 `UpdateTime`/`UpdateComboSize`를 그대로 쓴다. 콤보 애니메이션의 시간만 SDK/native 중 명시적으로 선택한다.
- 기존 `VersionSafe.RunAfter` 경계에서 요청한 표시를 갱신하고 완료한다. 해당 callback은 요청 token과 connection을 보관하므로 취소된 이전 요청이 다음 세션을 갱신할 수 없다.
- UI를 제자리에서 갱신하므로 세션 시작 때 기존 표시 속성 및 진행 중이던 native play의 저장 문맥을 잠시 보관한다. 종료 시 같은 객체에 되돌리고 표시 캐시를 무효화한다. 전체 모드 필드 이동이나 새 UI graph 생성은 없다.
- PlayCount의 렌더 임시 데이터와 저장 억제는 유지한다. 제외 시 같은 UI를 숨기며, 정상 사용 화면은 세션 종료 시 복구한다.

주요 소스:

- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.cs`
- `/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.Render.cs`

### ImplResourcePack 오버레이

- `GameplayOverlay`와 별도 객체를 만드는 `OverlayRenderScope`를 제거했다.
- 기존 `ModRuntime`이 같은 Canvas, material, feature, coordinator, ticker를 만들고 소유하는 구조를 복구했다. 요청 완료를 위해 기존 ComboView 참조를 보관하는 작은 연결점만 추가했다.
- `ModRuntime.Render.cs`는 SDK 등록·시작/종료·제외·완료를 맡는다. 요청마다 기존 ticker와 combo view를 적용한 뒤 완료한다. 시간 선택 분기와 기존 game-state reader는 유지한다.
- native 콤보 값 하나와 원래 표시 여부만 임시 보관한다. 기존 Hide/Show 초기화로 렌더 콤보를 시작하고, 종료 뒤 같은 feature에 native 값을 복구한다. 비영속 표시만 다루므로 모드 프로필을 복제하거나 저장 정책을 바꾸지 않는다.
- native 시계를 읽는 복귀는 SDK가 비활성화된 뒤 기존 Update에서 진행한다. 정상 Update 전 연속 렌더가 시작되면 최초 native 복귀 정보를 유지한다. 종료 직후 정상 게임의 Show/Hide가 오면 그 새 상태가 지연 복귀보다 우선한다.
- 제외는 기존 coordinator의 feature UI를 숨기고 overlay hook을 중단한다. 같은 Canvas의 다른 기능을 전체 Canvas 교체/차단으로 처리하지 않는다.

주요 소스:

- `/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/ModRuntime.cs`
- `/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/ModRuntime.Render.cs`
- `/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Application/Features/ComboFeature.cs`

## SDK와 Renderer

SDK의 런타임 계약과 Renderer의 캡처 순서는 변경하지 않았다. `TUFReplaySession`, 입력 이벤트/조회, `TUFReplayConnection`의 요청 token, 오류 제외/취소, 0-delta refresh, 요청별 완료 대기는 그대로다. 기존 영상 FPS가 더 높을 때 같은 캡처 화면을 반복하는 정책도 유지한다.

공용 안내 문서는 기존 모드 구조를 유지하고 필요한 시간·입력 경계만 연결하도록 수정했다. 별도 ViewerState나 Overlay 객체 교체를 필수로 요구하지 않는다.

- 공용 계약: `/Users/kgh/dev/src/TUFReplay-Renderer/sdk/README.md`
- SDK 배포물: `/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.Overlay-2.0.0.zip`
- SDK DLL SHA-256: `fadf1491f0018481c6774050e6d9f42fb039eec650cf215b07d87eccf4e841fc`

두 외부 모드는 이 standalone ZIP에서 검증한 DLL과 XML을 `libs/`에 고정해 직접 참조한다. Renderer 소스 ProjectReference나 별도 SDK 의존성 모드 설치는 요구하지 않는다. Renderer 출력·SDK 출력·두 모드의 libs 및 package stage의 DLL 체크섬 일관성을 확인했다.

## 자동 검증

모두 각 저장소의 `./scripts/run.sh`를 사용했다.

| 대상 | 명령/검증 | 결과 |
| --- | --- | --- |
| 공용 SDK | `overlay-sdk-check` | 24 그룹 통과; Mono의 forward/reverse DLL 로딩 공유 검사 통과 |
| Renderer | `mod-check` | 빌드, Unity/Mono compatibility, parser/timeline/media, FFmpeg, engine capture/completion, 기존 bridge, 임시 설치/패키지 검증 통과; 실제 설치 단계 생략 |
| JRP | `check` | 기존 rain geometry 회귀 검사 + production 소스를 연결한 키뷰어 3개/오버레이 3개 연동 시나리오 통과 |
| JRP | `build`, `package` | 본체와 VersionSafe 4개 bucket 빌드; 경고/오류 0개 |
| Impl | `mod-check` | 기존 16개 검사 + production 연결/ComboFeature 소스를 연결한 새 연동 5개 시나리오 통과; 빌드 경고/오류 0개 |
| 배포 | SDK/Renderer/Impl package 및 JRP 배포용 디렉토리 | 생성 및 SDK DLL 일관성 확인 |
| 소스/스크립트 | 관련 저장소 `git diff --check`, 변경 shell의 `bash -n` | 통과 |

새 연동 검사는 UI 객체 재사용, 기존 큐/worker와 실제 UI 반영 뒤 완료, 기록 이벤트 시각에 따른 rain, native 입력 혼입 차단, 저장 억제, native 복귀, 연속 세션, 취소된 지연 callback, 시작/UI 오류 및 제외를 확인한다. Unity/SkyHook/game-facing 경계는 작은 stub으로 대체한다. 실제 게임에서 캡처한 픽셀을 검증한 것으로 해석하면 안 된다.

JRP 전체 빌드는 로컬에 보관된 optional KeyboardChatterBlocker DLL을 compile-time 참조로 사용했다. 그 모드를 설치하거나 동봉하지 않았다.

## 배포 파일과 보존 범위

- JRP 배포용 덮어쓰기 파일: `/Users/kgh/dev/src/jipper-resourcepack/artifacts/render-support/JipperResourcePack`
- Impl ZIP: `/Users/kgh/dev/src/impl-resourcepack-tufreplay/build/ImplResourcePack.zip`
- Renderer ZIP: `/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay-Renderer.zip`
- SDK ZIP: `/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.Overlay-2.0.0.zip`

JRP 파일들은 기존 완전한 모드 패키지의 bootstrap·bundle·font·localization을 유지하며 합쳐 사용하는 payload다. 이번 작업은 설치, 게임 실행, 커밋, push 또는 PR 생성을 하지 않았다.

JRP 브랜치는 `feat/tufreplay-render-support`, Impl 브랜치는 `feat/tufreplay-overlay-support`를 유지했다. 기존 Installer 삭제, solution/.gitignore 변경, Renderer font asset 변경, 웹 오류 처리 변경 등은 보존했다. `impl-dmnote` 앱 소스는 수정하지 않았다.

실제 ADOFAI 렌더 검증은 사용자가 요청한 대로 보류했다. 실제 레이닝 픽셀, Unity 전체 모드 로딩 환경, Windows 실행 및 성능 영향은 아직 확인하지 않았다. 자동 검증 결과를 그 확인의 대체로 보고하지 않는다.
