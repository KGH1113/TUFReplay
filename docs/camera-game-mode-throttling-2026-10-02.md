# 게임 모드의 비게임 앱 제한 확인

확인일: 2026-10-02. 대상은 macOS에서 게임 모드를 켰을 때 버벅이는 **게임 안 실시간 카메라**다.

게임 모드가 카메라 헬퍼에 영향을 줄 수 있다는 가설에는 근거가 있다. Apple은 게임 모드가 게임에 CPU·GPU 우선권을 주고 백그라운드 작업의 자원 사용을 낮춘다고 설명한다. 다만 공개 자료에서 **모든 비게임 앱의 FPS를 특정 값으로 고정하는 정책**, 또는 **AVFoundation 카메라 캡처를 특정 FPS로 제한하는 정책**은 확인하지 못했다. 그런 제한이 없다는 것을 증명한 것은 아니다.

[Apple 지원 문서](https://support.apple.com/en-mide/105118)와 [WWDC25 게임 성능 설명](https://developer.apple.com/videos/play/wwdc2025/209/) 모두 백그라운드 활동을 줄여 게임에 더 많은 CPU 시간을 제공한다고 설명한다. 따라서 백그라운드에서 실행되는 별도 카메라 프로세스가 처리 시간을 덜 받아 실제 프레임 전달·변환 속도가 떨어질 가능성이 있다. 이 부분은 공식 자원 우선순위 설명과 현재 앱 구조에 근거한 추론이다.

현재 TUFReplay의 카메라는 게임 프로세스 밖의 `TUFReplayMicrophoneCapture.app`에서 AVFoundation으로 캡처한다. 헬퍼는 `LSUIElement=YES`인 앱이고, 독립적인 미리보기 창을 그리지 않는다. 미리보기 이미지는 공유 메모리로 게임에 전달하며 Unity가 게임 안에서 표시한다.

소스 확인 결과:

- 카메라의 지원 모드에서 `activeVideoMinFrameDuration`과 `activeVideoMaxFrameDuration`을 1/30초로 요청한다. 미리보기도 최대 30fps를 기준으로 동작한다. 게임 모드를 감지해 목표 FPS를 낮추는 코드는 없다. 목표 FPS가 실제 전달 속도를 보장하는 것은 아니다.
- 프레임은 AVFoundation 콜백에서 받는다. 헬퍼의 카메라 처리에는 `CADisplayLink`, `CVDisplayLink`, `NSTimer` 기반 UI 프레임 루프가 없다. 다른 앱의 창이나 애니메이션 갱신이 느려지는 현상만으로 원본 캡처의 속도 제한을 확정할 수 없다.
- `alwaysDiscardsLateVideoFrames=true`를 사용한다. 콜백이 제때 처리되지 않으면 프레임이 누락될 수 있다. [Apple의 프레임 누락 가이드](https://developer.apple.com/library/archive/technotes/tn2445/_index.html)는 느린 프레임 처리와 카메라 버퍼의 장시간 보유가 누락을 일으킬 수 있다고 설명한다.
- 캡처 큐와 미리보기 작업자에는 `userInitiated` QoS를 지정했다. 미리보기는 CPU에서 처리하고, 녹화 콜백이 렌더 완료를 기다리지 않도록 분리했다. 이것은 작업의 비용과 대기를 줄이는 조치다. **게임 모드의 프로세스 제한을 면제하거나 카메라 30fps를 보장하는 조치는 아니다.**

App Nap도 구분해서 확인해야 한다. [Apple의 App Nap 문서](https://developer.apple.com/library/archive/documentation/Performance/Conceptual/power_efficiency_guidelines_osx/AppNap.html)는 비활성 앱의 CPU 사용과 타이머 실행 빈도를 줄일 수 있다고 설명한다. 이는 게임 모드와 별개의 정책이다. 현재 카메라 코드에는 `ProcessInfo.beginActivity`를 통한 앱 수준 활동 선언이 없다. 그러나 AVFoundation이나 시스템이 내부적으로 활동 선언을 유지하는지까지 확인한 것은 아니므로, 선언이 없다는 사실만으로 실제 App Nap 상태라고 판단할 수 없다.

[Apple의 앱 수준 작업 우선순위 가이드](https://developer.apple.com/library/archive/documentation/Performance/Conceptual/power_efficiency_guidelines_osx/PrioritizeWorkAtTheAppLevel.html)는 녹음처럼 사용자가 요청한 장시간 작업을 `beginActivity`와 `endActivity`로 선언해 App Nap에 들어가지 않도록 시스템에 알릴 수 있다고 설명한다. 이 API도 게임 모드의 모든 제한에서 면제됨을 보장하는 수단으로 문서화되어 있지는 않다. 이번 확인에서는 이 API를 추가하거나 시스템 설정을 바꾸지 않았다.

`LSSupportsGameMode` 설정도 헬퍼의 제한 면제 옵션으로 볼 근거는 없다. [Apple의 해당 키 설명](https://developer.apple.com/documentation/bundleresources/information-property-list/lssupportsgamemode)은 그 앱을 실행할 때 게임 모드를 사용할 수 있는지를 지정하는 용도로 설명한다.

현재 점검 시 게임과 카메라 헬퍼가 실행 중이지 않아 실제 게임 모드 켬·끔 상태의 전달 속도나 App Nap 상태는 측정하지 않았다. 게임을 새로 실행하거나 카메라에 접근하지 않았고, 소스 코드는 변경하지 않았다.

원인을 확정하려면 같은 게임·카메라 설정에서 게임 모드만 바꾸며 아래 지표를 비교해야 한다.

| 관찰 지표 | 확인할 수 있는 부분 |
| --- | --- |
| 원본 샘플 PTS 간격과 실제 콜백 도착 간격 | 카메라 원본의 프레임 간격과 헬퍼에 전달되는 시점의 지연 |
| AVFoundation `didDrop` 횟수와 이유 | 늦은 처리, 버퍼 부족, 캡처 불연속에 따른 누락 |
| 미리보기 변환 시간과 공유 메모리 발행 속도 | 헬퍼의 CPU 처리와 최신 프레임 교체 속도 |
| Unity 프레임 수신·업로드 속도와 게임 Update 간격 | 게임 스레드의 복사·표시 병목과 실제 게임 FPS |
| Activity Monitor의 헬퍼 App Nap 상태 | App Nap에 실제로 들어갔는지 여부 |

앞선 합성 벤치마크는 프레임 복사 병목과 미리보기 처리 비용을 확인한 결과다. 게임 모드 제한이 실제로 해소됐다는 결과는 아니다. [앞선 수정과 검증 기록](./camera-macos-performance-2026-10-02.md)에 전후 수치와 한계를 기록했다.
