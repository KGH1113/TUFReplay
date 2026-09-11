# TUF 자동 제출 pass 리플레이 화면 — 2026-09-10

## 최신 수정 — 사용자 디자인 피드백 반영

추가 비교 이미지 피드백: 컨트롤 라벨을 점수 영역과 같은 16px로 맞추고, Pitch 입력·VFX 상태·재생 버튼에 기존 값 표시의 반투명 회색 배경과 3px 모서리를 적용했다. VFX 상태 점은 JSX와 CSS에서 제거했다. 타임라인은 보라색 원형 손잡이 대신 중립색 트랙과 작은 사각 손잡이로 변경했다. 브라우저에서 점수 값과 Pitch의 배경색 일치, 상태 점 0개를 확인했고 lint를 통과했다.

아래 초안의 장식·사이드 패널·단순 화면 확장 설명은 이 절로 대체한다.

- 별도 TUF REPLAY 브랜딩, 궤도 장식, 배경 그리드와 그라데이션, 홍보성 문구를 제거했다. 기존 TUF 색상 변수·`btn-fill-primary`와 평범한 검은 미디어 영역을 사용한다.
- 몰입 모드는 native modal dialog를 body에 portal로 열어 브라우저 viewport 전체를 쓴다. 페이지 배경은 비활성화되고 스크롤을 잠근다. 브라우저 자체의 Fullscreen API는 사용하지 않는다. Esc/종료 버튼으로 복귀하며 포커스와 기존 body overflow를 복원한다.
- 설정은 재생 영역 위에 겹치는 팝오버다. 일반/몰입/모바일에서 플레이어 폭·높이를 변경하지 않는다. 작은 화면에서는 설정 내용 자체를 스크롤한다.
- Playback speed는 Pitch로 교체했다. web-adofai의 `playbackPitchPercent`와 동일하게 기본 100%, 정수 1–1000%다. Enter 또는 입력 종료 시 확정하고 Escape는 미확정 입력을 취소한다. 몰입 모드 전환 시 값을 유지한다.
- **엔진 연결은 여전히 미구현이다.** Pitch를 포함한 값은 현재 React 상태이며 실제 오디오에 적용되지 않는다. 후속 iframe 연결에서는 `setSettings({ playbackPitchPercent })`에 대응시켜야 한다. 이번에는 임의 postMessage 명세나 iframe을 추가하지 않았다.
- 새 파일 `ReplayImmersiveView.jsx`는 모달 수명, `ReplayPitch.jsx`는 Pitch 입력 확정, `replay-immersive.css`는 viewport 레이아웃을 담당한다.

브라우저 검증: 일반 모드 재생 영역 1120×630, 몰입 모드 재생 영역 1280×500(1280×720 viewport). 각 모드에서 설정을 열기 전후 크기가 일치했다. 설정 Escape 후 몰입 모드는 유지되고, 다음 Escape에서 닫히며 진입 버튼으로 포커스가 돌아온다. Pitch 75%도 전환 후 유지된다.

모바일 390×844 viewport에서도 dialog는 390×844이며 재생 영역 390×620이 설정 열기 전후 동일했다. 가로 scrollWidth는 390px다. 수정 후 lint와 development Vite build를 통과했다.

## 이번 변경

대상은 `/Users/kgh/dev/src/t21c-web-frontend`의 자동 제출 상세 화면이다. `submissionSource === 'auto_submission'`일 때만 전용 리플레이 영역을 표시한다. 이전의 문구 없는 빈 박스 요구는 이번 사용자 요청으로 대체됐다. 일반 영상 pass의 미디어 처리는 유지한다.

- TUF의 어두운 배경, 보라색 포인트, 기존 레벨·플레이어 정보를 유지한다.
- 리플레이 영역을 전폭으로 배치하고 점수·판정은 아래에 둔다.
- 처음에는 로드 버튼을 표시한다. 클릭 전에는 재생·타임라인이 비활성화된다.
- 클릭 후에는 컨트롤 미리보기만 열린다. **iframe, 게임 엔진, WebGL, 차트·리플레이 다운로드, postMessage 연결은 없다.**
- 재생·일시정지, 처음으로, 탐색, 배속, 음악·히트사운드 볼륨, VFX, 기본 트랙 모양, 타일 아이콘 설정은 React의 화면 상태만 변경한다.
- 미리보기 타임라인은 0–100%다. 실제 기록의 시간·타일 수로 해석하면 안 된다. 실제 재생 시계나 게임 시뮬레이션은 구현하지 않았다.
- 설정은 플레이어 옆에 열리고 모바일에서는 아래로 내려간다. 확장 버튼은 페이지 안의 영상 영역을 확장하며 브라우저 전체 화면 기능은 아니다.
- pass 변경 시 미리보기 상태를 초기화한다. 설정을 서버에 저장하거나 점수를 변경하지 않는다.

## 파일 역할

프론트엔드 `src/pages/common/Pass/PassDetailPage/` 아래:

| 파일 | 역할 |
| --- | --- |
| `PassMedia.jsx` | source에 따라 영상 또는 리플레이 화면 선택 |
| `replay/PassReplay.jsx` | 미리보기 상태와 리플레이 영역 구성 |
| `replay/ReplayControls.jsx` | 타임라인·재생·배속 컨트롤 표시 |
| `replay/ReplaySettings.jsx` | 감상 설정 표시 |
| `replay/pass-replay.css` | 상세 레이아웃과 리플레이 화면 스타일 |
| `replay/replay-controls.css` | 컨트롤·설정 패널 스타일 |

문구는 기존 `pages.passDetail` 번역 구조의 `replay` 하위 키에 영어·한국어를 추가했다. 나머지 언어는 기존 영어 fallback을 따른다. 기존 React·CSS·react-icons를 사용하고 의존성을 추가하지 않았다.

## 향후 연결 시 참고

`/Users/kgh/dev/src/adofai-web-editor/src/editor-engine/editor-engine.facade.ts`의 공개 경계에는 `play()`, `pause()`, `seek(tileIndex)`, `setSettings()`가 있다. 설정 계약은 `editor-engine-settings.model.ts`에 있다. 실제 timeline은 타일 기준으로 preview/commit을 나누므로 현재 0–100% 미리보기 값을 그대로 엔진 시간으로 전달하면 안 된다.

다음 단계에서 사용자 로드 이후에만 iframe을 생성하고, TUF가 외부 컨트롤을 소유하며 iframe은 렌더링을 맡도록 연결한다. postMessage 명세, 허용 origin, 준비/실패 이벤트, 리플레이·차트 공급 경로는 이번에 확정하거나 구현하지 않았다. 엔진 준비 완료 전 재생을 허용하지 않고, pass 변경/종료 시 엔진 자원을 해제하는 수명 관리도 실제 연결 작업에 포함해야 한다.

## 로컬 확인

이번 작업에서 5176 포트는 web-adofai가 사용 중이므로 TUF 디자인 확인 서버는 **http://127.0.0.1:5177/passes/5**에 띄웠다. 실행 명령은 프론트엔드 디렉터리에서 `npm run dev -- --host 127.0.0.1 --port 5177 --strictPort`다. 실제 운영 배포는 하지 않았다. 기존 로컬 API 3002를 사용한다. 서비스와 포트는 작업 시작 시 다시 확인한다.

2026-09-10 lint·development Vite build 통과. 기존 의존성 eval, 큰 청크, Sentry 토큰 미지정 경고는 남아 있다. 이 결과는 이 날짜의 작업 트리에 대한 기록이다.

브라우저 검증: 로드 전 재생/타임라인 비활성, 로드 후 컨트롤 상태 변경, 키보드 탐색, VFX 빠른 토글과 설정 스위치 동기화, 배속 선택을 확인했다. 로드 후에도 iframe/canvas는 각각 0개다. 390px viewport에서 문서 폭 390px, 플레이어 내부 폭/scrollWidth 356px로 잘림을 수정했고 설정 패널은 재생 영역 아래에 표시된다. 일반 pass #2에서는 기존 영상 영역과 YouTube 링크가 유지된다. 모바일 검증용 임시 HTML은 제거했다.
