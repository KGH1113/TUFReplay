# 카메라 모달·녹화 종료 수정과 성능 계측 — 2026-10-04

`feat/webcam-recording` 전용 워크트리에 요청한 수정을 반영했다. 카메라 기능과 **Show live camera**가 모두 켜져 있을 때만 첫 run 모달을 열고, 게임 시계 앵커 없이 끝난 영상은 카메라 준비 상태를 유지한 채 폐기한다. 이후 카메라 오버레이의 OnGUI를 제거하고 실시간·리플레이 표시 모두 uGUI로 옮겼다. UI 업데이트 및 프레임 복사·업로드 비용은 `Player.log`에 5초 단위로 기록한다.

**모달 동작**

- 카메라 기능이 꺼져 있거나 Show live camera가 꺼져 있으면 에디터 Play와 커스텀 레벨 countdown 모두 모달을 건너뛴다.
- 열린 모달에서 어느 옵션이든 끄면 모달을 닫고 run을 이어간다. 이때 카메라 준비 완료를 기다리지 않는다.
- 옵션이 꺼져 있어서 건너뛴 것은 확인 완료로 저장하지 않는다. 이후 두 옵션을 켠 상태로 run을 시작하면 첫 확인 모달이 나온다.
- 미리보기의 **Click to reveal / 클릭해서 보기** 동작은 유지된다.

[CameraFirstRunCoordinator](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/CameraFirstRunCoordinator.cs:23)의 공통 조건과 진행 처리에 반영했다. 기존 uGUI 프리팹을 사용하므로 이 변경에는 Unity 에디터나 UI 번들 재생성이 필요하지 않다. 사용자 선택과 불필요한 중단을 줄이는 [apple-design 지침](/Users/kgh/.codex/skills/apple-design/SKILL.md), 오류가 생기지 않도록 흐름을 먼저 개선하는 [Toss 오류 메시지 가이드](https://toss.tech/article/21021)를 적용했다.

**Camera unavailable 수정**

기존에는 native helper가 영상을 정상 반환해도 게임 시계 앵커가 없으면 타임라인 적용에서 예외가 발생했다. 공통 오류 처리에서 `_armed=false`로 바꾸어, 입력 없는 중단 run이 카메라 전체 사용 불가 상태로 이어졌다. 이전 로그의 5건이 이 경로였다.

[WebcamCaptureTimeline.TryApplyTo](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Timing/WebcamCaptureTimeline.cs:76)에서 앵커와 캡처 시작 시각을 확인하고, [녹화 종료 처리](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Recording/WebcamRecordingFeature.cs:369)에서 동기화할 수 없는 영상만 폐기한다. 이 경로는 카메라를 disarm하거나 unavailable 알림을 띄우지 않는다. 정상 앵커가 있는 영상은 기존대로 동기화해 다음 저장 단계로 전달한다.

폐기할 때는 `[Camera/Diagnostics]` JSON의 `event`가 `capture.recording.discarded`인 기록이 남는다.

| reason | 의미 |
| --- | --- |
| `no_gameplay_clock_anchor` | 게임 시계 앵커를 얻기 전에 run이 끝남, 또는 타임라인이 없음 |
| `invalid_capture_timestamp` | 영상의 캡처 시작 시각이 유효하지 않음 |

run ID, 타임라인 관측 상태, 당시 카메라 상태도 함께 기록한다. 실제 장치나 native backend 종료 예외의 기존 오류 처리는 유지된다.

**새 성능 로그**

게임의 [Player.log](</Users/kgh/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log>)에서 `[Camera/Diagnostics]`와 `"event":"render.performance"`를 찾으면 된다. 기본적으로 5초마다 출력하며, 종료 시에는 완료된 프레임이 있는 남은 구간을 한 번 더 기록한다. 카메라 기능을 꺼도 기준 비교를 위한 프레임 기록을 계속 남긴다.

[CameraRenderDiagnostics](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/CameraRenderDiagnostics.cs:17)가 게임 스레드에서 수치를 수집하고, [CameraRenderStatistics](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Diagnostics/CameraRenderStatistics.cs)가 게임 프레임별로 집계한다. JSON 직렬화와 로그 쓰기는 별도 작업 큐에서 처리한다. 프레임·포인터 이벤트마다 로그를 출력하거나 통계 객체를 새로 만들지 않는다. 현재 기록은 `schemaVersion=2`, `renderer=ugui`다. 이전 OnGUI 빌드의 schema 1 기록과 구분한다.

아래 경로는 각 기록의 `data` 기준이다. 시간 통계에는 `count`, `totalMs`, `meanMs`, `maxMs`가 있다.

| 필드 | 측정 대상 |
| --- | --- |
| `metrics.windowSeconds`, `metrics.completedFrames` | 완전히 종료된 게임 프레임만 포함한 관측 구간과 프레임 수 |
| `metrics.gameFps` | 단조 시계로 측정한 프레임 간격에서 계산한 게임 FPS |
| `metrics.gameFrame` | 게임 프레임 간격의 평균·최댓값 |
| `metrics.uGui.updates` | 카메라 표면·커서·드래그 종료를 갱신하는 LateUpdate 실행 시간 |
| `metrics.uGui.pointerInput` | uGUI 포인터 누름·드래그·해제 처리 실행 시간 |
| `metrics.uGui.perGameFrame` | 한 게임 프레임 안의 updates와 pointerInput 시간 합계. 호출이 없는 프레임도 포함 |
| `metrics.uGui.perVisibleFrame` | 카메라가 표시된 게임 프레임의 UI 시간 합계 |
| `metrics.uGui.liveVisibleUpdates`, `replayVisibleUpdates`, `hiddenUpdates` | 실시간·리플레이·숨겨진 카메라 업데이트 구분 |
| `metrics.preview.tick` | 실시간 미리보기 Tick 전체 비용 |
| `metrics.preview.readAttempts` | 새 공유 프레임 읽기를 시도한 비용. 새 프레임이 없어 실패한 시도도 포함 |
| `metrics.preview.completedCopies` | 실제 새 프레임을 복사한 시도의 비용 |
| `metrics.preview.textureLoad` | Texture2D.LoadRawTextureData 실행 시간 |
| `metrics.preview.textureApply` | Texture2D.Apply 실행 시간 |
| `metrics.preview.uploadedFrames`, `textureResizes` | 성공한 업로드와 텍스처 크기 변경 횟수 |
| `metrics.preview.lastUploadedWidth`, `lastUploadedHeight` | 마지막 업로드 프레임 크기 |
| `metrics.contextFrames` | 카메라 기능 설정, 실시간 표시 설정, 게임 플레이, 포커스, 확인 모달 상태의 프레임 수 |

기록에는 `targetFrameRate`, `vSyncCount`, `timeScale`, 화면 크기, `stopwatchFrequency`, `measuredThreadId`, `windowEndUtc`도 포함한다. JSON 바깥의 `threadId`는 로그를 쓴 작업 스레드다. 실제 계측 스레드는 `data.measuredThreadId`로 확인한다.

한 게임 프레임의 UI 업데이트와 포인터 입력을 합산하므로 콜백 횟수가 게임 FPS에 섞이지 않는다. 실시간·리플레이 오버레이가 같은 프레임에 업데이트돼도 게임 프레임 수는 한 번만 센다. 카메라 FPS와 게임 FPS를 평균 내는 방식도 사용하지 않는다.

이 로그가 측정하는 값은 **게임 스레드에서 해당 코드가 차지한 경과 시간**이다. 스케줄링 대기나 그래픽 제출 대기를 포함할 수 있다. GPU의 실제 렌더링 시간, 영상 디코딩 전체와 Unity의 Canvas·EventSystem 처리 전체는 이 계측 범위에 들어가지 않는다. gameFrame 간격에는 프레임 제한, VSync, 포커스 변화와 다른 게임 작업의 영향도 들어간다.

중첩된 수치를 더하면 중복 집계된다. preview.tick에는 읽기·복사·텍스처 업로드가 포함되고, completedCopies는 성공한 readAttempts의 일부다. uGui.perGameFrame에는 updates와 pointerInput이 이미 포함되어 있다. 게임 스레드의 계측된 카메라 합계는 `uGui.perGameFrame.totalMs + preview.tick.totalMs`다.

비교할 때는 같은 레벨·카메라 크기·녹화 프리셋·프레임 제한을 유지하고 다음 세 상태의 안정된 구간을 선택하면 된다.

1. 카메라 기능 꺼짐: 게임 기준 FPS.
2. 카메라 기능 켜짐, Show live camera 꺼짐: 캡처·녹화가 있는 상태의 FPS.
3. 둘 다 켜짐: 미리보기 복사·업로드·uGUI 출력이 추가된 상태의 FPS와 단계별 비용.

확인 모달이 열린 구간과 설정을 바꾼 구간은 `contextFrames`로 구분한다. 120 제한, 240 제한, 무제한도 각각 같은 조건에서 비교할 수 있다. `uGui.perGameFrame.meanMs/maxMs`와 `preview.textureApply.meanMs/maxMs`를 게임 프레임 시간과 함께 보면 UI 업데이트 비용과 업로드 대기를 구별하는 데 도움이 된다. native helper의 렌더링 시간은 별도 프로세스의 값이므로 이 uGUI 시간과 직접 더하지 않는다.

**검증과 적용**

- `./scripts/run.sh mod-check` 통과: C# 빌드 오류·경고 0건, Unity/Mono 호환성, 전체 C# 회귀 테스트, macOS helper·native input 검증.
- 실제 WebcamRecordingFeature 종료 경로를 테스트용 backend로 실행해 앵커 없음, 타임라인 없음, 유효하지 않은 시작 시각에서 카메라 준비 유지·임시 파일 삭제·콜백 1회·finalization 정상 종료를 확인했다. 정상 동기화 영상의 전달과 시각 매핑도 확인했다.
- UI 여러 업데이트와 포인터 입력의 게임 프레임 합산, 완료되지 않은 프레임의 다음 구간 보존, 작업 큐에 전달한 스냅샷의 독립성을 검증했다. 계측 카운터의 프레임당 메모리 할당 0바이트도 테스트했다.
- `./scripts/run.sh mod-format check` 통과: 69개 C# 파일 검사.
- `git diff --check` 통과.

게임을 실행하거나 재시작하지 않았고, 실행 중인 게임에서 실측 FPS나 모달 조작을 검증하지는 않았다. 새 로그는 수정된 모드를 설치하고 게임에서 로드한 뒤 수집할 수 있다. 설치에는 전용 워크트리에서 기존 빌드 명령을 사용한다.

```sh
cd /Users/kgh/dev/src/tuf-replay-webcam
./scripts/run.sh build
```

이번 변경에는 companion web 코드나 프리팹 변경이 없으므로 web 재빌드와 Unity UI 번들 재생성은 필요하지 않다.
