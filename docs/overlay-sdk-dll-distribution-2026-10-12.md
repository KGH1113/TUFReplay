# SDK DLL 배포와 외부 모드 참조 수정

SDK를 별도 DLL 배포물로 제공하고, JipperResourcePack과 ImplResourcePack은 각 저장소에 동봉된 DLL만 참조하도록 수정했다. 두 모드의 빌드에는 Renderer 저장소나 SDK 소스 프로젝트가 필요하지 않다.

## 배포물

- [SDK 1.0.0 ZIP](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.Overlay-1.0.0.zip)
- [ZIP 체크섬](/Users/kgh/dev/src/TUFReplay-Renderer/Release/TUFReplay.Overlay-1.0.0.zip.sha256)
- [공용 연동 가이드](/Users/kgh/dev/src/TUFReplay-Renderer/sdk/README.md)

ZIP에는 `TUFReplay.Overlay.dll`, XML API 문서, 공용 연동 가이드, 라이선스 파일, 파일별 `SHA256SUMS`가 들어 있다. 외부 개발자는 ZIP을 풀어 확인한 DLL을 자신의 프로젝트에 참조하고 모드 패키지에 동봉하면 된다.

SDK 관리자는 Renderer 모노레포에서 `./scripts/run.sh overlay-sdk-package`로 버전이 붙은 독립 ZIP을 생성한다. SDK 소스와 Renderer 내부의 프로젝트 참조는 모노레포에 유지된다. 현재 배포 ZIP은 로컬에 생성했으며 GitHub 릴리스에는 업로드하지 않았다.

## 외부 모드의 변경

| 모드 | 빌드 참조 | 기본 위치 | 브랜치 |
| --- | --- | --- | --- |
| JipperResourcePack | `Reference`와 `HintPath`, `Private=true` | `libs/TUFReplay.Overlay.dll` | `feat/tufreplay-render-support` |
| ImplResourcePack | `Reference`와 `HintPath`, `Private=true` | `libs/TUFReplay.Overlay.dll` | `feat/tufreplay-overlay-support` |

각 `libs/`에는 같은 SDK DLL, XML 문서, 라이선스, 버전과 해시를 기록한 README를 넣었다. DLL은 `.gitignore`에서 제외하지 않도록 명시하여 소스 변경과 함께 커밋할 수 있다. 기본 빌드·테스트·패키지 경로에 Renderer의 SDK 소스나 빌드 출력 경로가 남아 있지 않다.

필요하면 `OVERLAY_SDK_PATH`로 다른 SDK DLL 경로를 지정할 수 있다. DLL이 없을 때는 어떤 파일을 어디에 넣어야 하는지 안내하고 실패한다. SDK 소스 프로젝트를 자동으로 찾아 빌드하는 경로는 없다.

SDK 갱신은 새 ZIP의 `SHA256SUMS`를 확인하고 `libs/`의 DLL·XML·라이선스를 교체한 뒤 버전과 해시 기록을 갱신하는 방식이다. 모드 개발자가 SDK를 빌드할 필요는 없다.

## 검증

Renderer 형제 디렉터리가 존재하지 않는 `/private/tmp/tuf-overlay-dll-check-20261012` 아래에 빌드 캐시와 로컬 설정 없이 검증 복사본을 만들었다. 각 복사본의 `libs/` DLL을 기본값으로 사용했다.

- Jipper `./scripts/run.sh package`: 빌드와 패키징 통과. 기존 R136 API의 obsolete 경고 1개, 오류 0개.
- Jipper `./scripts/run.sh check`: 일반/고스트 레이닝 생성과 풀 재사용 회귀 검사 통과.
- Impl `./scripts/run.sh mod-check`: 경고 0개·오류 0개, 기존 회귀 검사 17개 통과.
- NuGet 복원 프로젝트 그래프에도 SDK 소스 프로젝트가 포함되지 않음을 확인했다. Impl의 모드 그래프에는 Impl 프로젝트만, Jipper에는 기존 모드와 VersionSafe 프로젝트만 있었다.
- DLL이 없는 경로를 지정한 두 모드는 예상대로 실패하고 DLL 배치 안내를 출력했다.
- SDK `overlay-sdk-check`: 14개 검사와 Mono의 양방향 DLL 로드·공용 레지스트리 검사 2개 통과.
- SDK ZIP 무결성과 파일별 체크섬 확인 통과.
- 원래 두 모드 작업 디렉터리에서도 최종 패키지를 재생성하고 SDK DLL의 해시 일치를 확인했다.
- Renderer와 Impl의 셸 문법 검사, 세 저장소의 `git diff --check` 통과. `shellcheck`가 설치되어 있지 않아 셸 검사는 문법 검사만 수행했다.

SDK 원본, 두 모드의 `libs/`와 최종 모드 패키지에 포함된 SDK의 SHA-256은 모두 같다.

`5e6fd3a3175491dcfccade410a3e2dab4cf6c71605cac47e166266af0be578c0`

`impl-dmnote`와 해당 IPC·미러 경로는 수정하지 않았다. 이번 변경은 SDK 배포와 빌드 참조 경계에 한정되며, SDK 런타임 구현과 DLL 바이트는 기존 버전 그대로다. 게임에 파일을 설치하거나 새 렌더 작업을 실행하지 않았다.
