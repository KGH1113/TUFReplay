# Visual source implementation and verification — 2026-09-21

사용자가 첨부한 최초 프롬프트의 이미지·폰트·pose·식별자·저장/전달 항목을 구현했다. 수정 작업은 직접 수행했으며, 수동 브라우저/게임 비교는 사용자에게 맡겼다. 모드 설치 및 배포는 하지 않았다.

## 지원 소스

| 종류 | 표시 이름 | 식별자 |
| --- | --- | --- |
| Keyviewer / Overlay | Jipper Resourcepack | `jipper-resourcepack` |
| Keyviewer | Jipper KeyViewer | `jipper-keyviewer` |
| Keyviewer | DMNote | `dmnote` |
| Keyviewer | Impl DMNote | `impl-dmnote` |
| Overlay | ImplResourcePack | `impl-resourcepack` |

모드·companion·서버·TUF host·재생기가 동일한 소스/종류 계약을 사용한다. `jipper`와 `quartz`는 신규 입력에서 거부한다.

## 최초 요청에 대한 구현

### Jipper Resourcepack

- 키뷰어 compiler가 최상위 필드만 읽던 오류를 수정했다. importer의 실제 `Feature.KeyViewer.Setting` 구조에서 키 배열, 숫자형 layout, YLocation, Size, 색상, rain 및 foot 설정을 읽고 `Enabled: false`를 존중한다. 이전에 기본 보라색 16키와 `?`만 나왔던 이유이며, 펼친 구조만 사용한 기존 unit fixture가 오류를 놓쳤다.
- 사용자 설치 설정에서 시각 설정만 추출한 `tools/live-e2e/fixtures/jipper-resourcepack-settings.json`을 전체 pipeline의 입력으로 사용한다. YLocation 584.2361, 빨간 배경·외곽선, 16개 손 키와 4개 발 키가 API를 거쳐 유지되고 녹화된 A 입력이 해당 슬롯을 누르는지 검사한다.
- 원본 BackSequence16/20 및 발 키의 짝수/홀수 순서를 적용했다. 빈 커스텀 라벨은 비워 두며, `0x1000 + platform native code` 형태의 추가 바인딩도 녹화 입력과 연결한다. KPS/Total은 이름과 값을 별도 TMP 영역으로 배치하고 동일한 설정 색상·배율·원본 sprite를 사용한다.
- macOS JRP의 4321은 HID 225(LShift)이며, 녹화기의 macOS 가상 키 56과 연결한다. 새 snapshot은 `nativeKeyPlatform`을 저장하고, 기존 snapshot은 리플레이의 `inputNativePlatform`으로 해석하므로 재등록 없이 수정된 재생기에서 처리된다. 좌우 Shift 동시 입력·독립 해제, foot/ghost rain, 카운터 및 seek 검사를 통과했다.
- 원본 KeyBackground, KeyOutline, GhostRain PNG를 embedded resource로 수집한다. renderer가 번들 경로를 해석하여 사용하며, 원본 픽셀 RGBA와 설정 색을 곱한다.
- 키 배경/외곽선은 border 11px 및 자식 배율 0.5, ghost는 border 2px의 tiled Image로 그린다. 작은 사각형의 border 축소, 부분 타일, 원본 pivot (0, 0.5), 각 행의 rain 색·폭·공유 pool 위치를 반영했다.
- 키 이름과 카운터의 TMP 사각형을 구분하고 자동 글꼴 크기를 적용했다. 기본 눌림 색은 idle 색과 별도로 해석한다.
- ProgressBar prefab은 fileID 10907인 Unity 내장 **Background**를 참조한다. AssetRipper headless로 확인한 32×32 이미지, border 10px, PPU 200을 사용한다. 기본 border/background/fill 사각형은 각각 (639,10,642,18), (641,12,638,14), 왼쪽부터 진행률×638이다. 전체 배율은 원본 anchor 기준으로 적용한다.
- ColorPerDictionary의 RGB/RGBA, 알파, 지표별 색, 완전 정확도 색을 유지한다. 상태 행 간격은 35px이며, BPM 줄 간격은 TMP face line-height와 lineSpacing 30의 em 단위를 사용한다.
- Maple TMP 원본 face의 pointSize 74, ascent 62.9, descent -19.61, lineHeight 82.51로 기준선을 계산한다. 콤보는 원본 500ms 지수 감쇠 및 동적 제목 위치를 사용한다.
- Auto는 ResourceChanger의 게임 autoplay 아이콘 교체, SideImage는 설정 GUI의 배지다. replay keyviewer/overlay가 사용하는 이미지가 아니므로 snapshot 자산에는 포함하지 않는다.

### Standalone Jipper KeyViewer

- 독립 version-6 profile importer/compiler를 사용한다. 설치된 세 PNG와 사용자 교체 이미지를 우선 수집하고 실제 slice/tile 렌더링에 연결한다.
- 선택한 기본/custom 폰트와 별개로 설치된 `cjkFonts-regular-normalized.otf`를 항상 휴대한다. font-family의 두 번째 항목으로 연결한다.
- 이름 없는 FontIndex는 실행 중 Jipper KeyViewer의 실제 fontList에서 선택된 이름을 메인 스레드에서 읽는다. `CJK (Default)`와 게임의 `cjkFonts-regular-normalized` 이름은 같은 설치된 CJK 원본 파일로 연결한다. MapleStory/custom 폰트도 원본 파일을 자동 수집하고, Arial 등 시스템 폰트는 해당 CSS family로 보존한다. 목록을 읽을 수 없거나 이름을 파일에 연결할 수 없을 때만 폰트 첨부를 요청한다. 원본 CJK fallback은 항상 유지한다.
- 외부 custom image는 원래 참조를 표시하여 첨부받고, 검증한 bytes와 번들 내부 경로로 바꾼다. 자동 수집은 설치 폴더 안의 파일만 읽는다.

### DMNote / Impl DMNote

- 선택 탭의 spritePositions, base/pose 이미지, 자연 크기 메타데이터, pivot, trigger 조합, whileHeld/onPress, easing 및 seek를 전체 importer/compiler/renderer 경로에서 보존한다.
- 파일 첨부로 참조가 변경될 때 자연 크기 메타데이터의 source도 함께 갱신한다. pose·회전·easing overshoot의 도달 범위를 overlay 창 배치에 반영한다.
- 공식 Pretendard Variable과 Impl 기본 SUIT-Regular, IsYun(LeeSeoyun), RoundedFixedsys(DungGeunMo), Impl의 static Pretendard를 해당 원본 bytes로 내장한다. family 및 원본 CSS URL을 휴대 가능한 경로에 매핑한다.
- Pretendard/SUIT는 OFL, DungGeunMo는 원저작자의 public-domain 배포, LeeSeoyun은 변경하지 않은 폰트 embedding 허용 조건을 별도 license 파일로 보존한다. LeeSeoyun을 OFL로 표시하지 않는다.
- 임의 원격 font CSS/image, blob/asset/tauri 참조는 첨부 요구를 통해 해결한다. 허용된 snapshot image URL은 CSS background에 복원하고 외부 URL은 렌더링하지 않는다.
- ICO와 AVIF의 MIME·확장자·signature를 collector/server에서 일치시킨다. AVIF는 major brand뿐 아니라 ftyp compatible brand도 검사한다. renderer의 asset preload 실패는 재생 준비 실패로 전달되며 빈 이미지로 성공 처리하지 않는다.
- sounds/embeddedLocalSounds와 실행 스크립트는 visual-only 계약에서 제외한다. UI 안내, importer 및 서버 회귀 검사에 반영했다.

### ImplResourcePack overlay

- 원본의 Status/BPM/Combo/Judgement 배치, 색, RecordMode, HidePerfectJudgmentText, 콤보 및 TMP 기준선을 반영했다. 곡 제목을 replay project settings에서 전달하며 12–24pt 범위의 균일한 자동 크기 조절을 적용한다.
- Maple과 게임 원본 CJK 폰트를 모두 내장한다. 기본 등록의 검사 및 실제 가져오기는 첨부 없이 완료된다. 이전의 `ImplResourcePack CJK fallback` 필수 첨부 처리는 잘못된 구현이었으며 제거했다.
- AssetRipper headless로 `RDConstants.chineseFontTMPro` → `cjkFonts-regular-normalized SDF` → `sharedassets0.assets`의 원본 OpenType을 확인했다. 기존 플레이어의 원본 폰트와 SHA-256이 같다. 11개 테이블을 그대로 보존하는 WOFF(31,959,108 bytes)로 압축하여 글리프·문자 매핑·메트릭을 유지했다. 게임 자산의 출처는 별도 notice에 기록하며 OFL로 재표기하지 않는다.
- `./scripts/run.sh visual-font-assets <local-original-font>`는 고정된 원본 SHA를 검사하고 WOFF를 로컬에서 재생성하며, 압축 해제한 모든 테이블이 원본과 같은지 검증한다. 생성한 WOFF를 저장소에 포함하므로 일반 빌드 및 등록에는 AssetRipper나 형제 저장소가 필요 없다.
- 실물 fixture에서 CJK 업로드 입력을 제거했다. 첨부 없는 검사에서 누락 0건, 번들·DB/API의 원본 WOFF bytes 보존, compiler fallback 및 실제 폰트 decode를 회귀 검사한다.

## 저장과 이전 식별자

마이그레이션은 구 `jipper` 행의 source 열, bundle 내부 source, bundle bytes, SHA-256을 함께 갱신한다. preset ID 및 고정된 제출 선택은 보존한다. DB 임시 테이블 회귀 검사로 자산 내용과 참조 2건이 유지되는 것을 확인했다.

사용자가 지정한 로컬 `tuf_replay_e2e`의 구 행 `d913bde7-37dc-428d-9355-9e03814f281c`도 같은 방식으로 정리했다. 새 식별자, SHA와 실제 bytes 일치, 기존 제출 참조 2건 유지를 확인했다. 데이터 삭제는 하지 않았다.

## 자동 검증과 재현

- `TUFREPLAY_BUILD_FLAVOR=auto-submission ./scripts/run.sh mod-check`: C# 전체 테스트, Unity/Mono 호환성, 13개 updater 검사 통과. 설치 생략.
- `./scripts/run.sh web-check`: 150개 테스트, TypeScript, 195개 파일 Biome 및 프로덕션 빌드 통과.
- `./scripts/run.sh server-check`: 포맷, 전체 타깃 컴파일, 40개 라이브러리 테스트 통과.
- 전용 DB에서 마이그레이션의 bundle 무결성/참조 보존 검사 통과.
- 서버 요청/모델/worker 통합 검사 21개 통과. 실물 자산 전용 검사 1개는 아래 pipeline 명령에서 별도로 실행하여 통과했다.
- `./scripts/run.sh visual-check`: 다섯 저장소/계층의 소스 계약 및 구 식별자 거부 검사, assertion 27개 통과.
- 재생기 TypeScript 검사 및 unit tests 931개 통과, 기존 샘플 의존 테스트 1개 생략 (macOS Shift 수정 시점). 실제 JRP 설정 구조, Windows/macOS 눌림·해제·카운터·rain·seek, TMP metric, easing, tiled partial patch, 각 소스 compiler, pose seek 등을 포함한다.
- 재생기 관련 46개 파일 Biome 검사 오류 없음, 경고 105개. 기존 `tuf-replay.parser.ts` 및 `tests/replay/tuf-replay.test.ts`의 포맷 오류 2개는 이번 수정 범위 밖이라 유지했다.
- `visual-pipeline-check`: 실제 자산을 C# importer에서 생성 → authenticated 등록 API → DB 및 고정 제출 선택 → public manifest/visual API → parser/compiler/evaluator/Canvas renderer. 여섯 소스/종류 조합의 JSON 및 자산 bytes 동등성, SHA/ETag, decode, 시각 상태 변화, source PNG 픽셀 비교를 검사한다.
- 실제 기본/커스텀/CJK 폰트, PNG/CSS image, ICO/AVIF, ghost 및 sprite pose를 포함한 pixel/renderer 테스트 2개, assertion 352개 통과. Impl의 기본 CJK 자동 포함, JRP 설정 구조와 macOS Shift 수정 후 전체 pipeline을 다시 실행하여 통과했다.
- shell 문법 검사 32개 및 두 작업 저장소 `git diff --check` 통과. ShellCheck 실행 파일은 설치되어 있지 않다.

실행 전 현재 C# test bridge를 빌드하고, 형제 `adofai-web-editor`의 개발 의존성을 설치해야 한다. `JKV_SOURCE_ROOT`는 원본 standalone checkout의 `JipperKeyViewer` 디렉터리이며 기본값은 `/private/tmp/tuf-jipper-keyviewer/JipperKeyViewer`다. CJK 원본 파일을 테스트 입력으로 읽고 새 설치 폴더는 임시 디렉터리에 생성한다.

```sh
TUF_VISUAL_TEST_DATABASE_URL=postgres://.../disposable_visual_test ./scripts/run.sh visual-pipeline-check
```

이 DB는 전용으로 먼저 생성해야 하며 테스트가 테이블을 재생성한다. 기존 개발 DB를 자동 선택하지 않는다. generated JSON/API bytes 및 18개 PNG는 `build/visual-fixtures/`에 남는다.

픽셀 oracle은 원본 PNG의 실제 픽셀, border/cropping/alpha 및 원본 코드에서 읽은 geometry를 사용한다. 브라우저를 실행하지 않는 Canvas 테스트는 Skia를 사용하고, Skia Image에 없는 AVIF decoder는 테스트의 libvips로 제공한다. 저장된 bundle과 API는 AVIF 원본 bytes를 유지한다. 이 검사는 Unity 게임 화면을 캡처한 전 프레임 pixel-identical 비교라는 뜻은 아니다. 실제 게임/브라우저 화면 확인은 사용자가 진행한다.
