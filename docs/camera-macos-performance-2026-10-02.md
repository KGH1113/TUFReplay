# macOS 게임 모드와 카메라 프레임 처리

전용 워크트리 `/Users/kgh/dev/src/tuf-replay-webcam`, 브랜치 `feat/webcam-recording`에서 카메라 미리보기의 병목을 수정했다.

사용자가 렉이 심해지는 대상은 **게임 안 실시간 카메라**라고 확인했다. 수정한 경로는 이 게임 내 미리보기 경로이며, 브라우저의 직접 카메라 미리보기는 해당 프레임 복사와 헬퍼 렌더를 사용하지 않는다.

게임 모드가 비게임 앱의 FPS를 직접 제한하는지에 대한 추가 확인은 [게임 모드 제한 확인 기록](./camera-game-mode-throttling-2026-10-02.md)에 정리했다. 앞선 최적화는 게임 모드의 프로세스 제한을 면제하거나 실제 카메라 30fps를 보장하는 조치는 아니다.

## 재빌드 후 설치 상태 확인

사용자는 카메라 표시 시 120fps 제한에서 약 93fps로 내려가고, 240fps 제한이나 무제한에서는 실제 240fps 이상을 표시하며 같은 저하가 없다고 보고했다. 이후 재빌드하자 문제가 고쳐진 것 같다고 보고했다.

재빌드 전 설치된 `TUFReplay.dll`을 역컴파일한 결과 미리보기 복사가 여전히 `_view.ReadArray(64L, destination, 0, destination.Length)`였으며, 설치된 macOS 헬퍼에도 `CameraPreviewWorker` 타입이 없었다. 앞선 최적화가 설치본에 반영되지 않은 상태였다.

사용자가 재빌드한 뒤 설치 상태를 다시 확인했다. 현재 런타임 `0.2.0-beta.4`의 DLL에는 `AcquirePointer`와 `Marshal.Copy` 복사가 들어 있고, 기존 `_view.ReadArray` 호출은 없다. macOS 헬퍼에도 `CameraPreviewWorker`가 포함되어 있다. 설치 파일의 수정 시각은 2026-10-02 14:27 KST다. 이 설치는 사용자가 실행한 재빌드로 적용됐으며, 에이전트는 설치 파일을 읽어 확인했다.

설치된 ImplResourcePack의 FPS 계산도 직접 확인했다. `StatusTimeTicker.Update()`가 `Stopwatch` 시각을 `FrameRateSampler`에 기록하고, 최근 1초의 `(프레임 수 - 1) / 경과 시간`을 계산한다. 카메라 FPS를 입력받거나 `(게임 FPS + 카메라 FPS) / 2`로 계산하는 경로는 이 측정 코드에 없다. 90에 가까운 표시값만으로 두 FPS의 산술 평균이라고 판단할 수 없다.

따라서 지금까지의 근거는 **기존 설치본에 남아 있던 미리보기 처리 병목이 재빌드 후 개선됐음**을 지지한다. 120fps에서만 보이는 차이가 프레임 제한 타이밍과 어떻게 상호작용했는지는 실제 프레임 시간 추적 없이 확정하지 않는다. 사용자에게서 재빌드 후 개선 보고를 받았으므로 추가적인 프레임 제한기 변경은 하지 않았다. 게임 모드의 FPS 제한을 입증한 결과로 해석하지 않는다.

## 확인한 원인과 아직 확인하지 못한 부분

설치된 게임의 `Managed/mscorlib.dll`을 `ilspycmd`로 확인했다. 기존 `CameraPreviewBuffer.TryCopy`의 `MemoryMappedViewAccessor.ReadArray<byte>`는 `UnmanagedMemoryAccessor.ReadArray<T>`와 `SafeBuffer.ReadArray<T>`를 거쳐 원소마다 `Buffer.Memmove`를 호출한다. 960 × 540 RGBA 프레임을 게임 스레드에서 읽을 때 1바이트 복사를 2,073,600번 호출하고, 최대 960 × 720에서는 2,764,800번 호출하는 경로였다. .NET 8에서 실행하는 기존 테스트만으로는 Unity의 Mono에서 발생하는 이 비용이 드러나지 않았다.

macOS 헬퍼는 같은 AVFoundation 프레임 콜백에서 미리보기를 Core Image로 렌더링한 뒤에 녹화 프레임을 AVAssetWriter에 전달하고 있었다. 미리보기 렌더가 지연되면 녹화 콜백도 지연되는 구조였다. [Apple의 AVFoundation 가이드](https://developer.apple.com/library/archive/technotes/tn2445/_index.html)는 프레임 콜백이 오래 걸리거나 카메라 버퍼를 오래 보유하면 프레임이 누락될 수 있다고 설명한다.

[Apple의 게임 모드 설명](https://support.apple.com/en-mide/105118)에 따르면 게임 모드는 게임에 CPU·GPU 우선권을 주고 백그라운드 작업의 자원 사용을 낮춘다. 따라서 별도 헬퍼의 GPU 미리보기 렌더가 영향을 받을 수 있다는 판단으로 처리 경로를 바꿨다. 이 세션에서는 게임과 카메라 헬퍼가 실행 중이지 않았고, 기존 Player.log에도 카메라 처리 시간이나 프레임 누락 수가 없었다. 실제 게임 모드가 어느 단계에 얼마나 영향을 주는지는 아직 측정하지 않았다.

설치된 ImplResourcePack의 FPS 구현도 확인했다. `StatusTimeTicker.Update()`가 매 프레임 `Stopwatch` 타임스탬프를 기록하고, `FrameRateSampler`가 최근 1초의 `(프레임 수 - 1) / 경과 시간`을 계산한다. 카메라 FPS나 게임 timeScale을 표시하는 것이 아니라 게임 Update 간격을 측정한다. 표시값만의 문제로 단정할 근거는 없다. CPU와 GPU 각각의 프레임 시간을 분리하는 측정치는 아니다.

## 변경 내용

- C# 프레임 읽기를 `SafeMemoryMappedViewHandle.AcquirePointer`와 `Marshal.Copy`를 사용하는 한 번의 메모리 복사로 교체했다. `PointerOffset`과 64바이트 헤더를 반영하고, `finally`에서 포인터를 해제한다. 기존의 크기 확인·시퀀스 검사·중복 프레임 건너뛰기·Dispose 보호·대기하지 않는 읽기는 유지한다.
- macOS 미리보기 렌더를 AVFoundation 캡처·녹화 콜백에서 분리했다. 별도 작업자는 렌더 중인 프레임과 최신 대기 프레임만 보유하며, 느려지면 오래된 대기 프레임을 교체한다. 녹화는 미리보기 렌더가 끝날 때까지 기다리지 않는다.
- Core Image의 [CPU 렌더링 옵션](https://developer.apple.com/documentation/coreimage/cicontextoption/usesoftwarerenderer)을 사용해 미리보기 변환이 게임의 GPU 작업을 기다리는 경로를 제거했다. 캡처 콜백과 미리보기 작업자에 `userInitiated` QoS를 명시했다.
- 비율·960 × 720 미리보기 상한·30fps·Lanczos 축소·NV12 색 변환·녹화 프리셋·동기화 기준은 유지한다. 화질이나 프레임 목표를 낮추는 변경은 없다.

## 검증

`./scripts/run.sh camera-copy-bench 100`으로 Unity Editor에 포함된 독립 Mono 6.13 arm64 CLI에서 두 메모리 복사 경로를 비교했다. 각각 10프레임 워밍업 후 100프레임을 측정하고 실행 순서를 번갈아 바꿨으며, 결과의 전체 바이트 일치를 확인했다.

| 공유 메모리 복사 경로 | 960 × 540 평균 / p95 | 960 × 720 평균 / p95 |
| --- | ---: | ---: |
| 기존 `ReadArray<byte>` | 6.745ms / 7.582ms | 8.834ms / 9.268ms |
| 수정 후 `AcquirePointer` + `Marshal.Copy` | 0.028ms / 0.035ms | 0.035ms / 0.041ms |

게임과 Editor의 Mono 런타임 및 corlib 바이너리는 서로 다르다. 두 corlib에 같은 원소별 복사 경로가 있는 것은 확인했지만, 위 수치는 **독립 합성 벤치마크의 프레임 복사 시간**이며 실제 게임 FPS 개선량은 아니다. 텍스처 업로드·GPU 표시 비용도 포함하지 않는다.

`TUFREPLAY_CAMERA_PREVIEW_BENCHMARK=1 ./scripts/run.sh mac-helper`로 합성 NV12 1280 × 720 프레임을 RGBA 960 × 540으로 렌더했다. 10프레임 워밍업 이후 90프레임을 측정했으며 게임 모드는 활성화하지 않았다.

| 렌더 경로 | 평균 | p95 | 최대 |
| --- | ---: | ---: | ---: |
| 기존 기본 Core Image 경로 | 0.627ms | 0.731ms | 0.814ms |
| 수정 후 CPU 경로 | 1.256ms | 2.003ms | 2.143ms |
| 전체 mod-check에서 CPU 재검사 | 1.210ms | 1.613ms | 3.643ms |

일반 환경에서 CPU 경로의 렌더 자체는 더 많은 시간을 쓰지만, 30fps의 33.3ms 프레임 예산 안에서 처리했고 미리보기 GPU 대기와 녹화 콜백의 연결을 제거했다. 위 수치는 합성 프레임을 사용한 이 Mac의 결과이며 실제 게임 FPS 개선량은 아니다.

- `./scripts/run.sh mod-check`: macOS 헬퍼·native input·모드 빌드·Unity/Mono 호환성·C# 회귀 검사와 13개 updater 테스트 통과. C# 빌드는 경고와 오류가 없다. 게임 설치는 생략됐다.
- 기존 카메라 색감·영상 레벨·YCbCr 매트릭스·방향·원본 비율·세로 영상·축소 품질·30fps 타이밍·숨김 시 렌더 생략·H.264 인코딩 검사를 통과했다.
- 렌더를 의도적으로 멈춘 작업자 테스트에서 99개 새 샘플을 제출해도 캡처 제출이 렌더 완료를 기다리지 않고 최신 샘플 하나만 처리하는지 확인했다. 작업자 종료 시 대기 샘플과 이후 제출이 처리되지 않는지도 확인했다.
- 최대 960 × 720 프레임의 전체 바이트 일치, 잘못된 목적지 크기 후 재시도, 중복 프레임 차단, 반복 복사의 프레임당 관리 메모리 할당 0, Dispose 이후 포인터 접근 차단을 검사했다.
- `./scripts/run.sh check`: 셸 스크립트 34개의 문법 검사 통과. shellcheck는 설치되어 있지 않아 실행하지 못했다.
- `./scripts/run.sh mod-format check`: C# 파일 65개 통과. `git diff --check`도 통과했다.

게임을 실행하거나 모드를 설치하지 않았다. 실제 게임 모드 전후 비교와 물리 카메라의 지속 캡처 검증은 남아 있다. 적용은 이 워크트리에서 `./scripts/run.sh build`로 한다. 확인할 때는 같은 레벨·같은 녹화 프리셋으로 카메라 끔, 캡처만 켬, 게임 내 미리보기까지 켬을 각각 게임 모드 끔·켬에서 비교하면 캡처와 표시 비용을 구분할 수 있다.
