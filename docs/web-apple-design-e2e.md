# Web Apple Design 수동 E2E 체크리스트

이 문서는 데스크톱 TUFReplay Web의 모션, 가독성, 실행 기록 inspector, microphone calibration 회귀를 수동으로 확인하기 위한 체크리스트다. 모바일과 터치 레이아웃은 검증 범위에서 제외한다.

## 준비

1. 저장소 루트에서 의존성을 설치한다.

   ```bash
   bun install
   ```

2. chart embed 개발 서버가 `http://127.0.0.1:5173/embed/chart`를 제공하는 상태에서 TUFReplay Web을 실행한다.

   ```bash
   VITE_WEB_ADOFAI_EMBED_URL=http://127.0.0.1:5173/embed/chart bun run web:dev
   ```

3. Vite가 출력한 주소에 `?mock=1`을 붙여 연다. 일반적으로 chart가 5173 포트를 사용하면 TUFReplay Web은 `http://localhost:5174/?mock=1`에서 열린다.
4. 브라우저 viewport를 1440 × 1000으로 맞추고 macOS의 **동작 줄이기**를 끈다.

> Chart embed가 없어도 헤더 메뉴, dialog, calibration 설정, 가독성은 확인할 수 있다. Chart marker와 오른쪽 inspector 검증에는 실제 chart embed 연결이 필요하다.

## 기본 데스크톱 흐름

| 화면 위치 | 누를 항목 | 수행 동작 | 기대 결과 |
| --- | --- | --- | --- |
| 좌측 `DAYS` rail | `Jul 13` | 한 번 클릭 | 선택 표시가 즉시 `Jul 13`으로 이동하고 상단 레벨 목록이 갱신된다. |
| 상단 레벨 strip | `Cry` 카드 | 한 번 클릭 | chart와 해당 레벨 데이터가 표시된다. 레벨 카드는 명확한 초록색 선택 상태를 유지한다. |
| chart | 임의의 judgment marker | 클릭하여 marker를 선택하고 다른 marker로 바꾼다. | 오른쪽 inspector 폭은 한 번에 확보되고 내부만 짧게 나타난다. chart가 300ms 동안 계속 눌리거나 버벅이지 않으며 선택 marker가 계속 보인다. |
| 오른쪽 inspector 상단 | `Progress`, `Time`, `Pitch`, `Accuracy` | 각 정렬 버튼을 빠르게 연속 클릭한다. | 버튼 라벨이 12px로 읽히고 실행 카드가 최신 정렬 기준으로 자연스럽게 이동한다. 진행 중 정렬을 다시 눌러도 이전 animation이 남거나 카드가 튀지 않는다. |
| 정렬 버튼 오른쪽 | 위·아래 화살표 | 번갈아 빠르게 클릭한다. | 오름·내림차순 선택 상태가 즉시 바뀌고 카드 순서가 최신 선택과 일치한다. 화살표 버튼 높이는 정렬 버튼과 맞는다. |
| 오른쪽 실행 카드 | `Play` | 첫 번째와 두 번째 카드에서 한 번씩 클릭한다. | Play 라벨이 12px로 읽히고 hover, pending, playing 상태가 기존과 동일하게 표시된다. |
| 오른쪽 실행 카드 상단 | 세로 점 3개 | 메뉴를 열고 microphone recording 하위 메뉴가 있으면 연다. | 메뉴와 하위 메뉴가 짧게 fade/scale되며 파일 크기와 보존 기간 설명이 12px로 읽힌다. Escape로 단계별로 닫힌다. |
| 실행 카드 하단 | 9개 judgment 숫자 | 숫자와 tooltip을 확인한다. | 숫자가 12px로 읽히며 9개 열이 겹치거나 잘리지 않는다. hover tooltip의 레이블과 값이 정확하다. |

## Floating UI와 calibration

| 화면 위치 | 누를 항목 | 수행 동작 | 기대 결과 |
| --- | --- | --- | --- |
| 우측 상단 | 지구본 아이콘 | 클릭해 언어 메뉴를 열고 언어를 바꾼 뒤 다시 연다. | 메뉴가 짧은 fade/scale로 열리고 선택 언어가 표시된다. 바깥 클릭과 Escape로 정상 닫힌다. |
| 우측 상단 | 마이크 아이콘 | 클릭 | `Microphone input` 메뉴가 열리고 장치 목록과 toggle이 기존 상태를 정확히 표시한다. |
| 마이크 메뉴 하단 | `Adjust timing offset` | 클릭 | 메뉴가 닫히고 `Microphone timing` dialog가 같은 위치에서 짧은 fade/scale로 열린다. 배경은 dim/blur 처리되고 focus가 dialog 안으로 이동한다. |
| `Microphone timing` dialog | `Microphone offset`, `Mic gain` slider | 마우스로 값을 바꾸고 방향키로 1단계씩 조정한다. | 값과 접근성 output이 즉시 갱신되고 기존 저장 동작이 유지된다. |
| dialog 하단 | `Calibrate with level` | 클릭 | dialog가 calibration 진행 상태로 전환된 후 성공 시 넓은 editor로 바뀐다. 단계별 폭 변경이 불필요하게 흔들리지 않는다. |
| calibration editor 상단 | `−`, `+` | 여러 번 클릭 | 눈금 중심을 유지하면서 waveform zoom이 변경된다. |
| calibration editor waveform | 초록색 `Microphone` 파형 | 파형 안에서 drag를 시작해 영역 밖까지 이동한 뒤 놓는다. | 파형이 포인터와 1:1로 붙어 움직이고 영역 밖에서도 drag가 끊기지 않는다. 놓을 때 offset이 commit된다. |
| calibration editor 하단 | `Play test` | 재생과 정지를 반복한다. | playhead가 매끄럽게 움직이고 버튼 상태와 audio feedback이 일치한다. |
| calibration editor 하단 | `Mic gain`과 `Done` | gain을 조정한 뒤 `Done` 클릭 | gain 표시가 갱신되고 dialog가 닫히며 배경 focus가 복원된다. |

## 동작 줄이기

1. macOS에서 **시스템 설정 → 손쉬운 사용 → 디스플레이 → 동작 줄이기**를 켠다.
2. 페이지를 새로고침한다.
3. 다음 항목을 다시 열고 닫는다.

| 화면 위치 | 누를 항목 | 기대 결과 |
| --- | --- | --- |
| 우측 상단 | 지구본 아이콘 | 언어 메뉴가 확대·축소 없이 짧은 opacity 변화만 사용한다. |
| 우측 상단 | 마이크 아이콘 | microphone 메뉴가 확대·축소 없이 짧은 opacity 변화만 사용한다. |
| 실행 카드 | 세로 점 3개와 하위 메뉴 | 모든 dropdown 단계에서 zoom이 제거된다. |
| 실행 카드 | `Play`에 pointer hover | tooltip이 zoom 없이 표시된다. |
| 마이크 메뉴 | `Adjust timing offset` | dialog가 scale 없이 fade로 표시된다. |
| chart | marker 선택 | inspector 내부의 slide entrance가 제거되고 즉시 표시된다. |
| inspector | 정렬 버튼 | 카드 재정렬 animation 없이 즉시 최신 순서로 바뀐다. |

검증 후 **동작 줄이기**를 원래 설정으로 되돌린다.

## 키보드와 focus 회귀

1. 페이지를 새로고침하고 마우스를 사용하지 않는다.
2. `Tab`과 `Shift+Tab`으로 지구본, 마이크, 정렬 버튼, Play, 실행 작업 메뉴를 순회한다.
3. 메뉴는 `Enter` 또는 `Space`로 열고 화살표 키로 이동한다.
4. tooltip trigger, menu, dialog에서 focus ring이 명확하게 보이는지 확인한다.
5. `Escape`를 눌렀을 때 하위 메뉴 → 상위 메뉴 → dialog 순서로 현재 계층만 닫히는지 확인한다.
6. dialog를 닫으면 focus가 dialog를 연 버튼으로 돌아가는지 확인한다.

## 완료 조건

- 데스크톱 1440 × 1000에서 변경 대상 텍스트가 겹치거나 잘리지 않는다.
- 일반 모션에서는 floating UI가 동일한 빠른 fade/scale 언어를 사용한다.
- **동작 줄이기**에서는 scale, slide, run-sort animation이 제거된다.
- inspector 개폐 중 chart가 연속 reflow로 버벅이지 않고 선택 marker를 다시 focus한다.
- 파형 drag, slider keyboard 입력, menu/dialog focus와 Escape 동작에 회귀가 없다.
