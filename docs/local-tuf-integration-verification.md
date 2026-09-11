# 로컬 TUF 제출 연결 검증 — 2026-09-09

## 실행 중인 환경

- Submission Lab: http://127.0.0.1:5175/
- TUF 프론트엔드: http://127.0.0.1:5176/
- TUF API: http://127.0.0.1:3002/
- Rust 자동 제출 API: http://127.0.0.1:5151/
- TUF CDC: http://127.0.0.1:3990/health
- 로컬 fixture 아이콘: http://127.0.0.1:3003/icons/p1.svg
- 전용 MySQL: 127.0.0.1:3307, `tuf_web_test`
- 전용 TUF Redis: 127.0.0.1:6380
- 전용 Elasticsearch: 127.0.0.1:9201

재시작·토큰 갱신 명령은 [E2E README](../tools/auto-submission-e2e/README.md)의 로컬 TUF 절에 있다. 로컬 테스트 OAuth 토큰은 60분간 유효하다. 운영 DB·서비스에 pass 등록이나 마이그레이션을 하지 않았다.

## 실제 제출 결과

| 시나리오 | Rust run | 최종 상태 | 실제 TUF pass |
|---|---|---|---|
| 브라우저에서 업로드 후 수동 제출 | `7fb57791-4b9d-4ae6-9ecb-2891690c9f71` | submitted | [#4](http://127.0.0.1:5176/passes/4) |
| TUF 저장 후 응답 유실 | `7736c75a-2477-4a61-a405-68648f0f7d70` | submitted | [#5](http://127.0.0.1:5176/passes/5) |
| 검증기 미준비 | `9c3c281f-70e9-457c-bbc2-5959891818d6` | validator_unavailable | 없음 |

원본 fixture는 The Limit Does Not Exist, level ID 3072다. 첫 실행에서 입력 2,762개·hit context 1,360개를 전송했고 마지막 ACK는 2,901, 청크 수는 2,902, 전송 바이트는 177,522였다.

TUF MySQL에서 pass #4와 #5의 `submissionSource=auto_submission`, run UUID 연결, `videoLink=NULL`, `keyCount=9`, Perfect 1,360개를 확인했다. TUF가 계산한 `scoreV2=902.759`, `accuracy=1`이며 화면은 902.76점·100%다. 등급 P1·파일 ID·판정 결과는 테스트 fixture 값이므로 실제 난이도 점수나 안티치트 검증 결과로 해석하면 안 된다.

run별 pass와 `auto_submission_receipts`는 각각 1개다. #5의 등록 요청을 다시 전송해도 같은 pass ID를 반환했다. 검증기 미준비 실행에는 등록 요청 자체가 없었다. CDC 실행 후 실제 등록 결과가 검색 목록에 자동 반영되는 것도 브라우저에서 확인했다.

## 프론트엔드와 백엔드 변경

- 프론트엔드 main은 `08909d72`, 백엔드 master는 `1780cd1f`로 fast-forward했다.
- 원래 백엔드 변경 27개 파일은 최신화 전 백업과 바이트 단위로 일치한다. 백업은 `/private/tmp/tuf-backend-before-pass-ui-20260909.tar.gz`, patch와 stash도 유지한다.
- 공통 카드·상세의 자동 제출 표시를 공유하고 한국어 `자동 제출`, 영어 `Auto-submitted` 번역을 추가했다. 다른 언어는 기존 영어 fallback을 따른다.
- 자동 제출은 영상 URL이 있어도 영상 정보 조회·iframe·영상 링크 없이 빈 박스를 렌더링한다.
- 플레이어별 기록의 제한된 DB projection에 `submissionSource`를 포함했다. 검색 문서·상세·목록은 기존 직렬화를 통해 필드를 보존한다.

## 검증

- 프론트엔드 lint·Vite build 통과. 빌드의 기존 큰 청크·의존성 eval·Sentry 토큰 미지정 경고는 남아 있다. Sentry 업로드는 실행하지 않았다.
- 백엔드 타입 검사·보안 lint 통과, 기존 자동 제출 인증·등록 스키마 테스트 4개 통과.
- Lab 단위 테스트 5개·타입 검사·빌드 통과.
- 실제 React 컴포넌트를 브라우저에 렌더링하고 HTTP adapter를 통제한 검사 8개 통과: 자동 제출 영상 조회 0회, 빈 박스, 단독 배지, 일반 영상 조회, 늦은 응답 차단, source 누락 시 조회·iframe 유지, 자동 제출 전환 시 iframe 제거.
- 실제 pass 목록·상세에서 배지와 영상 링크 부재 확인. 일반 영상 pass에서는 기존 영상 없음 안내와 YouTube 링크를 유지한다. 통제된 검사에서는 정상 metadata에 대한 iframe 렌더링도 확인했다.
- 390px 실제 iframe viewport에서 모바일 박스는 358 × 201.375px(16:9), 자식 0개, 문서 폭 390px로 가로 넘침 없음. 데스크톱은 기존 flex 레이아웃의 높이를 유지한다.

배포 시 기존 `1788742800_auto_submission_receipts.cjs`와 `1788800000_auto_submission_pass_source.cjs` 마이그레이션이 필요하다. 과거 자동 제출 검색 문서에 source가 빠져 있다면 passes 재색인 또는 해당 문서 갱신이 필요하다. 이번에는 전용 로컬 검색 인덱스만 생성·갱신했다.

## 추가 수정: /levels 오류와 아이콘

로컬 fixture에는 P1만 있어서 `LevelPage`가 Q 난이도 검색 결과의 `.icon`을 읽다가 예외를 던졌다. 이를 우회하던 Q 아이콘 fallback과 U20 필터 초기화 변경은 모두 원복했다. `LevelPage.jsx`는 upstream HEAD와 diff가 없다.

대신 Git에서 제외된 `cache/web-test-icons.ts`로 로컬 DB에 P1·U20·Qq·UQ4를 준비하고, `cache/web-test-assets.ts`에서 해당 글자의 테스트 SVG를 제공한다. ID·순서·점수는 테스트 값이며 공식 난이도 카탈로그가 아니다. TUF API 재시작으로 난이도 캐시 해시를 갱신했다.

원복된 `/levels`에서 2개 결과와 Q 슬라이더 토글을 확인했다. 필터 초기화 후 P1–U20, Qq–UQ4 범위가 표시됐고 깨진 이미지 수는 0개였다.

이후 사용자 요청으로 임시 카탈로그를 실제 공개 TUF API의 난이도 91개로 교체했다. `cache/web-test-icons.ts`는 `https://api.tuforums.com/v2/database/difficulties`를 GET으로 읽어 원래 ID·정렬·색상·점수·아이콘 URL을 로컬 DB에 반영한다. 따라서 위 임시 SVG와 범위는 교체 전 검증 기록이며 현재 아이콘은 TUF CDN의 원본을 사용한다. P1의 실제 ID도 1이라 기존 테스트 level/pass 연결은 유지된다. 이미 저장된 pass 점수를 재계산하지는 않았다.

재시작 중 04:09–04:10 UTC의 재플레이 요청은 TUF identity API 연결 실패로 `identity_service_unavailable` 및 HTTP 500을 반환했다. 로컬 API 복구와 만료된 60분 테스트 OAuth 토큰 갱신, Lab 재시작 후 run `73fcb913-7cde-4e05-b972-523fecdb775e`는 청크 2,902개·177,522바이트·최종 ACK 2,901로 업로드되고 로컬 pass #6에 제출 완료됐다. 해당 실행 로그에는 오류가 없다.
