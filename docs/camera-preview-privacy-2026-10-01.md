# 첫 run 카메라 미리보기 가림

첫 run의 카메라 확인 모달에서 카메라가 켜져 있어도 미리보기는 기본으로 가려진다.
미리보기 영역 전체에 **Click to reveal** 버튼을 표시하며, 한국어에서는
**클릭해서 보기**로 표시한다. 버튼을 클릭하거나 키보드로 활성화하면 미리보기가
보인다. 화면 공유 중 모달이 열리면서 카메라 영상이 바로 노출되는 것을 막기 위한
변경이다.

가림 상태에서는 미리보기 `RawImage`를 비활성화하고 텍스처도 연결하지 않는다.
모달을 닫았다가 다시 열거나 카메라를 껐다 켜면 다시 가려진다. 카메라가 꺼져 있을
때는 기존의 꺼짐 안내를 표시한다. 카메라 캡처의 준비 상태와 run 시작 조건은
유지하며, 이 가림은 확인 모달의 미리보기에 적용된다.

## 변경 파일

- `TUFReplay.Unity/Assets/Scripts/ReplayTimeline/CameraSetupView.cs`: 공개 상태와
  클릭 처리, 가림 중 텍스처 연결 해제, 모달 재진입과 카메라 토글 시 초기화.
- `TUFReplay.Unity/Assets/Editor/CameraSetupPrefabBuilder.cs`: 미리보기 전체를 덮는
  uGUI 버튼과 기본으로 비활성화된 미리보기 이미지.
- `TUFReplay.Unity/Assets/Editor/ReplayTimelineRuntimeBundleBuilder.cs`: 실제 프리팹을
  생성해 미리보기 공개 동작을 검증하는 빌드 검증.
- 카메라 프리팹과 macOS·Windows·Linux UI 번들 갱신, README 동작 설명 갱신.

## 검증

열려 있던 Unity 프로젝트의 **Tools → TUFReplay → Build Runtime UI Bundles**에서
기존 번들 빌드 함수를 실행했다. 다음 동작을 프리팹 인스턴스와 합성 텍스처로 검증했다.

1. 첫 프레임과 반복적인 상태 갱신에서 이미지가 비활성화되고 텍스처가 연결되지 않는다.
2. 공개 버튼을 클릭한 뒤 이미지가 활성화되고 텍스처가 연결된다.
3. 카메라를 껐다 켜거나 모달을 다시 열면 가림 상태로 돌아간다.
4. 모달을 닫으면 미리보기 이미지와 텍스처 연결을 정리한다.

Unity의 공개 동작 검증과 세 플랫폼 번들 빌드가 통과했다.
`./scripts/run.sh mod-check`의 빌드·호환성 검사·C# 테스트와
`./scripts/run.sh mod-format check`도 통과했다. 게임에는 설치하지 않았다.

Unity Play 모드에서 실제 클릭을 확인하려던 중 Mac이 잠겨 화면 조작이 중단되었다.
따라서 실제 포인터 클릭 확인과 Play 모드 종료는 미완료다. 합성 텍스처 검증은
카메라 장치를 열지 않고 수행했다.

게임에 반영하려면 `/Users/kgh/dev/src/tuf-replay-webcam`에서
`./scripts/run.sh build`를 실행하면 된다.
