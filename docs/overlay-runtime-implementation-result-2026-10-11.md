# 공용 오버레이 렌더 SDK 구현 결과

작성일: 2026-10-11. 기준 작업 디렉토리: `/Users/kgh/dev/src/tuf-replay-dev`.

공용 SDK, Renderer 연결, JipperResourcePack 키뷰어·오버레이 포팅, ImplResourcePack의 게임 오버레이 포팅을 구현하고 빌드·자동 검증을 완료했다. 검증된 프로그램 파일도 로컬 게임에 설치했다. **실제 게임에서의 SDK 공동 로드, Canvas 출력과 종료 후 native 복구 검증은 사용자의 선택에 따라 나중으로 미뤘다.** 따라서 이 문서의 완료는 구현·빌드·자동 검증 범위이며 실제 영상 출력 성공을 뜻하지 않는다.

`impl-dmnote` 소스·앱·IPC·mirror 코드는 변경하지 않았다. 커밋, push, PR 생성은 하지 않았다.

## 작업 위치와 브랜치

| 구성 요소 | 작업 위치 | 브랜치 |
| --- | --- | --- |
| TUFReplay 웹 | `/Users/kgh/dev/src/tuf-replay-dev` | `dev` |
| Renderer와 공용 SDK | `/Users/kgh/dev/src/TUFReplay-Renderer` | `main` |
| JipperResourcePack 포크 | `/Users/kgh/dev/src/jipper-resourcepack` | `feat/tufreplay-render-support` |
| ImplResourcePack 실험 | `/Users/kgh/dev/src/impl-resourcepack-tufreplay` | `feat/tufreplay-overlay-support` |

Jipper의 origin은 사용자의 `KGH1113/JipperResourcePack` 포크다. Impl은 최신 main 커밋 `5426d52`에서 독립 작업 디렉토리를 만들었다. 기존 `/Users/kgh/dev/src/impl-resourcepack`과 `/Users/kgh/dev/src/impl-resourcepack-render`는 clean 상태로 보존했다. Renderer에 원래 있던 `ProgressFont SDF.asset` 변경도 보존했다.

## 구현된 동작

- SDK는 Unity 참조가 없는 `netstandard2.0` DLL이다. 각 모드에 같은 DLL을 동봉하며 별도 UMM 의존성 모드를 설치하지 않는다.
- `OverlayTime`, `RenderStopwatch`, `OverlayInput`, `OverlayEnvironment`는 렌더 밖에서는 등록된 native API를 읽고 렌더 중에는 공용 세션 값을 읽는다. 같은 Stopwatch의 실제 실행 상태와 렌더용 상태를 분리한다.
- Renderer는 게임 시뮬레이션 직전에 현재 프레임의 시계만 먼저 게시한다. 판정 콜백에서 재시작한 애니메이션 시계가 이전 프레임을 기준으로 잡히지 않도록 한다.
- native CSV 이벤트를 기존 게임 입력 경로에 적용한 뒤, 해당 시각의 최종 held key 상태를 SDK에 전달하고 모드 UI를 한 번 갱신한다. 프레임 사이에 끝난 짧은 입력은 모드의 기존 상태 조회 방식에 따라 보이지 않을 수 있다.
- 캡처 대기 동안 delta와 새 key edge는 0이다. 대기나 추가 UI refresh 때문에 KPS·카운터·애니메이션이 반복 진행하지 않는다.
- 기존 persistent Unity Canvas 합성을 사용한다. 게임 시뮬레이션 FPS와 영상 FPS에 따른 기존 프레임 반복 방식도 유지한다.
- 참여 모드가 실패하면 렌더를 멈추고 사용자 선택을 기다린다. 선택은 **문제 오버레이 제외 후 계속 / 렌더 취소**다. 선택을 거부하면 취소하며 자동 재시도하지 않는다.
- 제외 요청에는 job·decision·session 식별자를 넣는다. 만료된 응답으로 다른 세션을 수정하지 않는다. 오버레이 숨기기에 실패하면 렌더를 취소한다.
- 캡처 직전에도 새 비동기 오류를 확인한다. 종료 시 캡처가 원래 Canvas 속성을 복구한 다음 모드 세션을 정리한다.
- 오류 선택 대기 화면에서는 설정 폼을 숨겨 이유와 선택 버튼이 함께 보이게 한다. 설정 자체는 보존한다.

Renderer의 과거 모드별 자동 입력·시계 패치는 shipping compile과 기본 실행 경로에서 제외했다. 과거 소스·테스트 자료는 보존했다. 과거 검증 파일까지 삭제하려던 정리는 자동 승인 검토가 범위를 벗어난다고 거부했으며, 삭제 없이 SDK로 실행 경로를 전환했다.

## 모드별 포팅

### JipperResourcePack

[KeyViewer.Render.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs)가 SDK 프레임의 held key 변화에 기존 `Work` 처리를 연결한다. 기존 Canvas와 UI 생성 함수를 재사용하고, 렌더 전용 UI·rain·카운터를 만든다. 실제 입력 worker는 렌더 동안 차단하고 종료 때 기존 모델·시계·카운터 참조를 복구한다.

[Overlay.Render.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.Render.cs)는 기존 리소스팩 오버레이를 같은 세션에 등록한다. 콤보 시계와 표시 상태를 격리하고 실제 `Plays.dat` 저장을 차단한다.

최종 리뷰에서는 초기화가 바꾼 updater 활성 상태도 복구하고, 생성한 render root를 별도 참조로 소유해 초기화 실패 시 live root를 파괴하지 않도록 했다. 렌더 중 실제 키 변화는 카운터·KPS·rain에 반영하지 않고 native held 상태 복구에만 사용한다. 게임 스레드 호출을 직접 처리하는 기존 JALib dispatch를 재사용해 불필요한 Unity 직접 호출 분기를 제거했다.

현재 기록의 Unity KeyCode 이름으로 표현되는 바인딩을 지원한다. 이 모드의 별도 native key ID 바인딩은 SDK에 표현할 수 있지만, 현재 CSV에는 그 ID를 복원할 정보가 없으므로 입력 이름을 임의 변환하지 않았다. 실제 바인딩 확인은 게임 검증 때 필요하다.

공개 원본 빌드의 환경 의존성을 정리하기 위해 늦은 참조 override, Harmony extern alias와 선택적 KeyboardChatterBlocker 참조 설정을 추가했다. 키뷰어·UI 로직을 새로 작성한 것은 아니다. 설치 패키지는 이미 있는 JAMod bootstrap·assets·Info를 유지하는 프로그램 업데이트용이다.

### ImplResourcePack 게임 오버레이

[GameplayOverlay.cs](/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/GameplayOverlay.cs)가 기존 표시 요소를 재사용해 native 화면과 렌더용 화면을 소유한다. [OverlayRenderScope.cs](/Users/kgh/dev/src/impl-resourcepack-tufreplay/ImplResourcePack/Bootstrap/OverlayRenderScope.cs)는 새 상태 생성·제외·정리·원래 표시 상태 복구를 관리한다.

status ticker의 렌더 갱신은 가상 시계로 진행한다. 실제 FPS sampler는 native 성능 측정에 남겨 두고 그 측정 성공 여부가 렌더 UI 갱신을 막지 않게 했다. 콤보 애니메이션은 `RenderStopwatch`로 전환했다.

기존 mirror Canvas와 `_dmNoteMirror`의 attach·show·hide·tick·dispose 경로는 보존했다. 게임 오버레이를 별도 Canvas로 구성해 렌더 전용 화면의 숨김과 정리가 mirror에 영향을 주지 않도록 했다. 수정 파일 목록에서 앱·IPC·DmNote·vendor 경로는 비어 있으며 Renderer의 dmnote 관련 수정 라인도 없다.

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| Renderer `./scripts/run.sh mod-check` | Release 빌드, Unity/Mono 호환성, 파서·timeline·media·FFmpeg·엔진·설치 보존 검증 통과 |
| SDK `./scripts/run.sh overlay-sdk-check` | 14개 검증 통과, 빌드 경고·오류 0 |
| 공용 DLL 로드 | Mono에서 두 복사본의 정방향·역방향 로드 모두 동일 registry 공유 검증 통과 |
| Jipper `./scripts/run.sh package/check/install` | 실제 게임 참조로 본체·4개 VersionSafe DLL 빌드 및 설치 검사 통과 |
| Impl `./scripts/run.sh mod-check` | 경고·오류 0, 17개 테스트 그룹 통과 |
| TUFReplay `./scripts/run.sh web-check` | 210개 테스트, 타입 검사, Biome 231개 파일, 프로덕션 빌드 통과 |
| 스크립트 구문 검사 | TUFReplay 35개, Impl 26개 통과; shellcheck 실행 파일은 없어 해당 검사는 미실행 |
| 변경 파일 공백 검사 | 네 작업 디렉토리 모두 `git diff --check` 통과 |
| 프로그램 설치 후 사용자 데이터 | 별도로 해시를 기록한 JSON·DAT·XML 10개 변경 없음 |
| Jipper 최종 프로그램 설치 | 패키지·설치 DLL 6개 해시 일치, 보호 대상 설정·카운터·자산·bootstrap 22개 해시 변경 없음 |

SDK 검증은 native getter 값, held snapshot 복사, 프레임 선행 시계, 짧은 탭 상태 정책, getter 무할당, Stopwatch 격리와 반복 세션, 시작·비동기 실패 큐, 제외·등록 해제, 중복·역행 세션 거부를 포함한다. Impl의 추가 수명주기 검증은 정상 종료·제외·실패 정리·중첩 시작 거부·반복 종료와 기존 카운터·표시 상태 복구를 다룬다.

실제 브라우저의 격리 mock에서 2분 넘는 선택 대기 중 진행률이 정지하는 것, 제외 후 완료와 경고 표시, 취소 후 다시 시작 가능 상태, 만료된 제외 요청 후 대기 유지와 취소를 확인했다. 기본 1280×720 화면에서 오류 설명이 설정 폼 아래로 밀리는 문제를 발견해 수정했다. 수정 뒤 재시점의 IAB surface가 없어 추가 시각 검증은 수행하지 못했으며 최종 타입·포맷·빌드 검사는 통과했다. 검증용 서버와 임시 창·탭은 종료했다.

Jipper의 첫 전체 빌드에는 원본 R136 코드의 `FindObjectOfType` obsolete 경고 1개가 있었고 최종 incremental 패키지 빌드는 경고·오류 0이었다. 웹 빌드에는 기존 큰 chunk 경고가 남아 있다. 기존 dmnote 테스트는 코드를 바꾸지 않고 Renderer workflow의 검증으로 실행했다.

## 패키지와 설치 상태

- [Renderer ZIP](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay-Renderer.zip): Renderer·공용 DLL·공용 SDK 문서를 포함한다. archive 검사 통과.
- [Jipper 프로그램 패키지](/Users/kgh/dev/src/jipper-resourcepack/artifacts/render-support/JipperResourcePack): 본체·공용 SDK·VersionSafe 4개 DLL. 기존 bootstrap·settings·assets와 함께 사용한다.
- [Impl ZIP](/Users/kgh/dev/src/impl-resourcepack-tufreplay/build/ImplResourcePack.zip): 공용 SDK가 포함된 패키지.
- [공용 SDK 문서](/Users/kgh/dev/src/TUFReplay-Renderer/sdk/README.md): 특정 모드에 종속되지 않은 등록·native API·수명주기·배포 계약.

로컬 ADOFAI의 `Mods/TUFReplay-Renderer`, `Mods/JipperResourcePack`, `Mods/ImplResourcePack`에 검증된 프로그램 파일을 설치했다. Jipper는 DLL 6개만, Impl은 본체·SDK·PDB만 교체했다. 기존 설정·Info·자산·IPC와 bootstrap을 보존했고 Renderer도 기존 프로그램 파일만 업데이트했다.

세 모드에 설치한 `TUFReplay.Overlay.dll`의 SHA-256은 모두 다음과 같다.

```text
5e6fd3a3175491dcfccade410a3e2dab4cf6c71605cac47e166266af0be578c0
```

교체 전 프로그램 파일은 [백업 디렉토리](/private/tmp/tuf-overlay-validation-originals)에 `renderer`, `jipper`, `impl`별로 보관했다. Jipper·Impl의 SDK DLL은 이전에 없었으므로 복구 시 이 파일도 제거 대상이라는 기록을 남겼다. 백업은 임시 디렉토리이므로 실제 게임 검증과 복구 판단 전에 유지해야 한다.

최종 Jipper 본체 DLL의 SHA-256은 `3d421b118d57d3d05ece3a650fde31cae304c7ad190c4ff2db406350e65085b2`다. 종료 경계 보완까지 포함한 마지막 빌드와 설치 검사를 통과했으며 최초 원본 백업은 덮어쓰지 않았다.

## 실제 게임에서 다음에 확인할 것

사용자가 실제 게임 검증을 나중으로 선택했으므로 여기까지 진행했다. 다음 검증은 게임이 실행된 뒤 수행한다.

1. Renderer health의 참여 모드 ID를 확인해 실제 UMM·JALib 환경에서 동봉 DLL들이 registry 하나를 공유하는지 확인한다. 현재 Mono 복사본 검증은 통과했지만 실제 JALib loader 결과를 대신하지 않는다.
2. 기존 native input CSV 기록으로 Jipper 키뷰어와 Impl 게임 오버레이가 Canvas 합성 영상에 표시되는지 확인한다.
3. 서로 다른 simulation/video FPS, 홀드·동시 키·짧은 입력, KPS·rain·combo 시간 진행을 확인한다.
4. 반복 렌더·취소·모드 제외 후 실제 입력 worker·native 시계·Canvas·카운터가 복구되는지 확인하고 실제 사용자 파일의 해시를 비교한다.
5. 실제 판정·시각·콤보 등 게임 상태 표시를 확인한다. Windows 실제 실행은 후속 검증이다.

native 경로의 프레임 시간·worker 성능 차이는 실제 게임에서 아직 측정하지 않았다. SDK getter의 할당 없음은 자동 검증했으나 전체 모드의 성능 차이가 없다고 단정하지 않는다.
