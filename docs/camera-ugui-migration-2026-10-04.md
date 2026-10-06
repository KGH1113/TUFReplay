# 카메라 오버레이 uGUI 전환 — 2026-10-04

**실시간·리플레이 카메라 오버레이의 OnGUI를 제거했다.** `feat/webcam-recording` 전용 워크트리에서 화면 표시를 지속적으로 재사용하는 uGUI Canvas·RawImage로 옮겼다. 컴파일된 DLL을 ILSpy로 확인해 카메라 클래스에 OnGUI, GUI.DrawTexture, GUIUtility와 Event.current 호출이 없는 것도 검증했다.

[카메라 오버레이](/Users/kgh/dev/src/tuf-replay-webcam/TUFReplay/Webcam/Playback/WebcamReplayOverlay.cs)는 기존 미리보기 텍스처나 VideoPlayer 텍스처를 RawImage에 연결한다. 새 프레임이 도착해도 같은 텍스처와 사각형을 사용하며, 텍스처·크롭 UV·위치·크기 값이 달라질 때만 해당 UI 속성을 갱신한다. 화면 좌표를 그대로 쓰며 크롭의 비율과 미러링을 적용한다.

드래그와 크기 조절은 게임의 uGUI EventSystem 포인터 이벤트로 처리한다. 가장자리의 잡기 범위는 영상 크기를 바꾸지 않고 바깥으로 8픽셀 확장한다. 영상을 벗어나도 드래그가 이어지고, 해제·포커스 상실·숨김·화면 크기 변경 시 변경된 위치와 크기를 저장한다. 이동·가장자리 크기 조절 커서, 화면 밖으로 이동, 리플레이 중 위치 저장도 같은 동작을 사용한다. 숨겨진 Canvas와 raycaster는 비활성화된다.

첫 run의 **Click to reveal / 클릭해서 보기** 모달은 기존 uGUI 프리팹을 사용한다. 새 오버레이는 런타임에 생성하므로 이번 변경을 적용할 때 Unity UI 번들을 다시 만들 필요는 없다.

성능 로그는 `render.performance`, **schemaVersion=2**, **renderer=ugui**다. `metrics.uGui.updates`에서 화면·커서 갱신, `pointerInput`에서 포인터 처리, `perGameFrame`에서 한 게임 프레임의 합계를 확인할 수 있다. 복사·업로드 계측과 카메라를 끈 기준 FPS 기록도 유지한다. 필드는 [성능 로그 안내](/Users/kgh/dev/src/tuf-replay-webcam/docs/camera-runtime-fixes-and-profiling-2026-10-04.md)에 정리했다.

검증 결과:

- `./scripts/run.sh mod-check` 통과: C# 빌드 오류·경고 0건, Unity/Mono 호환성 검사, 전체 C# 회귀 검사, macOS helper·native input 자체 검사.
- 크롭·비율·화면 밖 위치·가장자리 크기 조절·제스처 종료의 기존 회귀 검사 통과.
- uGUI 업데이트와 포인터 입력 합산, 구간 사이의 미완료 프레임 보존, 스냅샷 독립성, 계측 카운터의 프레임당 할당 0바이트 검사 통과.
- `./scripts/run.sh mod-format check` 통과: C# 파일 69개.
- `git diff --check` 통과.
- 실제 게임에 포함된 UnityEngine.UI를 ILSpy로 확인해 RawImage의 UV 처리, raycastPadding, 포인터 드래그·해제 계약을 적용했다.

실행 중인 게임을 재시작하거나 새 모드를 설치하지 않았다. 실제 게임에서 uGUI 전환 후 FPS와 포인터 조작은 아직 실측하지 않았다. 적용은 전용 워크트리에서 다음 명령으로 빌드·설치한 뒤 게임에서 새 DLL을 로드하면 된다.

```sh
cd /Users/kgh/dev/src/tuf-replay-webcam
./scripts/run.sh build
```

이전 [성능 분석 보고서](/Users/kgh/dev/src/tuf-replay-webcam/docs/camera-performance-analysis-2026-10-04.md)는 OnGUI 빌드의 실행 결과다. uGUI 전환 후의 성능 결과로 해석하면 안 된다. 새 계측은 게임 스레드의 카메라 코드 시간을 나타내며 GPU·영상 디코딩·Canvas 전체 비용을 측정하는 값은 아니다.
