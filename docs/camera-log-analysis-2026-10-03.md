# Camera 로그 분석 — 2026-10-03

이 로그에서 반복된 **Camera unavailable은 카메라 장치나 권한 문제가 아니라, 게임 시계 앵커가 없는 중단 run을 카메라 전체 오류로 처리한 버그다.** 검토한 스냅샷에서는 동일한 예외가 5번 발생했다. 매번 native helper의 영상 저장은 성공했고, 이후 C# 동기화 처리에서 예외가 발생하면서 `_armed=false`로 바뀌었다.

**OnGUI 비용을 ms나 FPS 감소량으로 확정할 계측은 이 로그에 없다.** 로그에 있는 helper 렌더링 시간은 별도 프로세스의 미리보기 변환 작업 시간이며, 게임의 OnGUI 실행 시간과 다른 값이다. 따라서 이 자료로 “OnGUI 때문에 120 FPS가 93 FPS가 됐다”는 결론을 내릴 수 없다.

분석 대상은 [Player.log](</Users/kgh/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log>)와 그 로그의 handshake가 가리키는 [native helper 로그](/Users/kgh/Library/Logs/TUFReplay/camera-helper-2026-10-03T10-52-40Z-16560-32CCF5A7.jsonl)다. 게임 로그는 분석 중에도 계속 추가되었다. 아래 5건 집계는 마지막 진단 시각이 UTC 11:18:18.524, 한국 시각 20:18:18.524였던 스냅샷을 기준으로 한다. 설치된 활성 버전 `0.2.0-beta.4`의 DLL도 `ilspycmd`로 확인했다.

**Unavailable이 발생한 경로**

첫 번째 사례의 run ID는 `b8f59509c3d34bedaa34dbc1f4607e4f`다. 아래 시각은 한국 시각이다.

| 시각 / 위치 | 확인한 사실 | 의미 |
| --- | --- | --- |
| 19:59:25.492, Player.log 1963행 | `capture.begin` 시작 | 카메라 녹화 시작 요청 |
| Player.log 1964행 | `Gameplay started. songPosition=` | 이 메시지에는 유효한 곡 위치가 기록되지 않음 |
| Player.log 1976–1977행 | `result=aborted`, `inputs=0`, `hitContexts=0` | 입력 없는 중단 run으로 분류되어 저장 대상에서 제외됨 |
| 19:59:25.868, Player.log 1981행 | helper 응답 `ok=true`, 수신 10프레임, 인코딩 5프레임, 영상 길이 233,333µs | 카메라와 인코더는 약 0.23초 영상을 정상적으로 반환함 |
| 19:59:25.868, Player.log 1982행 | `observations=0`, `unavailableAnchors=0`, `segments=0`, `first=null` | 게임 시계 앵커 관측 자체가 한 번도 실행되지 않음 |
| 19:59:25.869, Player.log 1983행 | `The camera recording has no gameplay clock anchor.` | 영상과 게임 시간을 연결하는 후처리에서 예외 발생 |
| Player.log 1987행 | `armed=false`, 위 예외 메시지를 `error`로 저장 | 이 run의 동기화 실패가 카메라 전체 사용 불가 상태로 전파됨 |

해당 흐름은 다음 코드와 일치한다. 설치된 DLL에도 같은 조건과 상태 변경이 있다.

1. [EndWebcamRun](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Recording/Sessions/RecordingFeatureWebcam.cs:43)은 저장하지 않을 run도 카메라 종료 처리로 보낸다. `persist=false`는 영상의 최종 저장 여부를 정하지만, 종료 과정의 타임라인 적용을 생략하지 않는다.
2. [WebcamRecordingFeature.EndRun](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Recording/WebcamRecordingFeature.cs:369)은 helper가 반환한 영상에 `timeline.ApplyTo(recording)`을 호출한다.
3. [WebcamCaptureTimeline.ApplyTo](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Timing/WebcamCaptureTimeline.cs:76)는 첫 앵커가 없으면 위 예외를 던진다.
4. [SetError](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Recording/WebcamRecordingFeature.cs:625)는 예외 종류를 구분하지 않고 `_armed=false`로 바꾸고 “Camera recording unavailable” 알림을 띄운다. 권한과 선택 장치를 확인하라는 현재 안내도 이 원인에 맞지 않는다.

5건 모두 같은 패턴이었다. 영상 길이는 helper가 반환한 파일의 시간이며, 종료 명령의 처리 시간은 아니다.

| 예외 발생 시각(KST) | Player.log 예외 행 | helper 영상 길이 | 타임라인 관측 | 종료 후 armed |
| --- | --- | --- | --- | --- |
| 19:59:25.869 | 1983 | 0.233초 | 0 | false |
| 20:09:54.098 | 4596 | 0.333초 | 0 | false |
| 20:10:50.785 | 4854 | 0.033초 | 0 | false |
| 20:10:52.531 | 4895 | 0.033초 | 0 | false |
| 20:16:45.882 | 6895 | 3.950초 | 0 | false |

모두 입력 0개의 aborted run이었다. 각각의 helper 종료 응답은 `ok=true`, `authorization=authorized`, `sessionRunning=true`, `lastRuntimeError=null`이었다. 마지막 사례도 95프레임을 인코딩한 뒤 동일한 C# 예외가 발생했다. 이 5건에서 카메라가 영상을 못 보내서 unavailable이 발생했다는 증거는 없다.

첫 오류 직후인 19:59:26.049의 [native health 기록](/Users/kgh/Library/Logs/TUFReplay/camera-helper-2026-10-03T10-52-40Z-16560-32CCF5A7.jsonl:603)에서도 카메라 세션은 실행 중이고 수신 속도는 약 29.99 FPS였다. 이후 다시 준비했을 때 정상 상태로 돌아온 것도 로그에 기록되어 있다.

앵커가 없었던 정확한 게임 상태까지는 이 로그에서 구별할 수 없다. [conductor 후처리](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Recording/Patches/RecordingPatches.cs:43)는 캡처 상태, 포커스, 곡 시작 상태, 유효한 재생 속도, 타임라인 진행 여부 등을 검사한 뒤 앵커 관측을 호출한다. 로그의 `observations=0`과 `unavailableAnchors=0`은 관측 호출이 없었다는 근거다. 어느 조건에서 빠졌는지, 사용자가 어떤 동작으로 중단했는지는 개별적으로 기록되지 않았다.

수정 방향은 명확하다. 저장할 필요가 없는 중단 run과 앵커를 얻기 전에 끝난 run은 영상을 폐기하고 카메라 준비 상태를 유지해야 한다. 실제로 저장할 run의 동기화가 실패해도, 장치 실패와 구분해 해당 run의 영상만 처리해야 한다. 마이크 쪽은 앵커가 없을 때 이미 녹화물을 폐기하는 경로가 있다. 이번 분석에서는 소스 코드를 수정하지 않았다.

**OnGUI와 카메라 성능에서 확인할 수 있는 범위**

인게임 실시간 카메라와 리플레이 영상의 출력은 [WebcamReplayOverlay.OnGUI](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/WebcamReplayOverlay.cs:103)의 `GUI.DrawTextureWithTexCoords`를 사용한다. 드래그와 크기 조절 입력도 이 경로에 있다. 설치된 DLL에서도 확인했다.

카메라 프레임 복사와 GPU 업로드는 [CameraLivePreview.Tick](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/CameraLivePreview.cs:18)에서 새 공유 프레임이 있을 때 `TryCopy → LoadRawTextureData → Apply(false, false)`로 처리한다. OnGUI 호출마다 카메라를 다시 읽거나 프레임을 다시 업로드하는 구조는 아니다. 첫 run 카메라 확인 모달은 uGUI RawImage를 사용한다.

Unity는 한 게임 프레임 안에서도 GUI 이벤트에 따라 OnGUI를 여러 번 호출할 수 있다. `DrawTextureWithTexCoords`의 실제 그리기 경로는 Repaint 이벤트에 제한된다. 따라서 OnGUI 호출 횟수만으로 영상 그리기 비용을 계산하면 부정확하다. [Unity OnGUI 문서](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnGUI.html), [Unity IMGUI 구현](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUI.cs).

첫 오류 이전 구간에서 native helper의 5초 간격 health 기록 80개를 분석했다. 미리보기 요청이 켜져 있고 변환 작업이 수행된 것으로 구별한 56개 표본의 결과는 다음과 같다.

| 지표 | 값 | 해석 |
| --- | --- | --- |
| 카메라 수신 FPS 중앙값 | 30.001 FPS | 해당 구간에서 약 30 FPS 수신 |
| 카메라 수신 FPS 최솟값 | 29.396 FPS | 해당 health 구간의 수신 속도 |
| 표본의 마지막 preview render 시간 중앙값 | 2.419ms | helper 작업 스레드의 경과 시간 |
| 같은 표본 값의 95백분위 | 4.240ms | 5초마다 관측한 마지막 작업 값의 분포 |
| 같은 표본 값의 최댓값 | 4.683ms | 56개 표본 중 최댓값 |
| 누적 preview render 최댓값 | 15.860ms | helper가 누적 기록한 작업 최댓값 |
| 대기 프레임 교체 횟수 | 0 | 이 구간에서는 작업 대기 중 프레임을 교체한 기록 없음 |

이 값은 모든 프레임을 계측한 분포가 아니다. 특히 4.240ms를 “매 프레임 OnGUI 비용의 P95”라고 해석할 수 없다. helper 작업의 경과 시간에는 OS 스케줄링 대기도 포함될 수 있고, 게임 메인 스레드에서 실행한 시간도 아니다. 게임의 프레임 예산에서 2.419ms를 직접 빼면 안 된다.

실제 캡처 소스는 1920×1080, 게임 미리보기는 960×540 RGBA, compact 녹화는 640×360·목표 30 FPS·600kbps로 기록되었다. 미리보기 한 프레임은 2,073,600바이트이고, 초당 30개의 새 프레임을 모두 읽고 업로드하면 픽셀 데이터만 약 62.2MB/s다. 이는 크기로 계산한 처리량이며, GPU 전송 속도나 게임 스레드 처리 시간을 측정한 값은 아니다.

또한 native 초기 설정 로그에는 active format 1280×720을 선택한 것으로 나오지만, 실제 첫 프레임과 이후 상태에는 1920×1080이 기록되어 있다. 요청한 포맷보다 실제 입력이 큰 부분은 추가 확인할 성능 후보지만, 이것만으로 unavailable이나 게임 FPS 저하의 원인이라고 단정할 수 없다.

OnGUI의 비용을 수치로 확인하려면 같은 장면·프레임 제한에서 카메라를 켜고 끈 상태를 비교하면서 다음을 분리해 계측해야 한다.

- 게임 스레드의 공유 프레임 복사 시간과 `LoadRawTextureData/Apply` 시간.
- GUI 이벤트별 OnGUI 실행 시간과 한 게임 프레임 안의 합계.
- 입력·커서 처리 시간, 게임 CPU 프레임 시간, GPU 프레임 시간.
- helper의 변환 작업 및 프레임 도착 지연.

현재 Player.log에는 OnGUI, 프레임 시간, CPU/GPU profiler 기록이 없다. `operation.elapsedMs`나 `helper.command.elapsedMs`도 비동기 녹화 명령의 완료 시간이라 OnGUI 비용으로 사용할 수 없다. 이 분석으로 출력 경로는 확인했지만, OnGUI가 게임 성능 저하를 얼마나 만들었는지는 미확정이다.

로그의 다른 예외도 구분했다. 221행부터 반복된 NullReferenceException은 `AdofaiTweaks ... FloorTextDisplayCameraFollowPatch → scrFloor.OnBecameInvisible` 경로이며, 카메라 OnGUI 스택이 아니다. 첫 중단 뒤의 171.585ms unused-assets unloading도 복귀 시점의 별도 작업이다. 둘 다 상시 카메라 출력 비용의 근거로 사용하지 않았다.

이번 작업은 로그 분석과 설치된 DLL 확인, 이 보고서 작성까지다. 게임을 실행하거나 재시작하지 않았으며, 소스 변경과 빌드는 하지 않았다.
