# 로컬 리플레이 CORS·404 복구

2026-09-10, 브라우저 화면 조작 없이 HTTP로 확인했다.

- 기존 자동 제출 서버 PID 5916은 새 공개 replay API와 E2E CORS 설정이 적용되지 않은 실행 파일이었다. `/_health`는 200이지만 공개 manifest는 Loco 기본 HTML 404였고 CORS 헤더가 없었다.
- 해당 서버를 관리하던 로컬 E2E 실행기 PID 5915를 SIGTERM으로 정상 종료했다.
- `E2E_TUF_TARGET=local E2E_UI_PORT=5175 bun run e2e:dev`로 다시 빌드·실행했다. DB·저장 증거는 초기화하지 않았다.
- run `7736c75a-2477-4a61-a405-68648f0f7d70`의 manifest는 이제 HTTP 200, pass ID 5, 공개 증거 6개를 반환한다.
- Origin `http://127.0.0.1:5176` 요청에는 동일한 `Access-Control-Allow-Origin`이 반환된다. 허용되지 않은 `https://untrusted.invalid`에는 해당 헤더가 없다. 공개 API이므로 비브라우저 요청 자체는 인증 없이 200을 받을 수 있다.
- 자동 제출 API 5151, 테스트 실행기 5152, UI 5175를 다시 실행한 상태다. UI는 새로고침 후 사용한다.

별개로 로컬 TUF level 3072의 `dlLink`는 null이다. 따라서 CORS·manifest 조회 복구가 차트 ZIP 다운로드 및 실제 리플레이 재생 완료를 의미하지 않는다. 차트 다운로드 주소 준비가 추가로 필요하다. 이번 복구에서 해당 레벨 데이터나 해시 검사를 변경하지 않았다.
