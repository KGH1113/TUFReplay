# 카메라 녹화 시작 지연 개선

요청한 방식으로 구현했다. 카메라 입력은 계속 읽고, 영상 저장기를 run 전에 미리 준비해 둔다. run 시작 시 저장기를 연결하고, 종료 시 연결을 끊은 뒤 다음 저장기를 준비한다. 대기 중 카메라 영상은 파일에 누적하지 않는다.

작업 위치는 `/Users/kgh/dev/src/tuf-replay-webcam`, 브랜치는 `feat/webcam-recording`이다.

## 지연이 생기던 단계

기존 구현도 카메라 장치를 run마다 다시 열지는 않았다. 저장기와 인코더는 run이 시작된 뒤 생성했고, 이미 도착한 프레임을 버린 뒤 다음 프레임을 기다렸다. 리플레이에서도 영상 표시 구간에 도달한 뒤 디코더를 준비할 수 있었다.

기존 로그 `~/Library/Logs/TUFReplay/camera-helper-2026-10-03T18-46-13Z-46603-94A6C5F0.jsonl`에서 첫 run의 저장기 준비는 약 107.6ms, 첫 프레임 저장까지는 약 173.3ms였다. 이후 run은 준비에 약 7.2~12.0ms, 첫 프레임 저장까지 약 78.9~101.5ms였다. 이는 변경 전 실제 장치의 측정값이다.

## 저장기 준비와 연결

- **macOS:** AVFoundation 캡처 세션을 유지하면서 별도 작업 큐에서 빈 AVAssetWriter 하나를 준비한다. run이 시작되면 준비된 저장기를 가져와 가장 최근의 완성된 카메라 샘플부터 전달한다. 종료할 때 기존 저장기를 분리하고, 이전 파일의 마무리를 기다리는 동안 다음 저장기를 준비한다.
- **Windows:** DirectShow 캡처 프로세스는 계속 유지한다. 별도 FFmpeg 인코더 프로세스를 미리 시작하고, run이 시작된 뒤에만 프레임을 보낸다. 입력용 버퍼와 최신 프레임용 버퍼 두 개를 번갈아 사용하므로 대기 중 프레임마다 추가 복사나 메모리 할당을 하지 않는다.
- **대기 영상:** 최신 프레임 한 장만 연결용으로 유지한다. 오래된 프레임은 교체하며, 대기 중에는 프레임을 인코더에 넣지 않는다. 저장기 준비용 임시 파일이 존재할 수 있지만 대기 영상이 저장되지는 않는다.
- **파일 정리:** 준비용 파일에는 고유한 `.mp4.partial` 이름을 사용한다. 녹화가 정상 종료된 뒤 해당 run의 임시 경로로 이동한다. 사용하지 않은 저장기는 카메라 비활성화 시 취소하고 정리한다. 기존 저장 용량 제한과 보존 정책을 사용한다.

AVAssetWriter의 출력 URL은 읽기 전용이므로 준비된 저장기의 파일 경로를 run마다 교체할 수 없다. 따라서 고유한 임시 목적지를 준비해 두고, 완료된 파일을 run 경로로 옮긴다. 저장기 준비와 영상 시간축 시작은 분리했다. [Apple의 outputURL 문서](https://developer.apple.com/documentation/avfoundation/avassetwriter/outputurl), [startWriting 문서](https://developer.apple.com/documentation/avfoundation/avassetwriter/startwriting%28%29?changes=_8), [startSession 문서](https://developer.apple.com/documentation/avfoundation/avassetwriter/startsession%28atsourcetime%3A%29?changes=_9).

## 싱크와 재생 시작

연결에 재사용할 프레임은 시작 경계보다 앞서 촬영됐고 250ms 이내에 도착한 최신 프레임으로 제한한다. 없거나 오래됐으면 다음 정상 프레임부터 저장한다. macOS에서는 샘플의 원래 촬영 시각과 native host timestamp를 유지하고, Windows에서는 기존 캡처의 단조 시계 타임스탬프를 유지한다. 영상 시작 시각을 임의로 앞당겨 표시 지연을 감추지 않는다.

첫 프레임이 게임 시계 기준으로 60ms 앞서 촬영됐다면 영상 오프셋은 −60ms다. 리플레이 시각이 0이면 영상의 60ms 지점을 재생한다. 이 정렬과 기존 일시정지·재생 속도 변경·추가 보정값 처리는 회귀 검사에 포함했다.

리플레이 카메라 표시가 활성화돼 있으면 녹화 파일을 확보할 때 `VideoPlayer.Prepare()`를 호출한다. 영상 시작 구간보다 앞에서도 첫 프레임으로 이동할 수 있게 해서 표시 시점에 디코더 준비를 시작하던 지연을 줄인다. 영상은 해당 시각에 도달한 뒤 표시한다. 표시 옵션을 끄면 디코더를 정지하고, 다시 켜면 현재 리플레이 시각에 맞춰 준비한다.

처음 카메라를 켤 때 필요한 장치·저장기 준비 시간, 첫 프레임 인코딩 비용, 매우 빠른 재시도 중 준비가 아직 진행 중인 경우의 대기는 남을 수 있다. 이번 변경 이후 실제 카메라의 시작 지연과 게임 FPS는 새 빌드로 플레이한 로그에서 측정해야 한다.

## 추가 로그

| 이벤트 / 필드 | 확인할 내용 |
| --- | --- |
| `recording.prepare.complete` | 다음 저장기 준비 완료와 소요 시간 |
| `recording.prepare.failed` | 준비 단계의 실패 원인 |
| `recording.writer.started.preparedWriterUsed` | macOS에서 사전 준비된 저장기를 사용했는지 |
| `recording.writer.started.attachElapsedMs` | macOS run 연결에 걸린 시간 |
| `recording.first-frame.reusedLatestSample` | macOS 최신 샘플 재사용 여부 |
| `recording.first-frame.boundaryDelayMs` | 첫 촬영 시각과 요청 경계의 차이. 재사용한 프레임은 음수일 수 있음 |
| `standbyWriterPresent`, `preparingWriter`, `preparationError` | native 상태 스냅샷에서 준비 상태와 오류 |
| `playback.prepare.begin`, `playback.prepare.complete` | 리플레이 디코더 준비 시점과 소요 시간 |
| `playback.seek.begin`, `playback.seek.complete` | 재생 시각으로 이동한 과정과 소요 시간 |
| `playback.first-frame-ready`, `playback.first-display` | 첫 디코딩 프레임과 첫 표시 요청 시점 |

프레임마다 로그를 출력하지 않는다. 재생 단계 로그의 직렬화와 출력은 작업 큐에서 처리한다. `first-display`는 텍스처가 있는 상태에서 표시를 요청한 시점이며 GPU의 실제 화면 출력 완료 측정값은 아니다.

## 검증 결과

다음 검사들이 통과했다.

- `./scripts/run.sh mod-check`: macOS helper 빌드와 self-test, native input 검사, 모드 빌드, 세 어셈블리의 Unity/Mono 호환성 검사, 전체 C# 회귀 검사.
- macOS 합성 영상: 미리 준비한 실제 저장기에 12개 프레임을 연결해 MP4로 저장하고, 12개 모두 디코딩. 대기 구간 제외, 녹화 목적지 이동, 미사용 저장기 정리, 오래되거나 잘못된 프레임 거부, 준비용 디렉터리 명령 전달 검사.
- 최신 프레임 버퍼: 입력 버퍼와 최신 프레임의 소유권 분리, 두 버퍼 재사용 중 할당 0, 비활성화 시 프레임 해제.
- `TUFREPLAY_CAMERA_TEST_FFMPEG=/opt/homebrew/bin/ffmpeg ./scripts/run.sh mod-check`: 실제 FFmpeg 인코더에 합성 프레임 12개를 연결해 저장·디코딩하고, 정확히 12개만 재생되는지와 미사용 저장기의 정리를 확인. 실제 DirectShow 장치는 열지 않았다.
- `./scripts/run.sh mod-format check`: 변경 C# 파일 70개 통과.
- `git diff --check`: 통과.

실제 카메라 장치, Windows DirectShow 환경, 게임 안에서의 `VideoPlayer` 시작 지연은 이번 자동 검사로 측정하지 않았다. 게임을 실행하거나 설치된 모드에 새 산출물을 덮어쓰지는 않았다.

## 적용

전용 워크트리에서 빌드하고 새 DLL과 helper를 적용한 뒤 게임을 다시 시작하면 된다.

```sh
cd /Users/kgh/dev/src/tuf-replay-webcam
./scripts/run.sh build
```

다음 플레이 로그에서 `preparedWriterUsed`, `attachElapsedMs`, `reusedLatestSample`과 재생 준비 로그를 보면 저장 시작과 디코더 준비 중 어느 단계에서 대기가 남는지 구분할 수 있다.
