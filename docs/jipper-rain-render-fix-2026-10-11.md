# JipperResourcePack 렌더 중 레이닝 표시 수정

## 증상과 원인

키 박스의 눌림 표시는 정상인데 렌더 영상에서 레이닝이 보이지 않는 문제를 조사했다. 입력 전달, 레이닝 생성·갱신 호출, 가상 Stopwatch의 시간 단위는 연결되어 있었다. 설치된 Jipper와 Renderer의 DLL도 앞서 만든 패키지와 일치했다.

원인은 동적으로 생성하는 레이닝의 부모 연결 방식이었다. `Rain`은 새 `RectTransform`을 `SetParent(pool.Transform)`으로 붙인 뒤 `anchoredPosition`의 X/Y와 크기를 설정한다. 한 인자 오버로드는 월드 좌표를 유지하므로, 부모가 이동한 경우 로컬 Z와 회전은 부모 UI의 평면을 따르지 않는다.

Renderer는 캡처할 Canvas를 `ScreenSpaceCamera`로 전환하고 카메라 앞의 평면에 배치한다. 키 박스는 그 전환 전에 생성되므로 Canvas와 함께 이동한다. 레이닝은 전환 후 입력에 따라 생성되므로 기존 생성 방식에서는 월드 원점의 깊이를 유지한다. 캡처 레이어를 지정해도 깊이는 교정되지 않는다.

설치된 게임의 `UnityEngine.CoreModule.dll`을 `ilspycmd`로 확인해 `Transform.SetParent(Transform p)`가 `SetParent(p, worldPositionStays: true)`를 호출함을 검증했다. 같은 계약은 [Unity의 SetParent 문서](https://docs.unity3d.com/ScriptReference/Transform.SetParent.html)에도 명시되어 있다.

## 변경

레이닝을 생성할 때 `SetParent(pool.Transform, false)`로 부모의 로컬 UI 좌표를 따르게 한다. 일반 레이닝과 고스트 레이닝이 같은 생성 경로를 사용하므로 양쪽에 적용된다. 풀에서 재사용할 때도 같은 UI 평면이 유지된다.

이 수정은 JipperResourcePack의 일반 UI 생성 방식을 바로잡는다. TUFReplay 전용 분기, Renderer의 임의 위치 보정, 시간·입력 백엔드 변경은 추가하지 않는다. `impl-dmnote`와 ImplResourcePack은 변경하지 않는다.

작업 브랜치: `/Users/kgh/dev/src/jipper-resourcepack`의 `feat/tufreplay-render-support`.

## 검증과 적용

실제 게임 로그에서도 캡처 직전 카메라의 Z 좌표가 0이었다. Renderer의 Canvas 평면 거리는 최소 1이므로 기존 동적 레이닝은 카메라 평면인 월드 Z=0에 남고, 수정한 레이닝은 부모 Canvas의 깊이를 따르게 된다.

`tests/RainLayout`은 실제 `Rain.cs`와 `RainPool.cs`를 직접 컴파일하고, Unity의 부모 연결 계약을 모델링하는 최소 스텁으로 다음을 검증한다.

- 이동·회전·크기 조절이 적용된 부모 아래에서 일반/고스트 레이닝의 로컬 Z가 0이고 월드 Z는 부모의 평면을 따른다.
- 로컬 회전·크기·앵커와 활성 상태가 유지된다.
- 고스트의 이미지 타입, 스프라이트, 타일 설정이 유지된다.
- 풀로 반환했다가 재사용한 레이닝도 같은 평면과 초기 크기를 유지한다.

수정 전 소스로 테스트를 실행하면 `normal new local depth must remain on its Canvas plane`에서 실패했다. 한 줄 수정 후 같은 테스트가 통과했다. 이 테스트는 좌표와 풀 동작을 검증하며 실제 GPU 픽셀 출력을 대신하지 않는다.

완료한 확인:

- `./scripts/run.sh check`: 회귀 테스트, diff 검사, 셸 문법 검사 통과.
- `./scripts/run.sh install`: 빌드 경고 0개·오류 0개, 패키징과 설치 완료.
- 설치된 프로그램 DLL 6개와 패키지의 해시 일치.
- 설정·카운터·에셋 등 보호 대상 22개 파일의 작업 전후 해시 일치.
- 최초 원본 백업 매니페스트 유지: `/private/tmp/tuf-overlay-validation-originals/jipper`.

최신 JipperResourcePack DLL의 SHA-256:

`5a073a3cea490fd1bc163f7e835150624376d14f64faf13c4c4df3deb98fbd93`

패키지: `/Users/kgh/dev/src/jipper-resourcepack/artifacts/render-support/JipperResourcePack`.

실행 중인 게임에는 이전 DLL이 로드되어 있으므로 게임을 재시작해야 수정이 적용된다. 이 작업에서는 게임을 실행하거나 새 렌더 작업을 시작하지 않았다. 재시작 후 같은 기록을 렌더하여 실제 영상에서 레이닝을 확인하는 단계가 남아 있다.
