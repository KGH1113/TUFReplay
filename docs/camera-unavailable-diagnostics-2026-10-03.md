# Camera unavailable 상세 진단 로그

카메라가 unavailable로 바뀌는 원인을 구분할 수 있도록 모드와 macOS helper에 상세 로그를 추가했다. 기본으로 켜져 있으며 별도 설정은 필요 없다. 이번 변경은 진단 정보를 남기는 작업이며, 실제 unavailable 원인을 해결했다고 판단한 것은 아니다.

적용하려면 `/Users/kgh/dev/src/tuf-replay-webcam`에서 `./scripts/run.sh build`를 실행하고 게임을 완전히 종료한 뒤 다시 열어야 한다. 기존에 실행 중인 모드와 helper에는 새 코드가 적용되지 않는다. 검증에는 설치를 생략하는 `mod-check`를 사용했다.

## 로그 위치

- 게임: `/Users/kgh/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log`
- macOS helper: `/Users/kgh/Library/Logs/TUFReplay/camera-helper-*.jsonl`

게임 로그에서 `[Camera/Diagnostics]`를 검색하면 된다. `helper.handshake`에는 **실제로 연결된 helper의 PID와 정확한 진단 로그 경로**가 들어 있다. 모드의 assembly ID, helper 실행 파일 수정 시각, 실제 실행된 앱 경로도 기록하므로 이전 빌드가 실행 중인지 확인할 수 있다. `.previous.jsonl`은 해당 helper 로그가 회전하기 직전의 파일이다. `selfTest: true`인 항목은 빌드 중 합성 검사에서 생성한 로그다.

다음에 unavailable이 발생하면 해당 게임 로그와 `helper.handshake`가 가리키는 helper 로그를 함께 보면 된다. 문제 직후 파일을 확인하면 최초 실패와 그 이후 상태를 연결하기 쉽다.

## 기록되는 정보

게임에서는 작업별 ID, 작업명, run ID, 대기 시간, 실행 시간, capture generation, 활성화·준비·녹화·저장 상태, 선택한 장치 ID, 프리셋, 저장 용량 제한과 예외의 타입·HResult·내부 예외·스택을 남긴다. 작업명이 `capture.arm`, `capture.begin`, `capture.end`, `devices.refresh`, `storage.persist` 중 무엇인지에 따라 준비·녹화·장치 조회·저장 실패를 구분할 수 있다.

helper 실행 과정에는 실행 파일 존재 여부, `/usr/bin/open`의 종료 상태와 stderr, 연결 제한 시간, 연결된 PID, 프로토콜 버전, handshake 일치 여부, 명령의 요청과 응답 및 처리 시간이 포함된다. 인증 토큰은 기록하지 않는다. 카메라 명령이 실패하면 응답에 helper 상태와 네이티브 오류도 포함되어 게임 로그에서 확인할 수 있다.

macOS helper에는 다음 정보를 남긴다.

- 카메라 권한의 초기 상태, 요청 표시, 허용 여부, 대기 시간 초과와 최종 상태.
- 요청한 장치 ID, 실제 발견한 장치 목록, 기본 장치, 선택한 장치의 이름·ID·종류·모델, 연결·사용 중·일시 중지 상태.
- 원래 카메라 포맷, 적용한 크기와 픽셀 포맷, 지원 FPS 범위, 후보 포맷 수, 실제 프레임 간격과 녹화 출력 설정.
- AVFoundation 입력 생성, 입력/출력 추가, 설정 잠금, 미리보기 공유 메모리 연결, `startRunning`의 시작과 완료. 실패 시 마지막 `phase`로 중단 지점을 확인한다.
- 세션 시작·종료·중단·재개·런타임 오류, 장치 연결·분리 알림, 제공된 notification userInfo, NSError의 domain·code·설명·userInfo·내부 오류.
- 첫 수신 프레임, 실제 전달된 프레임 크기·픽셀 포맷의 변화, 첫 녹화 프레임, 타임스탬프·시계 변환 실패, 인코더가 받지 못한 프레임, append 실패, 용량 제한 및 MP4 저장 결과.

캡처가 켜져 있으면 **5초마다** `capture.health`를 남긴다. 수신·드롭·인코딩 프레임 수, 드롭 사유별 누적 수, 최근 수신 FPS, 마지막 프레임 이후 경과 시간, 최대 수신 간격, 샘플 시각과 도착 시각의 차이, 인코더 상태, 미리보기 대기/교체/완료 횟수, 진행 중 렌더의 경과 시간, 공유 프레임의 sequence·크기·요청 여부를 포함한다. 반복되는 드롭/인코더 지연은 최초 발생과 누적 요약으로 기록한다.

첫 프레임이 10초 안에 도착하지 않으면 `capture.first-frame.timeout`에 게임 측 공유 메모리 헤더를 남기고, helper의 현재 상태도 요청해 게임 로그에 붙인다. 실제 카메라 수신이 0인지, 미리보기 작업자가 멈췄는지, 공유 프레임은 발행됐는지 구분할 수 있다.

녹화 종료 시 `capture.timeline.apply`에는 게임 시계 앵커 관측 수, 소스 앵커를 얻지 못한 횟수, 유효하지 않은 속도/타임스탬프, 뒤로 간 게임 시간, 첫/마지막 앵커와 구간 수를 남긴다. 따라서 영상 프레임은 정상적으로 기록됐는데 게임 시계와의 동기화 때문에 녹화가 제외된 경우도 구분할 수 있다.

macOS는 iOS 전용 `AVCaptureSessionInterruptionReasonKey`를 제공한다고 보장할 수 없으므로, 숫자 코드를 임의로 추정하지 않고 알림의 원본 userInfo와 네이티브 오류를 보존한다. 프레임 드롭 사유는 AVFoundation이 제공하는 `FrameWasLate`, `OutOfBuffers`, `Discontinuity` 등의 값을 기록한다. [Apple 프레임 드롭 문서](https://developer.apple.com/library/archive/technotes/tn2445/_index.html), [AVCaptureSession 런타임 오류 문서](https://developer.apple.com/documentation/avfoundation/avcapturesession/runtimeerrornotification).

## 성능과 보관

프레임마다 로그 파일을 쓰지 않는다. 프레임 콜백에서는 숫자 카운터만 갱신하고 첫 발생 및 상태 전환에 필요한 작은 이벤트를 보낸다. JSON 생성, 파일 기록, 로그 회전은 별도 utility queue에서 처리한다. 게임 프레임마다 JSON을 생성하거나 로그 파일을 쓰는 코드도 추가하지 않았다.

네이티브 로그는 파일당 2 MiB에서 회전하며 이전 파일 하나를 유지한다. 디렉터리에는 최대 12개의 카메라 진단 로그를 보관한다. 카메라 픽셀이나 영상·오디오 내용은 로그에 포함하지 않는다. 로그 파일 기록 실패가 캡처 예외로 번지지 않도록 분리했다.

## 검증

`./scripts/run.sh mod-check`의 macOS helper 합성 검사, 네이티브 입력 검사, 모드 빌드, Unity/Mono 호환성과 C# 검사가 모두 통과했다. 네이티브 진단 검사에는 JSON 줄 형식, 중첩 NSError 보존, 로그 회전·크기·파일 권한, 드롭/수신 통계, 멈춘 미리보기 작업자 상태, 진단 응답의 큰 타임스탬프 정밀도가 포함된다. C# 검사에는 예외의 내부 원인, 해제된 공유 메모리의 안전한 상태 조회, 게임 시계 앵커 누락 진단이 포함된다. `./scripts/run.sh mod-format check`는 변경된 C# 66개 파일을 통과했고 `git diff --check`도 통과했다.

실제 카메라와 게임은 검증 과정에서 실행하지 않았다. 실제 재현 원인은 새 빌드를 적용한 뒤 수집되는 로그로 확인해야 한다.
