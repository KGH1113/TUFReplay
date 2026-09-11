# 로컬 TUF 공개 데이터 보완

2026-09-10. 실제 TUF 공개 API `https://api.tuforums.com`을 GET으로 조회하고 로컬 `127.0.0.1:3307/tuf_web_test` DB만 수정했다. 운영 데이터 쓰기·브라우저 조작은 하지 않았다.

## 반영 결과

레벨 3072, The Limit Does Not Exist:

| 항목 | 이전 | 공식 값으로 반영 |
|---|---|---|
| 난이도 | 임시 P1 | P16, diffId 16 |
| artist | Local replay fixture | cardboard box |
| fileId | e2e-3514c7a709173d32 | e2d5dea3-15b6-4409-adbd-d6198a1e43c6 |
| dlLink | NULL | https://api.tuforums.com/cdn/e2d5dea3-15b6-4409-adbd-d6198a1e43c6 |
| BPM | NULL | 260 |
| 타일 수 | NULL | 1360 |
| 길이 | NULL | 113764.95744247428 ms |
| baseScore / ppBaseScore | 100 / NULL | 0 / 0 (공개 API 원값) |

공식 workshopLink와 레벨 videoLink도 채웠다. 기존 pass 점수나 validation·증거는 재계산·변경하지 않았다.

난이도 카탈로그는 이미 91개가 있었고 공식 카탈로그와 비교해 누락된 ID 0개, 아이콘 URL 차이 0개여서 중복 반영하지 않았다. 로컬의 인공 테스트 레벨 ID 1은 공식 multi_arm과 동일한 차트라는 근거가 없으므로 숫자 ID만 보고 덮어쓰지 않았다.

## 검증과 보존

- 관련 로컬 API 캐시 3개 무효화 후 GET `/v2/database/levels/3072`에서 P16, 공식 fileId·dlLink, BPM·타일 수 확인.
- 공식 다운로드 주소 HEAD: 200, `Access-Control-Allow-Origin: *`. ZIP 본문·chart hash 비교는 이 작업에서 하지 않았다.
- 적용 전 백업: `/Users/kgh/dev/src/tuf-backend/cache/public-levels-before-1789007595369.json`.
- 재실행 도구: `/Users/kgh/dev/src/tuf-backend/cache/web-test-public-levels.ts`. 기본 실행은 비교만 하며 `--apply`에만 백업·로컬 DB 반영·캐시 무효화를 수행한다. 로컬 DB·Redis 주소 검사 포함.
- 도구·백업은 기존 `cache/` Git 제외 규칙을 따른다. 제품 코드로 로컬 데이터를 우회하는 변경은 하지 않았다.

## 리플레이 테스트에 남은 점

현재 레벨은 공식 fileId를 갖는다. 예전 테스트 pass의 validation은 mock `e2e-3514c7a709173d32`를 참조하지만, fixture와 현재 공식 차트의 의미 gameplay hash v1은 모두 `a7583eb3b1b8cda23a6a529ee96837859d44c5346dc29b4c6709f90928fc0396`이다. 로컬 E2E의 해당 제출 14건은 실제 비교 뒤 이 해시를 보완했으며, file ID와 전체 파일 SHA 차이만으로 재생을 거절하지 않는다.

기존 E2E fixture도 여전히 mock 파일 ID·차트 ZIP을 사용하므로 같은 fixture로 새 run을 만들기만 해서는 해결되지 않는다. 공식 ZIP의 대상 차트와 fixture의 게임플레이·증거 호환성을 확인하고 테스트 입력을 정렬한 뒤 새 제출을 만들어야 한다. 기존 제출의 검증 결과·해시를 수정해 맞춘 것으로 만들지는 않았다.
