# 키뷰어·오버레이 구현 및 검증 기록

> 아래 내용은 이전 구현의 검증 기록입니다. 후속 직접 수정, 폰트 업로드, 실제 서버 제출 및 연속 재생 검증 결과는 [최신 로컬 E2E 기록](visual-presets-local-e2e-2026-09-16.md)을 기준으로 확인하세요. 아래 5187 고정 프레임 대신 최신 5190 플레이어를 사용합니다.

## 이전 구현 상태

서버, 모드, companion web과 TUF 프런트엔드의 저장·선택·전달 경로를 구현하고 각 검증을 마쳤습니다. web-adofai 관련 파일 25개도 원본 저장소에 통합하고 staging과 내용이 동일한지, production build가 통과하는지 확인했습니다. 실제 Asgore 기록에 Jipper 기본 오버레이와 DMNote numpad를 적용한 게임 화면을 30초 시점에서 확인했습니다. 아래 검증 한계는 남아 있으므로 전체 재생 수명주기나 모든 원본 모드 설정의 동등성을 검증한 상태는 아닙니다.

- [로컬 데모 열기](http://127.0.0.1:5187/tests/fixtures/webgpu/real-asgore/real-gameplay-fixture.html)
- [실제 게임 화면 캡처](/private/tmp/tuf-visuals-20260916/test-artifacts/asgore-gameplay-final-verified.png)

데모 서버는 화면 확인을 위해 실행 상태로 남겼습니다. 이 페이지는 실제 `ReplayViewerRuntimeController`를 사용하는 테스트용 고정 프레임입니다.

제품 동작과 데이터 계약은 [visual-presets-contract-2026-09-16.md](visual-presets-contract-2026-09-16.md), 조사한 버전과 출처는 [visual-presets-source-baselines-2026-09-16.md](visual-presets-source-baselines-2026-09-16.md)에 기록했습니다.

## 구현한 경로

- 키뷰어와 오버레이를 별개로 등록하고, 제출마다 각 종류를 최대 하나 선택합니다. 선택하지 않은 종류는 표시하지 않습니다.
- 등록 당시 설정과 에셋을 묶어서 저장합니다. 같은 설정을 다시 등록해도 별도 프리셋으로 생성하며 이름의 중복을 검사합니다.
- 제출 선택을 고정하고 재시도에서도 보존합니다. 삭제한 프리셋은 과거 제출을 다시 로드할 때 빈 슬롯으로 처리합니다.
- 모드의 기존 로그인과 IPC를 통해 등록·조회·삭제합니다. 웹에 별도 인증 흐름을 추가하지 않았습니다.
- v3 manifest와 iframe protocol 2로 프리셋 번들을 전달합니다. 기존 v2 manifest 및 구형 플레이어 경로는 유지합니다.
- 클리어 후 에디터로 돌아올 때까지 입력을 기록합니다. 클리어 판정과 제출 키 수는 클리어 시점 기준으로 유지합니다.
- DMNote 여러 탭 입력은 거부하고, JS 플러그인과 소리는 제외합니다. 참조 에셋 누락과 허용 경로 밖의 파일 접근은 차단합니다.

## 완료된 검증

| 대상 | 검증 결과 |
| --- | --- |
| TUFReplay 모드 | `./scripts/run.sh mod-check` 통과. 빌드, native/Unity Mono, visual/submission 및 updater 테스트 포함. 게임 설치는 하지 않음. |
| 서버 | 라이브러리 34개, 통합 18개 통과. fmt/check 및 clippy all-targets 경고 오류 처리 통과. |
| Companion web | 테스트 139개, 타입 검사, Biome, production build 통과. |
| Companion web 실제 브라우저 | 로컬 mock에서 Jipper 키뷰어/오버레이 등록, 빈 기본 선택, 독립 선택, 제출 및 재시도 시 갤러리 생략 확인. 콘솔 오류·경고 0개. 추가 mock 회귀 테스트 4개 통과. |
| TUF 프런트엔드 원본 작업 폴더 | replay 테스트 11개/검증 30개, lint 및 production build 통과. |
| 실제 DMNote 입력 | 사용자가 선택한 numpad 단일 탭 사본이 모드와 서버를 통과. 렌더러 컴파일 시 키 슬롯 20개와 폰트 에셋 1개 확인. |
| web-adofai | staging 전체 테스트 901개 통과/1개 skip, 타입 검사 통과. 현재 판정 매핑 수정 후 관련 테스트 27개 통과. 원본 저장소 통합 후 production build 통과. |
| 렌더러 독립 리뷰 | 반복 입력 탐색, DMNote active 색상, 노트 설정, idle 불투명도 문제 4건 수정 확인. 실제 numpad의 idle/active alpha 0.5/0.9와 노트 300px·450px/s 확인. |
| Asgore 실제 런타임 화면 | 30초 시점의 차트·행성과 DMNote 키 그리드, Jipper 판정·콤보·진행률·통계가 함께 표시됨을 캡처로 확인. 브라우저 콘솔 오류 없음. |

서버 통합 검증은 별도 PostgreSQL/Redis 인스턴스에서 실행했으며, 검증 종료 후 두 인스턴스를 종료했습니다. 사용자 게임 데이터베이스는 읽기 전용으로 조회했습니다.

## 실제 재생 샘플

- 맵: Asgore
- run ID: `f689436ba19b4f4090dc704b8b785de2`
- 시작: `2026-09-15T11:24:06.9589930Z`
- 결과: 클리어, Too Early 3개
- 입력 이벤트: 11,896개, hit context: 5,271개
- 키뷰어: 제공된 DMNote JSON의 `numpad` (`custom-1782579510617`) 탭만 추출한 테스트 사본
- 오버레이: Jipper 기본 설정

첫 실제 로드에서 현재 ModernCompetitive 기록의 미드스핀 판정 값 `14`를 web-adofai 파서가 거부하는 문제가 확인되었습니다. 원본 기록을 변조하지 않고 판정 체계별 매핑을 수정했습니다. 후속 WebGPU 빈 화면은 카메라 이동으로 표시 범위가 어긋난 문제를 수정하여 해결했습니다. 실제 프리셋 화면과 GPU readback의 유효 픽셀을 확인했고, 첫 프레임 전에 파이프라인을 준비하도록 했습니다.

## 남은 검증 및 한계

- 실제 Asgore 데모는 30초 시점의 결정적 고정 프레임 검증입니다. 연속 애니메이션, 오디오 재생, 전체 기록 종료까지의 브라우저 재생은 이번 데모에서 검증하지 않았습니다.
- 데모는 DMNote 번들을 직접 전달하므로 replay-package URL 검증과 번들 폰트의 asset resolver를 통과하는 브라우저 경로는 이 캡처로 검증되지 않습니다. 별도의 parser/package/전달 테스트와 구분해야 합니다.
- companion web의 실제 모드 IPC 연결 및 지연 응답 중 계정 전환은 브라우저 mock 검증 범위에 포함하지 않았습니다. 계정별 캐시와 지연 응답은 별도 테스트로 확인했습니다.
- 게임 안에서 등록과 클리어 후 입력 기록 수명주기 확인은 자동 테스트와 별개로 남아 있습니다.
- 선언적 CSS는 지원하는 paint/text 속성 범위에서 적용합니다. DMNote의 일부 glow gradient와 고급 이미지 레이어 동작은 공통 Canvas 모델에서 근사하며, 픽셀 단위 동등성을 검증한 것은 아닙니다.

현재까지 배포, 게임 설치, git commit은 하지 않았습니다. 실제 fixture 파일은 임시 작업 폴더에만 두며 사용자 원본 JSON/DB를 수정하지 않았습니다.
