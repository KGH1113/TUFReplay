# 키뷰어·오버레이 공통 시간·입력 계층: 구현 계획과 확인 질문

작성일: 2026-10-11, Asia/Seoul. 상태: 최초 기획안과 질문의 이력. 소스 코드는 변경하지 않았다.

> 이 문서의 사용자 선택은 모두 답변됐다. 최신 계약은 [공통 SDK 설계](overlay-runtime-design-2026-10-11.md), 선택 결과와 실제 연동 작업은 [실험 작업 계획](overlay-runtime-integration-workplan-2026-10-11.md)을 따른다. 아래 추천과 질문은 최초 논의의 기록이며 미결 질문이 아니다. 오류 난 모드를 제외하지 않으면 렌더를 취소하는 것으로 확정했다.

## 1. 이번 계획의 기준

목표는 렌더링 시점의 시간과 눌린 키 상태를 모드에 공급하고, 모드가 기존 방식으로 갱신한 Unity UI를 현재 렌더러로 캡처하는 것이다. 프레임 사이에 눌렸다 떼어진 키를 모드가 표시하지 못해도 그 결과를 그대로 따른다.

사용자와 이미 정리한 사항은 다시 질문하지 않는다.

| 항목 | 정리된 방향 |
| --- | --- |
| 입력 | CSV의 입력을 해당 시각까지 반영한 현재 키 상태를 공급한다. |
| 짧은 입력 | 모드의 기존 표현 능력과 갱신 방식에 따른다. |
| UI·이펙트 | 기존 모드의 로직을 사용한다. 구체적인 연동은 모드 개발자가 담당한다. |
| 캡처 | 현재 Canvas 수집과 HUD 합성 경로를 사용한다. |
| 코드 품질 | 시간·입력 접근을 명확한 경계에 모으고, 모드별 내부 필드를 렌더러가 조작하는 의존성을 줄인다. |
| 기본 작업 디렉토리 | `/Users/kgh/dev/src/tuf-replay-dev` |

첫 버전의 기본안은 공통 런타임·SDK, 렌더러의 공급자 연결, 연동 문서와 작은 검증용 모드를 산출물로 잡는다. 실제 지원 모드의 포팅과 최초 완료 기준은 아래 질문에서 확정한다.

## 2. 코드에서 확인한 사실

이번 조사 시점의 TUFReplay 개발 worktree HEAD는 `2ce14815a623ee1ff329c507d27e249a1a39b91b`, Renderer HEAD는 `8ea66faed7b316c19a65fbdc6adb9ccaaee0100a`다. Renderer에는 기존 `ProgressFont SDF.asset` 수정이 있고, 이를 유지한다.

| 확인 사항 | 코드 근거 | 계획에 주는 의미 |
| --- | --- | --- |
| 기능을 구현할 주요 코드는 별도 Renderer 저장소에 있다. Renderer는 TUFReplay assembly를 참조하지 않는다. | [Renderer README](/Users/kgh/dev/src/TUFReplay-Renderer/README.md:3) | 기록기·CSV 계약을 유지하고 Renderer에 공급자 연결을 넣는 것으로 시작할 수 있다. |
| 입력 CSV는 `timeUs,key,down,sequence`이고, 이미 원래 플랫폼의 native code를 Unity 키 이름으로 변환한다. | [CSV 작성](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Replay/Export/RenderBundleCsv.cs:14), [키 이름 변환](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Shared/NativeInput/NativeInputKeyCodeMapper.cs:198) | 공통 계층이 OS 입력을 다시 해석하거나 native hook을 새로 만들 필요가 없다. |
| 입력 parser는 시간과 동일 시각의 sequence 순서를 검증한다. Cursor는 목표 시각 이하의 입력을 순서대로 소비한다. | [Parser](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordingCsvReader.cs:19), [Cursor](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/ReplayEventCursor.cs:24) | 이 Cursor에서 현재 키 상태를 누적해 공급하면 된다. 입력마다 CSV를 다시 검색하지 않는다. |
| Driver에는 held 상태와 recording/output 시간 변환이 이미 있다. | [Driver 입력 처리](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs:230), [시간 변환](/Users/kgh/dev/src/TUFReplay-Renderer/src/Media/RenderTimeline.cs:23) | 녹화 pitch·카운트다운·종료 후 시간 처리는 기존 timeline을 재사용한다. |
| 현재 shared input은 held 상태 외에 각 press/release를 SkyHook hub에 전송한다. 한 갱신에서 down과 up이 모두 기록될 수도 있다. | [SharedOverlayInput](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/SharedOverlayInput.cs:54) | 현재 이벤트 경로와 새 상태 조회 계약의 의미가 다르다. 협력 모드에는 상태를 공급하고, 기존 경로의 존속은 Q2에서 결정한다. |
| 입력과 판정 적용은 `AfterSimulationFrame`에서 일어나고, 이는 Renderer의 `LateUpdate`에서 호출된다. | [Replay 갱신](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs:150), [LateUpdate](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:1185) | 키 상태를 공개하는 시점과 다음 UI 갱신을 맞춰야 한다. SDK를 연결하는 것만으로 같은 프레임의 모든 모드 `Update`보다 먼저 입력이 전달된다고 가정할 수 없다. |
| 캡처 전에 추가 UI 갱신 프레임과 기존 managed work 완료를 기다린다. | [갱신 대기](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:588) | 기존 흐름에서 최신 상태를 읽도록 연결한다. 협력 모드는 정규 Unity 갱신 흐름에서 UI 반영을 마치도록 연동한다. |
| 모드 Canvas 수집, 전용 레이어·카메라 연결, UI rebuild, 최종 합성이 이미 있다. | [수집](/Users/kgh/dev/src/TUFReplay-Renderer/src/Jobs/RenderJobController.cs:391), [HUD 카메라](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/FrameCapture.cs:328), [최종 합성](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/FrameCapture.cs:508) | 새 SDK에 Canvas 반환·픽셀 전달·캡처 완료 API를 추가할 필요가 없다. 현재 Canvas 탐지 조건을 연동 문서에 명시하면 된다. |
| 게임 시뮬레이션 FPS와 영상 FPS는 별개다. 영상 FPS가 높으면 같은 캡처 화면을 반복한다. | [RenderClock](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RenderClock.cs:7), [루프](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:563), [프레임 반복](/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs:654) | 영상 프레임마다 키뷰어를 별도로 갱신하려면 루프와 캡처 반복 정책 변경이 필요하다. Q5에서 선택한다. |
| 현재 자동 계층은 발견한 모드의 시간·입력·queue 호출을 Harmony로 바꾼다. 패치는 렌더 사이에도 유지된다. | [자동 연결](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OptionalModClock.cs:54), [호출 탐색](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OverlayMethodDiscovery.cs:20), [설명](/Users/kgh/dev/src/TUFReplay-Renderer/README.md:29) | 협력 모드의 새 계층과 기존 자동 패치가 중복 적용되지 않게 해야 한다. 새 API를 직접 이용하는 모드를 구분하는 등록 절차가 필요하다. |
| 과거 Jipper·Ghostify 개별 어댑터는 현재 shipping assembly에서 제외된다. | [프로젝트 파일](/Users/kgh/dev/src/TUFReplay-Renderer/src/TUFReplay-Renderer.csproj:22) | 실제 연결 대상은 현재 shared runtime이다. 과거 어댑터에 기능을 추가하는 계획으로 잡지 않는다. |

설치된 게임의 `SkyHook.Unity.dll`을 ILSpy로 확인했다. `SkyHook.SkyHookKeyMapper`에는 `Mouse0`부터 `Mouse4`까지의 매핑이 있다. 공통 입력 계층도 기존 CSV가 표현하는 키와 버튼을 대상으로 하고, 새로운 장치 포맷을 추가하는 작업은 별도 범위로 둔다.

현재 캡처 대상은 enabled root Canvas, `ScreenSpaceOverlay`, `DontDestroyOnLoad` 조건에 맞아야 한다. 정규 UI 갱신이 끝나면 layout/mesh rebuild와 카메라 합성은 렌더러가 처리한다. 모드 자체의 임의 worker·비디오 decoder 완료까지 `Canvas.ForceUpdateCanvases()`가 보장하는 것은 아니다. 해당 부분은 모드의 연동 책임에 포함한다.

## 3. 모드 조사 결과를 이번 범위에 적용하면

이 세션에서 앞서 확인한 소스·DLL의 근거는 [시간 추상화 조사 보고서](/Users/kgh/dev/src/tuf-replay-dev/docs/overlay-time-abstraction-investigation-2026-10-11.md)에 있다. 그 보고서에서 다룬 짧은 탭의 정확한 이벤트 재현과 이펙트 보정은 이번 기본안의 요구 사항에서 제외된다.

| 모드 | 이번 SDK에 연결할 주요 경계 | 모드 개발자가 정리할 부분 |
| --- | --- | --- |
| Quartz | 이미 존재하는 `KvClock`과 외부 입력 드라이버 | 기존 clock을 공통 공급자에 연결하고 native fallback·focus·세션 상태 정책을 정한다. 새 시간 구조를 중복으로 만들 필요가 없다. |
| JipperKeyViewer | `KeySource`, KPS Stopwatch, 키 이펙트와 Rain의 delta time | 일반 layout과 FreeMake에서 공급받은 상태·시간을 읽게 한다. 짧은 입력 표현은 기존 모드 동작에 따른다. |
| JipperResourcePack | KeyViewer Stopwatch, 입력 queue 진입점, combo 애니메이션 시계 | 외부 상태와 native 입력의 선택, worker에서 UI에 전달하는 경로, render 중 저장 정책을 정리한다. |
| GhostifyOverlay | `CurrentTicks`, 입력 pump, Rain 갱신 | 외부 입력 활성화 동안 native fallback을 제외하고 세션 상태·카운터 저장 정책을 정한다. |
| ImplResourcePack | status ticker와 combo 애니메이션 | 모드가 읽는 게임 상태는 기존 시뮬레이션에서 얻고, UI 시간 갱신을 공통 clock에 연결한다. 실제 성능 측정 시계는 따로 유지한다. |

JipperKeyViewer는 JipperResourcePack과 별도 모드다. 공개 Ghostify 조사 소스는 `0.4.5`이며 스크린샷의 `0.4.7` DLL과 동일하다고 확인하지 않았다. 현재 로컬 게임에는 JipperResourcePack과 ImplResourcePack이 있고, Quartz·JipperKeyViewer·Ghostify는 설치되어 있지 않다. 모든 모드의 실제 렌더 성공을 확인한 상태는 아니다.

## 4. 제안하는 구조

### 공통 코어와 얇은 접근 계층

공통 코어는 시간 공급자, 키 상태 공급자, 세션 시작·종료, 협력 모드 등록만 담당한다. Unity adapter는 `KeyCode`와 Unity 시간 접근을 연결한다. Renderer adapter는 기존 timeline과 held 상태를 연결한다.

모드가 사용하는 이름은 기획용 가칭이다.

| 기능 | 첫 버전의 역할 |
| --- | --- |
| 시간 조회 | 현재 시각과 이번 논리 갱신의 경과 시간을 제공한다. |
| Stopwatch 형태의 timer | `Start`, `Stop`, `Reset`, `Restart`, `IsRunning`, elapsed 조회를 clock 공급자 위에서 제공한다. native Stopwatch와 TimeSpan의 ticks 단위 차이를 보존한다. |
| 키 상태 조회 | `KeyCode`에 해당하는 현재 눌림 상태를 제공한다. 실제 사용 시 모드의 원래 입력원을 보존할 수 있게 한다. |
| 세션 상태 | 외부 구동 여부와 세션 식별자, 시작·종료 알림을 제공한다. 종료에는 완료·취소·오류를 포함한다. |
| 등록 | 해당 모드가 공통 계약으로 연동되었음을 알려 기존 자동 호환 처리와 구분한다. 모드마다 시간·입력 공급자를 따로 만들지 않는다. |

V1의 입력 계약은 현재 상태 조회를 기본으로 한다. 같은 갱신 사이에 press/release가 모두 끝나면 최종 상태는 released다. 모드가 이 상태에서 표시하지 않는 입력을 렌더러가 복원하지 않는다. 원본 CSV 이벤트는 계속 보관하고 기존 게임 재생·다른 소비자에 필요한 처리는 유지한다.

SDK는 모드의 UI·프로필·카운터 구조를 알지 않는다. 시작·종료 알림을 받은 모드가 자기 타이머와 표시 상태를 준비하고 복구한다. Stopwatch 형태의 wrapper가 이미 실행 중인 모드의 private 필드를 찾아 바꾸는 방식으로 만들지 않는다.

평소 실행에서 기존 입력·시간 동작이 유지되는지가 중요한 검증 항목이다. 기존 `Time`의 scaled/unscaled 의미와 모드의 native 입력 경로를 보존한다. 프레임 시간, UI 타이머, 실제 성능 측정, 저장·네트워크 timeout의 시계를 구분해 연동 문서에 예시를 둔다.

코어 target framework 기본안은 `netstandard2.0`이다. Renderer와 Quartz·Impl 계열의 `netstandard2.1`뿐 아니라 Jipper 계열 `net481`, Ghostify의 .NET Framework 4.8도 고려해야 한다. Unity API는 adapter에 두고 코어에 game·Harmony·IPC 참조를 넣지 않는다. 외부 프로젝트별 확인 근거는 앞선 조사 보고서에 있다.

### 렌더러가 값을 공개하는 순서

1. 기존 RenderClock으로 논리 갱신 시각을 정한다.
2. 기존 Cursor와 timeline을 사용해 해당 시각까지의 입력·판정을 처리한다.
3. 완성된 키 상태와 프레임 시간을 공통 공급자에 공개한다.
4. 기존 추가 UI 갱신 흐름에서 모드가 이 상태를 읽게 한다. 이때 가상 시간은 고정하고 delta는 0으로 둔다.
5. 기존 `FrameCapture`가 Canvas를 합성하고 인코딩한다.

현재 코드에서는 첫 Unity `Update` 이후 `LateUpdate`에 기록 입력이 적용된다. 따라서 3번은 Cursor가 입력 처리를 마친 뒤에 두고, 4번이 최신 입력 상태를 읽는지 검증한다. 구현 시점에 `Update`/`LateUpdate` 순서를 다시 확인하고, 프레임 지연이나 상태 중복 처리를 검사한다.

협력 모드가 읽는 시계는 논리 프레임에 고정한다. 기존 `ApplyInput`이 이벤트 전송을 위해 잠깐 `CurrentVideoTimeUs`를 각 입력의 시각으로 바꾸거나, `OverlayVideoClock`이 worker의 event time을 읽는 동작을 새 상태 조회 공급자에 그대로 노출하지 않는다.

기본안의 시간은 영상의 진행 시간이고, 정상 논리 갱신의 delta는 게임 시뮬레이션 step에 맞춘다. 현재 자동 계층도 `Clock.Fps`를 기준으로 delta를 계산한다. UI refresh에서 delta가 0이어야 타이머와 애니메이션이 기다리는 프레임 수만큼 추가 진행되지 않는다. [현재 delta 계산](/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OptionalModClock.cs:188)

### 기존 자동 계층과 함께 둘 경우

협력 모드는 등록 정보를 통해 자동 시간·입력 치환에서 제외한다. 시간·입력을 직접 읽는 공통 runtime assembly도 탐색·치환 대상에서 제외한다. 기존 모드는 현재 호환 경로를 사용한다.

현재 자동 패치는 job 종료 시 없어지는 구조가 아니다. 등록이 이전 job 이후에 생기면 기존 패치의 적용 여부까지 처리해야 한다. 기본안은 모드 초기화 시 등록하고 활성 job 중 등록 변경을 금지한다. 이미 설치된 renderer 소유 패치의 정리·우회가 필요한 경우 다른 Harmony owner를 보존한다. queue 동기화와 시간·입력 치환의 적용 범위도 분리해 검사한다.

Q2에서 SDK 전용 전환을 선택하면 위 병행 연결 대신 현재 자동 호환 처리를 shipping 경로에서 정리하는 계획으로 바꾼다. 과거 모드별 어댑터는 현재도 테스트 fixture이므로 삭제 여부를 별도로 검토한다.

### 성능 기준

- CSV parsing과 문자열→키 변환은 기존 준비 단계에서 끝낸다.
- 키 상태 조회와 시간 조회에 reflection, 파일 I/O, 이벤트별 delegate 생성이 들어가지 않게 한다.
- 논리 갱신마다 상태를 한 번 공개한다. 작업 스레드에서도 사용한다면 일관된 상태를 읽을 수 있게 한다.
- 평소 플레이에서 모드가 사용하던 native 경로를 보존하고, 공통 공급자 접근의 실제 비용을 측정한다.
- 게임 스레드에서 worker의 개별 입력 처리마다 blocking wait를 추가하지 않는다.

## 5. 구현 단위와 변경 후보

다음은 Q1~Q6의 기본안을 선택했을 때의 구체적인 변경 범위다. 새 프로젝트 경로와 package 형태는 Q3·Q4 답변 뒤에 확정한다.

| 단위 | 변경 후보 | 작업 내용 |
| --- | --- | --- |
| SDK core | Renderer 저장소 내 신규 `OverlayRuntime` 프로젝트를 기본 후보로 둔다. | 시간·키 상태 contract, 세션 수명주기, timer wrapper, 협력 모드 등록을 구현한다. |
| Unity adapter | 신규 별도 adapter 또는 작은 source adapter | Unity 시간·`KeyCode`를 연결하고 평소 입력 경로를 유지한다. Q12의 사용 방식에 맞춘다. |
| Renderer 공급자 | `/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/RecordedReplayDriver.cs`, 신규 frame provider | 기존 Cursor 처리 뒤 현재 상태를 공개하고 세션을 시작·종료한다. |
| 호환 경로 분리 | `/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OptionalModClock.cs`, `OverlayAssemblyDiscovery.cs`, `OverlayMethodDiscovery.cs`, `OverlayRuntimeScope.cs` | 협력 모드와 SDK의 중복 치환을 막는다. Q2에 따라 병행 지원 또는 자동 경로 정리를 한다. |
| UI refresh 시간 | `/Users/kgh/dev/src/TUFReplay-Renderer/src/Engine/Renderer/RendererController.cs`와 신규 provider | refresh 중 현재 시각 고정과 delta=0을 제공한다. 기본안에서는 기존 Canvas 수집·합성·프레임 반복을 사용한다. |
| 상태 진단 | `/Users/kgh/dev/src/TUFReplay-Renderer/src/Replay/OptionalModCapabilities.cs` | 실제 SDK 등록 여부, 기존 호환 경로, 실제 검증 여부를 구분해 보고한다. 등록만으로 runtime verified라고 표시하지 않는다. |
| 빌드·배포 | `/Users/kgh/dev/src/TUFReplay-Renderer/src/TUFReplay-Renderer.csproj`, `/Users/kgh/dev/src/TUFReplay-Renderer/scripts/lib/artifacts.sh`, 기존 workflow/test scripts | 새 의존성과 선택한 package를 build·install·package 검증에 포함한다. 현재 단일 Renderer DLL payload 가정을 수정할 수 있다. |
| 개발자 자료 | Renderer docs와 작은 연동 sample | 상태 조회 방식, time wrapper, 세션 알림, focus/native 입력 선택, render 중 저장 정책, 기존 Canvas 조건을 설명한다. |
| TUFReplay | `/Users/kgh/dev/src/tuf-replay-dev` | 기본안에서는 기존 bundle export와 Render 요청 계약을 재사용한다. 인게임 재생·새 설치 UI까지 선택하면 해당 범위를 별도로 추가한다. |

## 6. 구현 순서와 검증

1. **질문 답변 반영:** 첫 완료 기준, runtime 배포, 병행 호환, FPS 정책을 확정한다. API 이름은 이때 소수의 일관된 이름으로 정한다.
2. **공통 core:** fake clock으로 timer 수명주기, 상태 조회, session 전환을 검증한다. 실제 UI와 game assembly 의존성을 넣기 전에 계약을 안정화한다.
3. **Renderer 연결:** 새 공급자에 frame time과 누적 held 상태를 연결한다. 동일 시각 순서, countdown, 녹화 pitch, terminal tail을 기존 timeline과 비교한다.
4. **호환 경로 분리:** 협력 모드가 자동 치환되지 않는지, 미연동 모드의 기존 경로가 유지되는지 검사한다. 여러 모드와 연속 렌더·취소를 포함한다.
5. **작은 sample:** 기존 Canvas 경로로 키 표시와 timer 기반 애니메이션을 실제 캡처한다. UI refresh에서 상태가 한 번 반영되고 시간이 추가 진행되지 않는지 확인한다.
6. **선택한 실제 모드:** 모드 개발자가 사용할 수 있는 integration 예제를 작성하고, Q6·Q14에 따라 실제 포팅 또는 SDK 사용 빌드의 검증까지 진행한다.
7. **배포 검증과 문서:** runtime 의존성·버전·로드 순서·정상 native 복귀를 확인한다. 지원 대상별로 소스 조사·계약 검증·실제 렌더 검증을 구분해 기록한다.

의미 있는 테스트 항목은 다음과 같다.

- 프레임 사이 press/release를 최종 released로 읽고, 같은 시각의 sequence 순서가 최종 상태에 반영된다.
- held 키와 여러 동시 키, `Mouse0`~`Mouse4`, signed countdown 입력을 읽는다.
- 같은 시간의 추가 UI refresh에서 delta=0이고, 타이머 시간이 늘지 않는다.
- 게임 FPS/영상 FPS 조합 `60/60`, `60/120`, `240/60`에서 선택한 정책대로 상태·시간·캡처가 진행된다.
- 서로 다른 모드가 같은 공급자를 읽으며 실제 keyboard 입력이 render 상태에 섞이지 않는다.
- 완료·취소·시작 실패·모드 unload 후 native 공급자와 세션 상태가 복구된다. 연속 렌더가 이전 세션의 시간·held 상태를 물려받지 않는다.
- native Stopwatch와 virtual timer에서 Start/Stop/Reset/Restart 및 elapsed 단위가 맞는다. 모드의 실제 성능 측정 clock이 의도치 않게 바뀌지 않는다.
- SDK와 기존 호환 경로가 공존하는 경우 cached delegate와 지속 Harmony 패치가 중복 시간 전환을 만들지 않는다.
- 정상 플레이에서 기존 UI·입력 동작과 저장 결과가 유지된다. Q10의 카운터 정책도 실제 저장 파일로 확인한다.

Renderer의 `./scripts/run.sh`를 사용한다. 기존 `check`, `test`, `clock-check`, `script-test`, `mod-check`에 신규 runtime 검증을 연결한다. 설치를 동반하는 `build`는 검증·배포 단계에 사용한다. 구조 변경의 크기에 따라 순수 runtime 테스트용 task를 추가해 빠른 반복 검증을 가능하게 한다.

이번 기획 단계에서는 build·test·설치·실제 game render를 실행하지 않았다. 코드와 설치 파일의 읽기 조사, 게임 mapper DLL의 ILSpy 확인, 기획 문서의 참조·형식 확인을 수행했다.

## 7. 먼저 결정해야 구현 범위가 정해지는 질문

추천은 현재 요구와 코드 구조를 기준으로 잡은 기본안이다. 추천을 답변으로 간주하지 않는다.

### Q1. 첫 버전은 오프라인 렌더링만 대상으로 할까, 일반 인게임 리플레이도 같은 계층을 사용할까?

- **추천: 오프라인 렌더링부터.** Renderer만 공급자를 연결하고 현재 TUFReplay 재생·native emission 구조를 유지한다.
- 인게임 리플레이도 포함. TUFReplay 쪽 session·pause·seek·focus·native worker와 공급자를 연결하는 범위가 추가된다.

### Q2. SDK를 사용하지 않는 모드에 대한 현재 자동 시간·입력 패치 경로는 남길까?

- **추천: 우선 병행.** 협력 모드는 새 경로를 사용하고 기존 모드는 현재 호환 경로를 유지한다. 두 경로의 중복 적용 방지가 필요하다.
- SDK 전용으로 전환. 자동 호환 코드를 정리하고 연동된 모드를 지원 기준으로 삼는다. 미연동 모드의 입력·애니메이션 동기화는 보장하지 않는다.

### Q3. 공통 runtime을 별도의 UMM 의존 모드로 설치하는 형태를 받아들일 수 있어?

- **추천: 하나의 공유 runtime package.** 모드들과 Renderer가 같은 타입·세션 상태를 공유하고, Renderer가 없어도 runtime의 native 기능은 동작한다.
- 추가 설치 없이 모드 또는 Renderer package에 runtime DLL을 포함. 배포는 편하지만 중복 DLL·버전·로드 순서와 하나의 runtime instance 공유를 실제 UMM에서 검증해야 한다.

이 선택은 단순 파일 이름 결정이 아니다. 각 모드에 source를 복사해 서로 다른 static 상태를 만드는 방식은 외부 공급자 연결 계약에 추가 구조가 필요하다.

### Q4. SDK 소스는 Renderer 저장소 안의 독립 프로젝트로 둘까, 별도 저장소로 뺄까?

- **추천: Renderer 저장소 안의 독립 프로젝트·package부터.** core의 참조 경계를 지키면서 build·test·version을 함께 관리한다.
- 처음부터 별도 저장소·배포 단위. 독립 재사용에 유리하지만 SDK와 Renderer 간 version/release 관리가 추가된다.

실제 렌더 연결 코드 위치는 `/Users/kgh/dev/src/TUFReplay-Renderer`다. `/Users/kgh/dev/src/tuf-replay-dev`는 현재 기본 작업·기획 문서 위치이며, 두 저장소의 역할을 확정할 필요가 있다.

### Q5. 영상 FPS가 게임 FPS보다 높을 때, 키뷰어도 영상 프레임마다 새로 갱신해야 할까?

- **추천: 현재 방식 유지.** 게임 갱신 시 UI도 진행하고, 영상은 필요한 프레임을 반복한다. 시간·입력 계층 연결에 범위를 집중할 수 있다.
- 키뷰어를 영상 FPS마다 독립 갱신. 게임 화면은 반복하면서 overlay를 다시 그리는 단계가 필요하며, 렌더 루프와 프레임 반복 정책 변경이 포함된다.

이 질문은 짧은 입력의 보정 여부와 별개다. 최종 화면에서 overlay 애니메이션을 몇 번 갱신하느냐를 정한다.

### Q6. 이번 작업의 첫 완료 기준은 어디까지로 잡을까?

- **추천: SDK + Renderer 연결 + 작은 sample의 실제 렌더 + 개발자 문서.** 각 모드 개발자가 포팅할 수 있는 계약을 먼저 완성한다.
- 실제 모드 한 개가 SDK를 사용하는 빌드와 실제 렌더 검증까지. 대상 모드 소스 변경 또는 개발자의 연동 빌드가 필요하다.

## 8. 제품 동작과 연동 경험을 정하는 질문

### Q7. 지원·연동 문서 대상은 스크린샷에 나온 다섯 모드 모두일까?

Quartz, ImplResourcePack, JipperResourcePack, GhostifyOverlay, JipperKeyViewer다. 앞서 “4개”라고 했지만 JipperKeyViewer가 별도 모드로 추가되어 목록은 다섯 개다.

**추천: 다섯 모드를 문서 대상으로 두고 실제 검증 범위를 별도 표기.** 특정 모드를 제외하려면 알려주면 된다.

### Q8. 키 이펙트·KPS 타이머는 영상의 경과 시간을 따를까, 녹화의 song/replay 시간을 따를까?

- **추천: 영상의 경과 시간.** 일반적인 unscaled UI 애니메이션처럼 작동하고, 입력 시각은 기존 pitch mapping으로 변환한다.
- song/replay 시간. 녹화 pitch에 따라 UI 애니메이션·KPS window의 실제 영상 길이도 바뀔 수 있다.

게임의 판정·진행도·song position은 기존 시뮬레이션에서 읽는다. 이 선택은 UI timer의 기본 시계에 관한 것이다.

### Q9. 렌더 시작 화면의 카운터·키 이펙트 상태는 어떻게 시작하는 게 좋아?

- **추천: 이번 렌더용 상태로 시작.** 세션 카운터와 활성 키 이펙트를 초기화한다. 누적 카운터 표시는 모드의 설정에 맞게 별도 초기값을 줄 수 있다.
- 렌더 직전 모드 화면 상태를 이어간다. 켜져 있던 효과와 기존 카운터가 시작 화면에 포함될 수 있다.
- 모드 개발자가 기존 설정과 의미에 맞게 전부 정한다. SDK는 세션 알림만 제공한다.

### Q10. 렌더링 때문에 실제 사용자의 누적 카운터·프로필 저장값이 늘어나는 것은 막아야 할까?

- **추천: 실제 저장값 유지.** 모드 개발자에게 render 중 임시 상태와 저장 억제를 계약으로 요구하고, SDK는 세션 알림을 제공한다.
- 누적되어도 허용하거나 모드별 정책에 맡긴다. SDK 문서에 이를 의무로 두지 않는다.

SDK가 모드의 저장 파일·private counter를 직접 수정할 필요는 없다. 요구할 사용자 동작을 정하는 질문이다.

### Q11. 연동된 모드가 세션 시작·갱신에서 오류를 내면 전체 렌더를 실패시킬까?

- **추천: 해당 job을 실패시키고 정상 정리.** 잘못된 UI를 포함한 결과를 조용히 완료 처리하지 않는다.
- 문제 overlay를 제외하고 계속 렌더. 해당 모드의 Canvas를 제외하는 추가 식별·처리와 명확한 경고가 필요하다.

### Q12. 모드 개발자가 사용하는 API 형태는 어느 쪽이 더 마음에 들어?

- **추천: 익숙한 Time·Stopwatch·키 조회 형태의 얇은 wrapper + 내부 공급자 interface.** 일반 모드는 읽는 위치를 바꾸는 정도로 접근하고, Quartz처럼 기존 clock이 있으면 provider를 직접 연결한다.
- 처음부터 `IClock`·`IInputState`를 생성자/객체에 명시적으로 주입. 객체마다 의존성이 보이지만 기존 static·MonoBehaviour 중심 모드의 변경 범위가 늘 수 있다.

두 선택 모두 모드 곳곳에 TUFReplay 조건 분기를 넣지 않는 것을 목표로 한다. 첫 버전에 전체 Unity Time·System DateTime API를 복제하기보다는 조사된 시각·delta·timer·키 상태 접근부터 제공하는 것을 추천한다.

### Q13. 첫 실제 게임 검증은 어떤 플랫폼까지 필요해?

- **추천: 현재 로컬 macOS에서 먼저 검증하고 Windows를 별도 필수 검증 단계로 명시.** core 계약과 두 플랫폼용 build 검증도 수행한다.
- 첫 완료 시점부터 macOS와 Windows 실제 game render 모두 필요.

현재 로컬 설치 상태만으로 Windows 실기기 검증 완료를 주장할 수는 없다. Windows 실행 환경이 이미 준비되어 있다면 그 범위를 계획에 반영한다.

### Q14. 실제 모드 연동을 검증한다면 어떤 모드를 먼저 잡을까?

- **추천: Quartz.** 외부 시계·입력 driver가 이미 있어 공통 계약을 기존 구조에 연결하는 예제로 적합하다. 현재 로컬 게임에는 설치되어 있지 않다.
- JipperKeyViewer. 입력 진입점이 모여 있어 상태 조회형 예제를 만들기 좋다. 현재 로컬 게임에는 설치되어 있지 않다.
- ImplResourcePack 또는 JipperResourcePack. 둘 다 로컬에 설치되어 있다. Impl은 시간·게임 상태 overlay, JipperResourcePack은 입력·worker·UI 전달 경계를 검증할 수 있다.
- GhostifyOverlay. 대상 source/binary version부터 먼저 확정해야 한다.

## 9. 답변 방법

Q1~Q6은 구현 범위·배포·렌더 흐름을 결정한다. 먼저 번호로 선택해주면 계획의 주요 분기를 확정할 수 있다. Q7~Q14는 원하는 동작이 있으면 함께 답하면 된다. 모드별 구현 방법은 개발자가 결정한다는 현재 방향을 유지한다.

질문 답변을 반영해 계획을 확정한 뒤, 소스 변경을 요청받으면 구현을 시작한다.
