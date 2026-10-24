# 플레이 후 에디터 복귀 멈춤 조사

조사일: 2026-10-24. 소스, 현재 Mac에 설치된 게임·모드 DLL, 설정, 최근 두 게임 로그를 읽었다. 게임이나 설정을 변경하지 않았으며, 성능 계측용 패치도 설치하지 않았다.

## 판단

**현재 가장 먼저 검증할 원인은 ImplResourcePack의 이펙트 제거 복원과 ADOFAI의 맵 재구성이 복귀 프레임에 몰리는 것이다. 리플레이 저장 완료를 기다리는 구조는 확인되지 않았다.**

이것은 코드로 확인한 병목 후보이지, 사용자가 경험한 1–2초의 시간을 해당 함수에서 측정했다는 뜻은 아니다. 해당 복귀 순간의 프로파일과 기능별 비교 결과가 없어 최종 원인을 확정할 수 없다. 확대된 화면에서 멈추는 현상도 카메라 복구 중 프레임이 지연되는 설명과 부합하지만 아직 추정이다.

## 1. 리플레이 저장에서 확인한 사실

설치된 `Mods/TUFReplay/Runtime/versions/1.0.1/TUFReplay.dll`을 ilspycmd로 확인했다. 아래 핵심 경로는 작업 디렉터리의 소스와 같은 구조다.

| 작업 | 실행 방식과 근거 |
| --- | --- |
| 복귀 직전 입력·판정 상태 확정 | 게임 스레드에서 `RecordingFeature.OnReturnedToEditor` → `RecordingSession.CompleteRunPayload` |
| 완료한 입력·판정 목록 인계 | `CompletedSnapshot()`의 얕은 복사. 전체 목록을 복제하지 않음 |
| CSV/메타데이터 생성 및 DB 쓰기 | `RecordingActivityTracker.SaveRun`이 `SerialBackgroundQueue`에 예약. `TaskScheduler.Default` 사용 |
| 저장 완료 대기 | 일반 에디터 복귀 경로에서 저장 Task에 대한 `Wait`, `Result`, `GetResult` 대기를 찾지 못함 |
| macOS 마이크 녹음 종료 | `EndRunAsync`가 helper 명령을 큐에 넣고 Task를 반환 |
| 카메라 녹화 종료 | `WebcamRecordingFeature.EndRun`이 `capture.end` 작업을 큐에 넣음 |
| 네이티브 입력 훅 종료 | 매 복귀 때 훅 스레드를 Join하지 않음. 진행 중 콜백 대기는 코드상 약 1 ms 기한을 사용 |

관련 코드:

- [복귀 Prefix](../TUFReplay/Recording/Patches/RecordingPatches.cs#L311)
- [녹화 종료 흐름](../TUFReplay/Recording/Sessions/RecordingFeature.cs#L131)
- [완료 payload 생성](../TUFReplay/Recording/Sessions/RecordingSession.cs#L342)
- [얕은 스냅샷](../TUFReplay/Replay/Models/RecordedRunPayload.cs#L63)
- [백그라운드 저장](../TUFReplay/Activity/Tracking/RecordingActivityTracker.cs#L223)
- [백그라운드 큐](../TUFReplay/Shared/Threading/SerialBackgroundQueue.cs#L27)
- [macOS 마이크 종료](../TUFReplay/Microphone/Capture/MacOsMicrophoneCaptureBackend.cs#L192)
- [카메라 종료](../TUFReplay/Webcam/Recording/WebcamRecordingFeature.cs#L320)

따라서 저장 코드를 단순히 `Task.Run`으로 감싸는 처방은 현재 구현에 대한 해결책이 아니다. 이미 분리돼 있다. 다만 백그라운드 직렬화의 메모리 할당, CPU·디스크 경쟁, 남아 있는 동기 콜백까지 영향이 없다고 단정할 수는 없다. Unity의 GC는 모드에 따라 메인 스레드를 멈출 수 있으므로 저장 영향의 최종 제외에는 계측이 필요하다. [Unity GC 설명](https://docs.unity.cn/Documentation/Manual/performance-incremental-garbage-collection.html)

## 2. 구체적으로 발견한 추가 동기 작업

최근 게임 로그에서 `ImplResourcePack 0.1.0`의 활성화를 확인했다. 저장된 `ImplResourcePack/Settings.xml`은 `Effects.Enabled=true`, `ActivePresetId=remove-all`이다. 반면 AdofaiTweaks, EnhancedEffectRemover, JipperResourcePack은 해당 로그에서 비활성 상태였다.

설치된 ImplResourcePack DLL의 `EffectReturnToEditorPatch.Prefix`는 다음 순서로 작동한다.

1. 이펙트 제거 적용을 잠시 중단한다.
2. 적용된 제거 옵션이 하나라도 있으면 `UpdateDecorationObjects(true)`를 호출한다.
3. `UpdateVideo()`를 호출한다.
4. 그 뒤 게임 원본 `SwitchToEditMode`가 실행된다.

동일한 코드는 인접 저장소의 [EffectRenderingPatches.cs](/Users/kgh/dev/src/impl-resourcepack/ImplResourcePack/Infrastructure/Game/Effects/EffectRenderingPatches.cs:92)에도 있다. 이 Prefix는 장식·배경을 실제로 제거했는지 구분하지 않고 `Applied != None`만 확인한다. 따라서 필터만 제거하는 프리셋도 장식과 영상 복원을 실행할 수 있다. 현재 저장된 Remove All에서는 실제로 장식도 제거 대상이므로 복원 자체가 필요하다.

게임 DLL에서 확인한 `UpdateDecorationObjects(true)`는 가벼운 표시 전환이 아니다.

- `ClearDecorations()`가 기존 장식 하위 Transform을 모은 뒤 `DestroyImmediate`로 게임 오브젝트를 제거한다.
- 활성 장식을 순회하면서 `CreateDecoration`으로 다시 만든다.
- MoveDecorations 이벤트를 순회하고 필요한 이미지를 조회·로드한다.
- `UpdateVideo()`는 영상이 있는 맵에서 영상 경로를 설정하고 `Stop()`·`Prepare()`를 호출한다.

**장식이 많은 맵에서는 이 전체 파괴·재생성 경로가 강한 병목 후보다.** 다만 현재 재현 맵의 장식 수와 이 호출의 소요 시간은 확인하지 못했다.

## 3. 게임 자체도 맵을 다시 구성한다

설치된 `Assembly-CSharp.dll`을 역컴파일해 다음 경로를 확인했다.

```text
scnEditor.SwitchToEditMode
  → TogglePause
    → scnEditor.ResetScene
      → scnGame.ResetScene(isResetCustomLevel: true)
        → ReloadAssets(force: false, reloadDecorations: false)
        → 카메라·필터·행성 상태 초기화
        → RemakePath(applyEventsToFloors: true, remakeLevel: true)
          → levelMaker.MakeLevel()
          → ApplyEventsToFloors(...)
        → ResetDecorations()
  → 선택 복원, 홀드·번호·멀티플래닛 등 편집용 표시 갱신
  → Resources.UnloadUnusedAssets()
```

즉 복귀에는 원래도 타일·이벤트 재구성 비용이 있고, 활성 이펙트 제거 모드가 그 앞에 장식 복원 비용을 추가한다. 녹화 저장과 같은 시점에 일어나므로 체감만으로 저장을 원인으로 구분하기 어렵다.

최근 `Player.log` 및 `Player-prev.log`의 unused-assets 정리 `Total`은 약 13–83 ms였다. 별도의 serialized-file `UnloadTime`에는 최대 약 300 ms가 있었다. 이 로그에는 문제의 복귀 시점과 연결할 실행 ID나 구간별 시간이 없다. **관측한 에셋 정리 수치만으로 1–2초 전체를 설명하거나, 에셋 정리를 주원인으로 확정할 수 없다.**

로그 위치: `/Users/kgh/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log` 및 `Player-prev.log`.

## 4. 왜 약간 확대된 상태로 멈추나

TUFReplay의 일반 녹화 종료 경로에서는 게임 카메라 줌을 바꾸는 코드를 찾지 못했다.

게임의 복귀 초기화는 `scrCamera.zoomSize = 1f` 등을 설정한다. 실제 화면 배율은 `scrCamera.UpdateSize()`에서 보간된 크기 × `userSizeMultiplier` × `zoomSize`로 적용된다. 에디터에는 줌 보간과 복귀 위치 이동도 존재한다. 현재 이펙트 제거 프리셋에는 CameraMotion이 포함되어 있어 플레이 상태와 편집 상태의 카메라 조건도 다를 수 있다.

이 과정에서 긴 메인 스레드 작업이 발생하면 사용자는 마지막으로 렌더링된 배율을 계속 보게 된다. 그러므로 “확대된 상태에서 멈춘 뒤 정상으로 돌아옴”은 복귀 중간 상태의 프레임 지연으로 설명할 수 있다. **정확히 어떤 카메라 값이 언제 바뀌는지는 아직 측정하지 않았으므로 줌 현상의 확정 원인으로 취급하면 안 된다.** 또한 화면 전체의 프레임 정지인지 입력만 잠긴 것인지도 구분해야 한다.

## 5. 원인을 확정하는 최소 비교

같은 맵·같은 복귀 방식으로 아래 네 조건을 비교한다. 설정은 플레이 전에 바꾸고 새로 시작해야 한다. 이펙트 제거 설정은 시작·재시작 때 적용되므로, 복귀 직전에 스위치만 바꾸는 비교는 부정확하다.

| 조건 | TUFReplay 자동 녹화 | ImplResourcePack Effects | 구분하는 영향 |
| --- | --- | --- | --- |
| A | 켬 | 현재 Remove All | 기준 |
| B | 켬 | 끔 | 제거된 이펙트 복원 비용 |
| C | 끔 | 현재 Remove All | 해당 run의 리플레이·마이크·카메라 저장 비용 |
| D | 끔 | 끔 | 게임 기본 복귀와 다른 활성 모드의 비용 |

이 비교는 아직 실행하지 않았다. 자동 녹화를 끄는 것만으로 상시 카메라 캡처 등 모드 전체가 사라지는 것은 아니므로, C/D는 TUFReplay 완전 비활성 대조와 동일하지 않다. 클리어 후와 중도 중단 후도 나누어야 한다. 짧은 run과 긴 run, 장식이 적은 맵과 많은 맵의 차이는 각각 저장량과 복원량의 영향을 구분하는 데 도움이 된다.

계측을 추가한다면 다음 항목을 같은 복귀 ID와 단조 시계로 묶는 것이 좋다.

- TUFReplay 입력 drain, 완료 상태 확정, 미디어 종료 예약, 복귀 Postfix 각각의 경과 시간.
- ImplResourcePack의 장식 복원과 영상 복원 시간.
- 게임의 ResetScene, ReloadAssets, MakeLevel, ApplyEventsToFloors, ResetDecorations 시간.
- 저장 큐 대기·직렬화·DB 쓰기·녹화 파일 종료 시간은 별도로 기록.
- 복귀 직전부터 이후 몇 프레임의 실제 프레임 간격과 카메라 배율, 메모리 할당·GC 정보.

Harmony Prefix 하나의 측정만으로 모든 타 모드 Prefix 시간을 포괄했다고 가정하지 말고 실제 패치 순서도 확인해야 한다. 로그는 복귀마다 요약 한 번으로 제한한다. 저장 완료 시간이 길어도 프레임 간격이 정상이라면 저장은 화면 정지의 직접 원인이 아니다.

## 6. 해결 방향

우선순위는 **복귀 구간 계측 → ImplResourcePack 복원 범위 축소 → 실제로 남은 병목 최적화**다.

1. **불필요한 장식·영상 복원부터 줄인다.** 장식 상태를 바꾸는 제거 옵션이 실제 적용됐을 때만 장식을 복원하고, 배경 영상 상태를 바꿨을 때만 영상을 복원한다. 옵션 간 의존성은 EffectLevelFilter와 게임의 적용 결과를 기준으로 판별한다. `Applied != None`은 너무 넓다. 다만 현재 Remove All은 두 대상이 모두 포함되어 있어 조건 축소만으로 해결되지 않을 수 있다.
2. **Remove All의 장식 전체 재생성을 줄인다.** 편집용 장식 오브젝트를 보존·재사용하거나 차이만 복원하는 구조를 검토한다. 메모리 사용과 유지 비용이 늘 수 있으므로 맵 크기별로 검증한다. 선택된 장식, collider, 태그, 게임플레이 조건 장식의 참조가 정확히 복구돼야 한다. 기존 Prefix를 단순 삭제하면 편집 상태가 깨질 수 있다.
3. **작업 분산은 참조 일관성을 유지할 수 있을 때만 적용한다.** 게임 원본 복귀는 선택 복원 전에 장식이 존재한다고 가정한다. 재생성을 무조건 Postfix나 다음 프레임으로 옮기면 그 가정을 깨뜨린다. Unity 오브젝트 생성·파괴를 `Task.Run`으로 보내는 방식도 적절하지 않다. 대부분의 Unity API는 메인 스레드에서 사용해야 한다. [Unity .NET/스레딩 설명](https://docs.unity3d.com/cn/2023.2/Manual/overview-of-dot-net-in-unity.html)
4. **TUFReplay가 측정상 영향을 주면 할당과 경쟁을 줄인다.** 이미 비동기인 저장 흐름은 유지하면서 CSV 중간 버퍼·복사·GC, 저장 완료 콜백, 미디어와 DB 작업의 동시 실행을 조사한다. 임의로 1–2초 지연시키는 수정은 병목을 뒤로 미룰 뿐이다.
5. **줌은 저장과 분리해서 다룬다.** 프레임 정지를 해결한 뒤에도 확대 전환이 남는지 확인한다. 실제 값 기록 없이 사용자 줌 배율을 강제로 초기화하면 기존 편집 배율이나 맵 카메라 의도를 훼손할 수 있다.

코드 수정·빌드·설치·설정 변경은 수행하지 않았다. 현재 확인 가능한 사실은 비동기 저장 구조와 복귀 중 전체 장식 복원 경로이며, 사용자가 경험한 1–2초의 최종 귀속에는 위 비교 또는 구간별 계측이 필요하다.
