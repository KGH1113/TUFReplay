# TUFReplay 성능 수정 — 2026-10-01

공통 수정은 `/Users/kgh/dev/src/tuf-replay-dev`의 `dev`에서 구현·검증한 뒤 [f92367b8](https://github.com/KGH1113/TUFReplay/commit/f92367b8)로 커밋하고 `origin/dev`에 푸시했다. 총 24개 파일을 변경했다. 이어서 `/Users/kgh/dev/src/tuf-replay-webcam`의 `feat/webcam-recording`에 fast-forward로 병합하고 카메라 전용 수정을 적용했다.

병합과 겹치는 기존 변경 파일 6개만 잠시 stash하고 모두 복원했다. 나머지 기존 변경 파일 104개의 합산 해시가 병합 전후 동일한 것을 확인했다. 임시 stash는 복원 확인 후 정리했다. 카메라 전용 수정은 기존 카메라 작업과 함께 미커밋 상태다. dev는 작업 트리가 깨끗하고 원격과 일치한다.

dev에 반영한 공통 수정은 다음과 같다.

| 대상 | 변경과 효과 | 주요 코드 |
| --- | --- | --- |
| 활동 DB 저장 | 세션 생성·종료와 run 저장을 순서가 보장되는 백그라운드 큐로 이동했다. 게임 스레드가 SQLite 잠금과 파일 쓰기를 기다리지 않는다. 잠금 오류는 작업 큐에서 재시도한다. | [RecordingActivityTracker.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Activity/Tracking/RecordingActivityTracker.cs) |
| 리플레이 직렬화 | 게임 상태는 게임 스레드에서 확정하고, 완료된 입력·판정 버퍼를 봉인한 뒤 백그라운드에서 직렬화한다. 다음 시도는 새 버퍼를 사용한다. CSV의 전체 문자열 복사 대신 재사용 가능한 문자 청크로 UTF-8을 만든다. | [RecordingSession.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Recording/Sessions/RecordingSession.cs), [RecordedRunPayload.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Replay/Models/RecordedRunPayload.cs) |
| 저장 완료 처리 | 마이크 보관은 활동 DB의 저장 성공 이후에 확정한다. DB 저장 실패 시 늦게 완료되는 마이크 녹화도 제거한다. 정상 종료와 모드 unload에서는 대기 중인 활동 저장을 최대 5초간 기다린다. | [RecordingFeatureActivity.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Recording/Sessions/RecordingFeatureActivity.cs), [Main.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Composition/Main.cs) |
| 네이티브 입력 | 정상적인 재시도 사이에 입력 소스를 유지한다. 매번 OS 훅을 종료·재시작하던 비용을 제거했다. 모드 비활성화 시 종료 작업은 백그라운드로 넘기고, 이전 시도의 늦은 이벤트는 타임스탬프로 배제한다. | [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Recording/Input/RecordInputTracker.cs) |
| 마이크 캡처 | Unity 마이크 읽기 청크를 250ms에서 20ms로 줄이고, 평상시 한 Tick의 읽기를 최대 4청크로 제한했다. WAV 파일 생성과 헤더 쓰기도 작업 스레드에서 시작한다. 마지막 부분 청크는 종료 시 보존한다. | [UnityMicrophoneCaptureBackend.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Microphone/Capture/UnityMicrophoneCaptureBackend.cs), [Pcm16WavWriter.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Microphone/Processing/Pcm16WavWriter.cs) |
| 마이크 재생 종료 | 게임 오브젝트 정리 후 스트림·prefetch 종료와 임시 파일 삭제는 백그라운드에서 처리한다. 게임 스레드가 오디오 읽기 잠금이나 prefetch 스레드 종료를 기다리지 않는다. | [ReplayMicrophonePlayer.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Microphone/Playback/ReplayMicrophonePlayer.cs) |
| 차트 해시와 보정 상태 | 각 angle의 float를 쓸 때 생기던 임시 배열 할당을 없앴다. 기존 해시 바이트 형식은 유지한다. 보정 상태 확인도 매 프레임 DTO 전체를 복제하지 않는다. | [GameplayChartHashCanonicalWriter.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Activity/Charts/GameplayChartHashCanonicalWriter.cs), [MicrophoneCalibrationState.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Calibration/Models/MicrophoneCalibrationState.cs) |
| 긴 타임라인 | 같은 표시 열의 판정을 합치되 가장 심한 판정을 우선 표시하고 필터를 유지한다. 최대 4,096개 열·16,384개 정점으로 제한해 Unity의 65,000개 정점 제한을 넘지 않는다. 진행 표시 mesh는 폭 변화가 0.5 이상일 때 갱신한다. 실제 입력·판정 기록은 모두 보존한다. | [UIJudgmentMarkerGraphic.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay.Unity/Assets/Scripts/ReplayTimeline/UIJudgmentMarkerGraphic.cs), [UILinearTimelineGraphic.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay.Unity/Assets/Scripts/ReplayTimeline/UILinearTimelineGraphic.cs) |
| 입력 탐색 | 재생 준비 시 눌린 키 체크포인트를 만든다. 정렬된 기록에서는 이진 탐색 후 최대 2,047개 전환만 적용한다. 키 순서, 처음부터 눌린 키, 네이티브 메타데이터를 유지한다. | [ReplayInputScheduler.cs](/Users/kgh/dev/src/tuf-replay-dev/TUFReplay/Replay/NativeInput/ReplayInputScheduler.cs) |

카메라 브랜치에 추가로 반영한 수정은 다음과 같다.

| 대상 | 변경과 효과 | 주요 코드 |
| --- | --- | --- |
| 숨겨진 실시간 미리보기 | 공유 메모리의 표시 요청 플래그로 macOS RGB 렌더링과 Windows YUV→RGB 변환을 생략한다. 카메라 캡처는 계속 유지하고 준비 확인용 첫 프레임은 항상 생성한다. | [CameraPreviewBuffer.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Capture/CameraPreviewBuffer.cs), [CameraPreviewBuffer.swift](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay.MicrophoneCapture.Mac/TUFReplayMicrophoneCapture/Webcam/CameraPreviewBuffer.swift) |
| 미리보기 잠금 | 게임 스레드의 크기 확인·프레임 복사·표시 요청은 생산자 잠금을 기다리지 않는다. 쓰기가 겹치면 해당 갱신을 건너뛰고 기존 텍스처를 유지한다. | [CameraLivePreview.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/CameraLivePreview.cs) |
| 숨겨진 리플레이 영상 | 숨겨진 영상은 준비·디코딩하지 않는다. 다시 표시하면 디코더를 준비하고 현재 리플레이 시각으로 탐색한 뒤 표시한다. 재생 속도는 변경될 때만 설정하며 중복 Tick도 제거했다. `VideoPlayer.Stop`이 준비 상태와 내부 영상 자원을 해제하는 동작은 [Unity 공식 문서](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer.Stop.html)로 확인했다. | [WebcamReplayPlayer.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/WebcamReplayPlayer.cs) |
| Windows 프레임 메모리 | 시도마다 큰 YUV 배열 5개를 새로 만드는 대신 카메라 전용 풀에서 재사용한다. 실제 Unity/Mono 기본 풀이 1MiB로 제한됨을 확인하고, 1080p 프레임도 들어가는 최대 4MiB 버킷을 명시했다. FFmpeg에는 배열 용량 대신 정확한 프레임 바이트 수를 쓴다. 필터 작업 스레드도 2개로 제한했다. | [FfmpegCameraRecording.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Capture/FfmpegCameraRecording.cs), [FfmpegCameraSession.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Capture/FfmpegCameraSession.cs) |
| 영상 보관소 | 재생 lease 해제와 파일 정리는 백그라운드 큐로 넘긴다. 저장·예약 시 용량을 확인하는 중복 디렉터리 스캔을 줄였고, 삭제 대기 판정에서 모든 run ID를 다시 해싱하지 않는다. | [WebcamRecordingStore.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Repositories/WebcamRecordingStore.cs) |
| 카메라 저장 순서 | 활동 DB 저장이 성공한 뒤 영상 보관을 확정한다. 이 대기는 캡처 명령 큐 밖에서 수행해 다음 시도의 카메라 시작을 막지 않는다. DB 저장이나 editor 복귀가 실패하면 영상을 제거한다. | [PendingWebcamDisposition.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Recording/PendingWebcamDisposition.cs), [RecordingFeatureWebcam.cs](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Recording/Sessions/RecordingFeatureWebcam.cs) |

미리보기의 색상 처리, 원본 비율, 프리셋 해상도·비트레이트·30fps 설정은 유지했다. 이번 최적화는 불필요한 작업, 대기, 반복 할당을 줄이는 방식이다.

검증 결과는 다음과 같다.

- dev와 카메라 브랜치 모두 `./scripts/run.sh mod-check` 통과: 모드 빌드, macOS 헬퍼 합성 카메라/오디오 self-test, 네이티브 입력 core test, Unity/Mono 호환성, C# 테스트, 업데이트 테스트 13개.
- 공통 회귀 검사: 잠긴 DB에서 게임 쪽 호출이 기다리지 않는지, 저장·세션 종료 순서, 시도별 데이터 분리, 입력 소스 재사용, 판정 10만 개 표시, 해시 바이트·할당, CSV 청크 경계, 긴 기록의 키 상태 탐색.
- 카메라 회귀 검사: 숨겨진 미리보기의 첫 프레임과 표시 요청, 생산자 잠금 회피, DB 성공/실패에 따른 보관, DB를 기다리지 않는 삭제, 보관소 잠금 중 재생 lease 해제, 1080p 버퍼 재사용.
- 설치된 실제 FFmpeg로 합성 프레임 3개를 H.264 인코딩·디코딩해 풀의 초과 용량이 영상에 섞이지 않는 것을 확인했다. 실제 카메라는 열지 않았다.
- 카메라 브랜치 `./scripts/run.sh web-check` 통과: 테스트 124개, TypeScript, Biome, 프로덕션 빌드. Vite의 큰 청크 경고는 남아 있다.
- shell 문법 검사 통과: dev 30개, 카메라 브랜치 33개. shellcheck는 설치되어 있지 않아 정적 shellcheck 검사는 실행하지 못했다.
- `git diff --check` 통과. 게임에 설치하는 작업은 실행하지 않았다.

실제 게임에서 평균 FPS·프레임 타임 개선량은 아직 측정하지 않았다. 다음 항목은 추가 최적화 판단을 위한 계측 대상이다.

- **Windows 소프트웨어 H.264 인코딩:** `libx264`의 CPU 사용은 남는다. 지원되는 NVENC/QSV/AMF를 검색하고 초기화 실패 시 소프트웨어 인코더로 복귀하는 방식이 다음 후보다. 드라이버별 안정성과 비트레이트·동기 검증이 필요하다.
- **표시 중인 카메라의 CPU→GPU 업로드:** RGBA 복사와 `Texture2D.Apply` 비용은 남는다. 실제 프로파일러에서 업로드 비용을 확인한 뒤 외부 GPU 텍스처 또는 비동기 전송 경로를 검토할 수 있다.
- **게임 판정·타일 상태 복원:** 입력 탐색은 줄였지만, seek 시 과거 판정을 `scrMarginTracker.AddHit`로 다시 반영하고 타일 상태를 복원하는 게임 스레드 작업은 남는다. 게임 상태의 체크포인트와 변경된 구간만 갱신하는 방식이 후보이며, ADOFAI의 누적 상태 계약을 확인해야 한다.
- **차트 전환 시 해시의 전체 순회:** angle별 할당은 제거했지만 해시 계산 자체는 여전히 차트 크기에 비례한다. 차트 수정·재로드 시 확실히 무효화할 수 있는 revision 캐시가 후보다.
- **입력 소스의 오류 복구:** 정상 재시도의 OS 시작/종료 비용은 제거했지만, 소스가 실제로 중단된 경우의 재시작 경로에는 동기 작업이 남는다. 입력 누락과 복구 상태를 검증하면서 별도 시작·종료 작업 큐로 옮기는 것이 후보다.
- **종료 중 저장:** 정상 quit/unload의 대기는 최대 5초다. 강제 종료나 오래 지속되는 DB 잠금까지 보장하려면 별도의 복구 가능한 임시 저장 형식이 필요하다.

게임에서 비교할 때는 카메라 끔, 캡처 켬·미리보기 숨김, 라이브 미리보기 표시, 리플레이 영상 숨김/표시를 같은 레벨·해상도·프리셋으로 비교하는 것이 좋다. 평균 FPS와 함께 p95/p99 프레임 타임, GC 할당·수집, 게임 스레드와 렌더 스레드 시간, 카메라 프로세스의 CPU 사용을 확인하면 다음 병목을 구분할 수 있다.
