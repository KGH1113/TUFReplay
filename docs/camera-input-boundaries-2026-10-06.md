# 카메라 녹화 시작 경계와 에디터 복귀

카메라 녹화를 키 입력 녹화 시작과 같은 시계 경계에서 시작하고, 클리어·실패 후에도 Esc로 에디터에 복귀할 때까지 유지하도록 변경했다. 작업 브랜치는 `feat/webcam-recording`, 워크트리는 `/Users/kgh/dev/src/tuf-replay-webcam`이다.

## 시작

- Countdown/Checkpoint에서 키 입력 녹화를 시작한 뒤 카메라 녹화를 요청한다. `RecordInputTracker.CaptureStartTimestampTicks`가 해당 run의 실제 입력 시작 경계를 제공한다. 포커스가 바뀌어도 이 run 시작 경계는 바뀌지 않는다.
- `WebcamRecordingFeature.BeginRun`과 양쪽 `IWebcamCaptureBackend.BeginAsync`에 시작 경계를 전달한다. 저장 용량 확인과 작업 큐 대기로 인해 인코더 연결이 늦어져도 경계를 다시 찍지 않는다.
- macOS는 Stopwatch 경계를 mach host timestamp로 변환해 helper의 `cameraBegin.startHostTime`에 UInt64로 전달한다. 큰 타임스탬프의 정밀도와 IPC 호출보다 이전인 경계를 보존한다.
- Windows는 준비된 인코더를 연결할 때 같은 Stopwatch 경계를 설정한다. 최신 프레임과 이후 스트림 모두 이 경계보다 앞선 프레임을 제외한다.
- 최신 완성 프레임을 재사용할 수 있지만 입력 시작보다 앞선 프레임은 저장하지 않는다. 첫 프레임의 실제 촬영 시각을 기존 입력 시간축에 매핑한다. 카메라 프레임 간격·장치 전달 지연 때문에 첫 이미지가 경계보다 늦을 수 있으며, 그 시각을 정확히 저장한다.
- Countdown에서 activity draft 준비가 일시적으로 실패했다면 gameplay 시작 때 재시도하되 원래 입력 시작 경계를 유지한다.

기존의 카메라 상시 읽기와 빈 저장기 사전 준비는 유지한다. 게임 스레드에 파일 I/O나 인코더 준비 대기를 추가하지 않는다.

## 종료

- 클리어 후 에디터 복귀까지 계속 녹화하는 기존 동작을 유지한다.
- 실패 시 키 입력과 마이크의 기존 종료 처리가 카메라까지 끝내지 않도록 분리했다. 카메라는 실패 화면에서도 계속 녹화한다.
- Esc에 의해 `scnEditor.SwitchToEditMode`가 호출되면 카메라를 종료한다. 설치된 게임 어셈블리를 ILSpy로 확인해 에디터의 Esc 처리와 이 메서드 호출을 검증했다.
- 실패 run의 저장 여부를 유지하고, 카메라 종료 시 해당 run의 activity 저장 작업을 기다려 영상을 보존한다. 저장되지 않은 run의 영상은 버린다.
- 실패 화면에서 에디터를 거치지 않고 재시도하면 기존 카메라를 이전 run의 ID·저장 작업에 묶어 종료한 뒤 상태를 초기화한다. 새 run은 새 입력 경계로 녹화한다. 세션 정리에서도 동일한 영상을 중복 종료하지 않는다.
- 실패 시 카메라 시간축에 rate 1 구간을 추가해 실패 이후의 대기 시간을 실제 시간으로 매핑한다. 기존 파일 크기·용량 제한과 비활성화·세션 종료 처리는 유지한다.

## 렌더링 연동 시 확인할 사항

`CaptureStartTimestampTicks`는 첫 실제 영상 프레임의 시각이고, `CaptureStartOffsetUs`와 `Timeline[]`는 그 시각과 게임 입력 시간축의 관계다. 인코더 연결 시각으로 오프셋을 대체하면 안 된다.

실패한 run의 카메라 MP4는 이제 `RunRecord.TerminalTimeUs` 뒤까지 이어질 수 있다. 입력·마이크의 실패 종료 동작과 run 결과 시각은 변경하지 않았다. 따라서 실패 이후 영상을 렌더링에 포함할 때는 카메라의 `DurationUs`와 실패 지점의 rate 1 구간을 함께 고려해야 한다. 클리어 후 카메라 꼬리와 기존 timeline 형식도 그대로 사용할 수 있다.

## 검증

`TUFREPLAY_CAMERA_TEST_FFMPEG=/opt/homebrew/bin/ffmpeg ./scripts/run.sh mod-check`가 통과했다.

- macOS helper의 실제 저장·디코딩 self-test, native input 검사, 모드 빌드, 세 어셈블리의 Unity/Mono 호환성 검사, 전체 C# 회귀 검사.
- native 시작 경계 이전 샘플 제외와 경계에 정확히 걸친 샘플 허용.
- UInt64 host timestamp 요청의 정밀도와 Stopwatch↔mach 변환. 지연된 IPC 기준점에서도 같은 시작 경계를 복원.
- 입력 시작 경계가 포커스 전환으로 바뀌지 않고, 입력 종료 후에는 이전 경계를 노출하지 않는지 확인.
- 실제 FFmpeg에 입력 시작 이전 프레임 하나와 정상 프레임 12개를 전달한 뒤, 저장된 MP4에서 정상 12개만 디코딩되는지 확인.
- 마이크 종료 때 카메라 유지, 에디터 종료·직접 재시도 시 단일 finalize, run 초기화 시 이전 실패 상태 정리.
- 실패 이후 카메라 시간축이 게임 pitch가 아닌 실제 시간으로 진행하는지 확인.

게임과 실제 카메라는 실행하지 않았으며, 설치된 모드를 덮어쓰지 않았다. 적용은 이 워크트리에서 `./scripts/run.sh build` 후 게임을 다시 시작하면 된다.
