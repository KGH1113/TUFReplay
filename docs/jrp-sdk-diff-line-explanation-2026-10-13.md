# JipperResourcePack SDK 연동 diff 줄별 설명

작성일: 2026-10-13 (Asia/Seoul)

대상: /Users/kgh/dev/src/jipper-resourcepack, 브랜치 feat/tufreplay-render-support, 비교 기준 HEAD ac6d6410571ee7d585aedce7f5085de34f615af2.

이 문서는 현재 작업 트리의 코드 변경을 실제 diff와 줄별 의사코드로 설명한다. 각 hunk 아래에서 삭제 줄은 이전 파일 번호, 추가 줄은 현재 파일 번호를 사용한다. diff의 그대로인 문맥 줄은 변경 대상이 아니므로 설명 표에 중복하지 않는다. 빈 줄·괄호·주석도 변경된 줄이면 표에 포함한다. 추가 파일은 모든 줄을 추가 diff로 표시한다.

기존 Installer 삭제는 SDK 구현과 별개로 남아 있던 변경이다. [Installer 삭제 줄별 부록](./jrp-installer-deletion-line-explanation-2026-10-13.md)에 텍스트 삭제 diff 전체와 각 삭제 줄의 역할을 포함했다. 삭제 목적은 확인되지 않았으며 SDK를 위해 삭제했다고 설명하지 않는다. 바이너리는 사람이 읽는 줄이 없으므로 파일 크기·SHA-256·역할로 설명한다.

## 전체 연결 순서

실제 입력은 평소 SkyHook → 기존 FIFO → ProcessKeyEvent → Work를 따른다. 재생 입력은 SDK 이벤트를 같은 KeyEvent로 바꾸어 같은 FIFO에 넣는다. 입력 배치 뒤의 요청 표식을 worker가 처리하고, Unity Updater가 키·텍스트·레이닝을 적용한 다음 해당 요청만 완료한다. 오버레이는 기존 game hook 뒤의 UpdateTime/UpdateComboSize와 지연 위치 보정 뒤에 완료한다.

## 현재 동작과 읽을 때의 주의점

- 기존 UI와 카운터를 유지한다. 재생 카운터도 KeyCount.dat로 저장하며 기존 오버레이 게임 hook은 Plays.dat 기록을 갱신할 수 있다. 저장 억제가 구현됐다고 읽으면 안 된다.
- RainManager의 자체 Update를 없애고 KeyViewerUpdater에서 호출한다. 정상 사용의 호출 구조도 바뀐다.
- KeyViewer.cs의 KPS 숨김 설정은 Updater.enabled=false로 한다. 현재 완료·레이닝 경로가 이 Updater에 있으므로 해당 설정에서 갱신/완료가 실행되지 않을 수 있다. 아래 자동 검사는 KeyViewer.cs 전체를 실행하지 않아 이 경로를 검증하지 않는다.
- 시간 원점 보정은 기존 rain/KPS/콤보 애니메이션의 나이를 이어가기 위한 것이며, UI 전체 snapshot이나 별도 렌더 기능 구현이 아니다.
- Harmony alias와 참조 경로 처리는 현재 빌드 환경 대응이다. SDK 계약 자체가 요구하는 API 변경과 구분한다.
- 최근 scripts/run.sh check와 git diff --check는 통과했다. 전체 모드 빌드는 KeyboardChatterBlocker.dll 참조 누락으로 진행하지 못했다. 실제 게임·픽셀 검증과 정상 입력 성능 측정은 별도다.

## 연결 상태의 역할

| 상태 | 의미와 보관 이유 |
|---|---|
| `tufReplayConnection` | 참가 등록·입력 수신·요청 완료·실패·연결 해제를 담당하는 SDK 연결이다. |
| `_renderActive` | 이 키뷰어가 재생 입력을 받는 단계인지 worker가 확인한다. 전역 IsActive와 함께 수명 경계를 보호한다. |
| `_inputGeneration` | 큐에 넣은 뒤 공급원이 바뀐 항목을 구분하는 번호다. SDK 요청 ID와 같은 번호가 아니다. |
| `_inputSync` | worker의 Work와 시작·종료·폐기를 직렬화한다. 성능 비용이 없는 추상화라고 가정하지 않는다. |
| `_processedFrame` | worker가 처리한 입력 배치의 요청 객체다. Unity frameCount와 독립적인 완료 대상이다. |
| `_replayTickOrigin` | SDK 경과 초를 기존 tick 시간축의 현재 위치에 맞추는 원점이다. |
| `_nativeTickOffset` | SDK에서 native로 돌아갈 때 rain/KPS 경과 시간이 뛰지 않도록 원점 차이를 보관한다. |
| `_nativeResumeState` | 렌더 중 관찰한 실제 키별 눌림 상태다. 기존 UI·카운터 전체 snapshot이 아니다. |
| `_nativeResumeTimes` | 각 실제 키 상태가 언제 관찰됐는지 native 시각을 보관한다. 늦은 worker 이벤트로 최신 상태가 덮이지 않게 한다. |
| `_presentationEnabled` | 오류 제외 후 일반 Show hook이 표시를 다시 켜지 못하게 한다. |
| `_visibleBeforeExclusion` | 제외 직전의 표시 활성 상태다. 세션 종료 때 그 상태로 복귀한다. |
| `_comboStartedAt` | SDK 시계에서 콤보 애니메이션이 시작한 시각이다. |
| `_comboAnimating` | 애니메이션 진행 여부다. native Stopwatch.IsRunning 대신 두 시계 모두에 적용한다. |
| `_nativeComboElapsed` | 시계 전환 전에 이미 진행된 콤보 애니메이션 시간을 native 계산에 포함한다. |
| `_replayClockActive` | 오버레이의 시계 전환을 시작했는지 기억해 EndRender를 중복 실행하지 않게 한다. |
| `KeyEvent.NativeTicks` | 실제 입력 순서 판정용 native 시각이다. Work 계산에 들어가는 Ticks와 단위를 섞지 않는다. |
| `KeyEvent.Ticks` | 기존 rain/KPS 기능에 공급하는 이벤트 발생 시각이다. 처리된 프레임의 현재 시각으로 바꾸지 않는다. |
| `KeyEvent.Request` | FIFO에서 입력 배치 뒤에 들어가는 요청 완료 표식이다. worker 도착만으로 캡처를 허용하지 않는다. |
| `KeyEvent.IsReplay` | 실제 입력의 설정 변경·native 코드 처리와 재생 입력을 구분한다. |

## 요청 완료의 의사코드

```text
SDK가 기록 입력을 보낸다
  기존 label로 변환한다
  기존 FIFO에 넣는다
SDK가 프레임 요청을 보낸다
  같은 FIFO 뒤에 요청 표식을 넣는다
worker가 항목을 꺼낸다
  공급원/세대/취소를 검사한다
  유효 입력은 기존 ProcessKeyEvent와 Work로 처리한다
  요청 표식에 도착하면 처리된 요청을 공개한다
Unity Updater가 실행된다
  현재 기다리는 요청과 같은 요청인지 확인한다
  그 요청의 시간 범위에서 기존 표시를 갱신한다
  RawRain graphic, 키 상태, TMP 텍스트를 적용한다
  그 요청만 CompleteFrame으로 완료한다
Renderer가 자신의 Canvas/캡처 순서를 이어간다
```

오버레이는 입력 FIFO가 필요 없다. 기존 game hook 뒤에서 UpdateTime과 UpdateComboSize를 실행하고, 지연 콤보 위치 보정 callback이 실행된 다음 해당 요청을 완료한다. SDK는 특정 함수 이름을 강제하지 않고 이 연결 코드가 기존 갱신 경계를 선택한다.

## 빌드와 테스트 파일을 읽는 기준

- csproj의 Private=true는 SDK DLL을 빌드 출력에 복사하는 설정이다. 별도 의존성 모드 설치 요구와 다르다.
- ValidateOverlaySdk는 DLL 누락을 빌드 전에 오류로 표시한다.
- Harmony Aliases=HarmonyLibrary는 0Harmony를 기본 참조 namespace와 분리한다. HarmonyAlias.cs는 필요한 CodeInstruction만 가져온다.
- Directory.Build.targets는 HintPath가 있지만 파일이 없는 선택적 참조를 제거한다. SDK·JALib·KeyboardChatterBlocker는 이 제거 대상에서 제외한다.
- build는 기존 자동 ModPath/ReleasePath 복사 값을 비운다. package는 DLL을 모으고, install만 명시적으로 설치 파일을 교체한다.
- install의 cp/mkdir/해시 기록은 설치 전 프로그램 DLL을 백업하고 설치본을 식별하기 위한 단계다. 설정/asset/카운터 파일은 복사 목록에 포함하지 않는다.
- RainLayout은 production Rain/RainPool과 transform stub을 검사한다. 실제 캡처 픽셀 검사는 아니다.
- ViewerReplay는 production 입력 worker·연결·키·텍스트·카운터·PlayCount를 가져오지만 KeyViewer.cs 전체, Overlay.cs 전체, RainManager는 그대로 실행하지 않는다. 활성화 설정/실제 Unity 실행 순서까지 검증했다고 해석하지 않는다.


## 파일 목록

- 수정: .gitignore
- 수정: Directory.Build.props
- 수정: JipperResourcePack.sln
- 수정: JipperResourcePack/Async/AsyncText.cs
- 수정: JipperResourcePack/JipperResourcePack.csproj
- 수정: JipperResourcePack/KeyViewerContents/Key.cs
- 수정: JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs
- 수정: JipperResourcePack/KeyViewerContents/KeyViewer.cs
- 수정: JipperResourcePack/KeyViewerContents/Rain.cs
- 수정: JipperResourcePack/KeyViewerContents/RainManager.cs
- 수정: JipperResourcePack/Main.cs
- 수정: JipperResourcePack/OverlayContents/Overlay.cs
- 수정: JipperResourcePack/OverlayContents/PlayCount.cs
- 수정: JipperResourcePack/OverlayContents/Status.cs
- 수정: README.md
- 추가: Directory.Build.targets
- 추가: JipperResourcePack/HarmonyAlias.cs
- 추가: JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs
- 추가: JipperResourcePack/OverlayContents/Overlay.Render.cs
- 추가: libs/LICENSE-EXCEPTION
- 추가: libs/LICENSE.md
- 추가: libs/README.md
- 추가: libs/TUFReplay.Overlay.dll
- 추가: libs/TUFReplay.Overlay.xml
- 추가: scripts/run.sh
- 추가: tests/RainLayout/Program.cs
- 추가: tests/RainLayout/RainLayout.csproj
- 추가: tests/RainLayout/UnityStubs.cs
- 추가: tests/ViewerReplay/OverlayReplayTests.cs
- 추가: tests/ViewerReplay/OverlayStubs.cs
- 추가: tests/ViewerReplay/PlayCountReplayTests.cs
- 추가: tests/ViewerReplay/PlayCountStubs.cs
- 추가: tests/ViewerReplay/Program.cs
- 추가: tests/ViewerReplay/Stubs.cs
- 추가: tests/ViewerReplay/ViewerReplay.csproj

## .gitignore

현재 파일: [.gitignore](/Users/kgh/dev/src/jipper-resourcepack/.gitignore)

생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다.

### @@ -10,4 +10,10 @@

```diff
@@ -10,4 +10,10 @@ obj/
 packages/
 
 # User Settings
-Directory.Build.props.user
\ No newline at end of file
+Directory.Build.props.user
+
+# Local render-support package
+artifacts/
+
+# Pinned overlay SDK binary dependency.
+!libs/TUFReplay.Overlay.dll
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 13 | 연결된 식·선언·설정의 일부다: Directory.Build.props.user | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |
| 추가 | 현재 13 | 연결된 식·선언·설정의 일부다: Directory.Build.props.user | 생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다. |
| 추가 | 현재 14 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 15 | 설정 주석/전처리 지시/스크립트 경계다: # Local render-support package | 생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다. |
| 추가 | 현재 16 | 연결된 식·선언·설정의 일부다: artifacts/ | 생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다. |
| 추가 | 현재 17 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 18 | 설정 주석/전처리 지시/스크립트 경계다: # Pinned overlay SDK binary dependency. | 생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다. |
| 추가 | 현재 19 | 연결된 식·선언·설정의 일부다: !libs/TUFReplay.Overlay.dll | 생성 파일은 제외하고 고정 SDK 바이너리는 버전 관리에 포함한다. |

## Directory.Build.props

현재 파일: [Directory.Build.props](/Users/kgh/dev/src/jipper-resourcepack/Directory.Build.props)

게임 Mods/SDK/외부 mod 경로를 빌드 환경에서 지정할 수 있게 한다.

### @@ -3,5 +3,8 @@

```diff
@@ -3,5 +3,8 @@
 
     <PropertyGroup>
         <GameManagedPath Condition="'$(GameManagedPath)' == ''">C:/Program Files (x86)/Steam/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed</GameManagedPath>
+        <GameModsPath Condition="'$(GameModsPath)' == ''">$(GameManagedPath)/../../Mods</GameModsPath>
+        <OverlaySdkPath Condition="'$(OverlaySdkPath)' == ''">$(MSBuildThisFileDirectory)libs/TUFReplay.Overlay.dll</OverlaySdkPath>
+        <KeyboardChatterBlockerPath Condition="'$(KeyboardChatterBlockerPath)' == ''">$(GameModsPath)/KeyboardChatterBlocker/KeyboardChatterBlocker.dll</KeyboardChatterBlockerPath>
     </PropertyGroup>
-</Project>
\ No newline at end of file
+</Project>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;GameModsPath Condition="'$(GameModsPath)' == ''"&gt;$(GameManagedPath)/../../Mods&lt;/GameModsPath&gt; | 게임 Mods/SDK/외부 mod 경로를 빌드 환경에서 지정할 수 있게 한다. |
| 추가 | 현재 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OverlaySdkPath Condition="'$(OverlaySdkPath)' == ''"&gt;$(MSBuildThisFileDirectory)libs/TUFReplay.Overlay.dll&lt;/OverlaySdkPath&gt; | 게임 Mods/SDK/외부 mod 경로를 빌드 환경에서 지정할 수 있게 한다. |
| 추가 | 현재 8 | 프로젝트·리소스·설정 요소를 지정한다: &lt;KeyboardChatterBlockerPath Condition="'$(KeyboardChatterBlockerPath)' == ''"&gt;$(GameModsPath)/KeyboardChatterBlocker/KeyboardChatterBlocker.dll&lt;/KeyboardChatterBlockerPath&gt; | 게임 Mods/SDK/외부 mod 경로를 빌드 환경에서 지정할 수 있게 한다. |
| 삭제 | 이전 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Project&gt; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 게임 Mods/SDK/외부 mod 경로를 빌드 환경에서 지정할 수 있게 한다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |
| 추가 | 현재 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Project&gt; | 게임 Mods/SDK/외부 mod 경로를 빌드 환경에서 지정할 수 있게 한다. |

## JipperResourcePack.sln

현재 파일: [JipperResourcePack.sln](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack.sln)

기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다.

### @@ -2,8 +2,6 @@

```diff
@@ -2,8 +2,6 @@
 Microsoft Visual Studio Solution File, Format Version 12.00
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "JipperResourcePack", "JipperResourcePack\JipperResourcePack.csproj", "{F601ED78-FCBD-4553-96DF-823484985679}"
 EndProject
-Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Installer", "Installer\Installer.csproj", "{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}"
-EndProject
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FakeCore", "FakeCore\FakeCore.csproj", "{14DB6B56-E690-4E04-AFAF-C1AF9C53B172}"
 EndProject
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FakeSkyHook", "FakeCore\FakeSkyHook.csproj", "{C322F93B-D8E5-4869-AA9F-0FB1BC165909}"
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 5 | 오른쪽 결과를 왼쪽 상태에 저장한다: Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Installer", "Installer\Installer.csproj", "{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}" | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 6 | 연결된 식·선언·설정의 일부다: EndProject | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |

### @@ -20,21 +18,20 @@

```diff
@@ -20,21 +18,20 @@ Global
 		{F601ED78-FCBD-4553-96DF-823484985679}.Debug|Any CPU.Build.0 = Debug|Any CPU
 		{F601ED78-FCBD-4553-96DF-823484985679}.Release|Any CPU.ActiveCfg = Release|Any CPU
 		{F601ED78-FCBD-4553-96DF-823484985679}.Release|Any CPU.Build.0 = Release|Any CPU
-		{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
-		{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Debug|Any CPU.Build.0 = Debug|Any CPU
-		{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Release|Any CPU.ActiveCfg = Release|Any CPU
-		{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Release|Any CPU.Build.0 = Release|Any CPU
 		{14DB6B56-E690-4E04-AFAF-C1AF9C53B172}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
 		{14DB6B56-E690-4E04-AFAF-C1AF9C53B172}.Debug|Any CPU.Build.0 = Debug|Any CPU
 		{14DB6B56-E690-4E04-AFAF-C1AF9C53B172}.Release|Any CPU.ActiveCfg = Release|Any CPU
 		{14DB6B56-E690-4E04-AFAF-C1AF9C53B172}.Release|Any CPU.Build.0 = Release|Any CPU
-		{1FF00464-7806-4151-A64E-39296F3BC564}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
-		{1FF00464-7806-4151-A64E-39296F3BC564}.Debug|Any CPU.Build.0 = Debug|Any CPU
-		{1FF00464-7806-4151-A64E-39296F3BC564}.Release|Any CPU.ActiveCfg = Release|Any CPU
-		{1FF00464-7806-4151-A64E-39296F3BC564}.Release|Any CPU.Build.0 = Release|Any CPU
 		{C322F93B-D8E5-4869-AA9F-0FB1BC165909}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
 		{C322F93B-D8E5-4869-AA9F-0FB1BC165909}.Debug|Any CPU.Build.0 = Debug|Any CPU
 		{C322F93B-D8E5-4869-AA9F-0FB1BC165909}.Release|Any CPU.ActiveCfg = Release|Any CPU
 		{C322F93B-D8E5-4869-AA9F-0FB1BC165909}.Release|Any CPU.Build.0 = Release|Any CPU
+		{1FF00464-7806-4151-A64E-39296F3BC564}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
+		{1FF00464-7806-4151-A64E-39296F3BC564}.Debug|Any CPU.Build.0 = Debug|Any CPU
+		{1FF00464-7806-4151-A64E-39296F3BC564}.Release|Any CPU.ActiveCfg = Release|Any CPU
+		{1FF00464-7806-4151-A64E-39296F3BC564}.Release|Any CPU.Build.0 = Release|Any CPU
+	EndGlobalSection
+	GlobalSection(SolutionProperties) = preSolution
+		HideSolutionNode = FALSE
 	EndGlobalSection
 EndGlobal
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: {B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Debug\|Any CPU.ActiveCfg = Debug\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 24 | 오른쪽 결과를 왼쪽 상태에 저장한다: {B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Debug\|Any CPU.Build.0 = Debug\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: {B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Release\|Any CPU.ActiveCfg = Release\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 26 | 오른쪽 결과를 왼쪽 상태에 저장한다: {B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}.Release\|Any CPU.Build.0 = Release\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Debug\|Any CPU.ActiveCfg = Debug\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Debug\|Any CPU.Build.0 = Debug\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 33 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Release\|Any CPU.ActiveCfg = Release\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 삭제 | 이전 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Release\|Any CPU.Build.0 = Release\|Any CPU | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 29 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Debug\|Any CPU.ActiveCfg = Debug\|Any CPU | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 30 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Debug\|Any CPU.Build.0 = Debug\|Any CPU | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Release\|Any CPU.ActiveCfg = Release\|Any CPU | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: {1FF00464-7806-4151-A64E-39296F3BC564}.Release\|Any CPU.Build.0 = Release\|Any CPU | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 33 | 연결된 식·선언·설정의 일부다: EndGlobalSection | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSection(SolutionProperties) = preSolution | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |
| 추가 | 현재 35 | 오른쪽 결과를 왼쪽 상태에 저장한다: HideSolutionNode = FALSE | 기존 Installer 제거 및 솔루션 설정 변경이다. SDK 연동으로 발생했다고 단정하지 않는다. |

## JipperResourcePack/Async/AsyncText.cs

현재 파일: [JipperResourcePack/Async/AsyncText.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/Async/AsyncText.cs)

TMP 텍스트가 예약만 된 상태로 요청을 완료하지 않게 한다.

### @@ -19,7 +19,8 @@

```diff
@@ -19,7 +19,8 @@ public class AsyncText(TextMeshProUGUI tmp) {
         }
     }
 
-    private void ApplyText() {
+    internal void ApplyText() {
+        if(Volatile.Read(ref _textChangeRequested) == 0 || !TMP) return;
         int current;
         do {
             current = Volatile.Read(ref _textChangeRequested);
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 22 | 대기 중인 값을 TMP 텍스트 객체에 적용한다. | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 예약만 된 상태로 프레임을 완료하지 않는다. |
| 추가 | 현재 22 | 대기 중인 값을 TMP 텍스트 객체에 적용한다. | 예약만 된 상태로 프레임을 완료하지 않는다. |
| 추가 | 현재 23 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Volatile.Read(ref _textChangeRequested) == 0 \|\| !TMP) return; | TMP 텍스트가 예약만 된 상태로 요청을 완료하지 않게 한다. |

## JipperResourcePack/JipperResourcePack.csproj

현재 파일: [JipperResourcePack/JipperResourcePack.csproj](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/JipperResourcePack.csproj)

SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다.

### @@ -23,20 +23,32 @@

```diff
@@ -23,20 +23,32 @@
 
     <Target Name="GetReferenceAssemblyPaths"/>
 
+    <Target Name="ValidateOverlaySdk" BeforeTargets="ResolveAssemblyReferences">
+        <Error Condition="!Exists('$(OverlaySdkPath)')" Text="Place the SDK distribution DLL at libs/TUFReplay.Overlay.dll, or set OverlaySdkPath to that DLL." />
+    </Target>
+
     <PropertyGroup>
         <FrameworkPathOverride>$(GameManagedPath)</FrameworkPathOverride>
         <TargetFrameworkDirectory>$(GameManagedPath)</TargetFrameworkDirectory>
     </PropertyGroup>
 
     <ItemGroup>
+        <!-- 독립 SDK 배포물의 고정 DLL을 사용한다. Renderer source checkout이나
+             별도 의존성 모드 없이 빌드하며 Private=true로 이 DLL을 출력에 동봉한다.
+             여러 모드의 동봉본은 같은 SDK여야 하고 실제 공유 여부는 세션 시작에 검사한다. -->
+        <Reference Include="TUFReplay.Overlay">
+            <HintPath>$(OverlaySdkPath)</HintPath>
+            <Private>true</Private>
+        </Reference>
         <Reference Include="0Harmony">
             <HintPath>$(GameManagedPath)/UnityModManager/0Harmony.dll</HintPath>
+            <Aliases>HarmonyLibrary</Aliases>
         </Reference>
         <Reference Include="Accessibility">
             <HintPath>$(GameManagedPath)/Accessibility.dll</HintPath>
         </Reference>
         <Reference Include="AdofaiTweaks">
-            <HintPath>$(GameManagedPath)/../../Mods/AdofaiTweaks/AdofaiTweaks.dll</HintPath>
+            <HintPath>$(GameModsPath)/AdofaiTweaks/AdofaiTweaks.dll</HintPath>
         </Reference>
         <Reference Include="Assembly-CSharp">
             <HintPath>$(GameManagedPath)/Assembly-CSharp.dll</HintPath>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 26 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Target Name="ValidateOverlaySdk" BeforeTargets="ResolveAssemblyReferences"&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 27 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Error Condition="!Exists('$(OverlaySdkPath)')" Text="Place the SDK distribution DLL at libs/TUFReplay.Overlay.dll, or set OverlaySdkPath to that DLL." /&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 28 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Target&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 29 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 36 | 프로젝트·리소스·설정 요소를 지정한다: &lt;!-- 독립 SDK 배포물의 고정 DLL을 사용한다. Renderer source checkout이나 | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 37 | 연결된 식·선언·설정의 일부다: 별도 의존성 모드 없이 빌드하며 Private=true로 이 DLL을 출력에 동봉한다. | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 38 | 연결된 식·선언·설정의 일부다: 여러 모드의 동봉본은 같은 SDK여야 하고 실제 공유 여부는 세션 시작에 검사한다. --&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 39 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="TUFReplay.Overlay"&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 40 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(OverlaySdkPath)&lt;/HintPath&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 41 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Private&gt;true&lt;/Private&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 42 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Reference&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 45 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Aliases&gt;HarmonyLibrary&lt;/Aliases&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 삭제 | 이전 39 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(GameManagedPath)/../../Mods/AdofaiTweaks/AdofaiTweaks.dll&lt;/HintPath&gt; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 51 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(GameModsPath)/AdofaiTweaks/AdofaiTweaks.dll&lt;/HintPath&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |

### @@ -78,10 +90,10 @@

```diff
@@ -78,10 +90,10 @@
             <HintPath>$(GameManagedPath)/I18N.West.dll</HintPath>
         </Reference>
         <Reference Include="JALib">
-            <HintPath>$(GameManagedPath)/../../Mods/JALib/JALib.dll</HintPath>
+            <HintPath>$(GameModsPath)/JALib/JALib.dll</HintPath>
         </Reference>
         <Reference Include="KeyboardChatterBlocker">
-            <HintPath>$(GameManagedPath)/../../Mods/KeyboardChatterBlocker/KeyboardChatterBlocker.dll</HintPath>
+            <HintPath>$(KeyboardChatterBlockerPath)</HintPath>
         </Reference>
         <Reference Include="Mono.Data.Sqlite">
             <HintPath>$(GameManagedPath)/Mono.Data.Sqlite.dll</HintPath>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 81 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(GameManagedPath)/../../Mods/JALib/JALib.dll&lt;/HintPath&gt; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 93 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(GameModsPath)/JALib/JALib.dll&lt;/HintPath&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 삭제 | 이전 84 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(GameManagedPath)/../../Mods/KeyboardChatterBlocker/KeyboardChatterBlocker.dll&lt;/HintPath&gt; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 96 | 프로젝트·리소스·설정 요소를 지정한다: &lt;HintPath&gt;$(KeyboardChatterBlockerPath)&lt;/HintPath&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |

### @@ -541,9 +553,11 @@

```diff
@@ -541,9 +553,11 @@
     <Target Name="CopyToModPath" AfterTargets="Build" Condition="'$(ModPath)' != ''">
         <Copy SourceFiles="$(OutputPath)JipperResourcePack.dll" DestinationFolder="$(ModPath)/JipperResourcePack" SkipUnchangedFiles="true"/>
         <Copy SourceFiles="$(OutputPath)JipperResourcePack.pdb" DestinationFolder="$(ModPath)/JipperResourcePack" Condition="Exists('$(OutputPath)JipperResourcePack.pdb')" SkipUnchangedFiles="true"/>
+        <Copy SourceFiles="$(OverlaySdkPath)" DestinationFolder="$(ModPath)/JipperResourcePack" SkipUnchangedFiles="true"/>
     </Target>
 
     <Target Name="CopyToReleasePath" AfterTargets="Build" Condition="'$(ReleasePath)' != '' and '$(Configuration)' == 'Release'">
         <Copy SourceFiles="$(OutputPath)JipperResourcePack.dll" DestinationFolder="$(ReleasePath)/JipperResourcePack" SkipUnchangedFiles="true"/>
+        <Copy SourceFiles="$(OverlaySdkPath)" DestinationFolder="$(ReleasePath)/JipperResourcePack" SkipUnchangedFiles="true"/>
     </Target>
 </Project>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 556 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Copy SourceFiles="$(OverlaySdkPath)" DestinationFolder="$(ModPath)/JipperResourcePack" SkipUnchangedFiles="true"/&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |
| 추가 | 현재 561 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Copy SourceFiles="$(OverlaySdkPath)" DestinationFolder="$(ReleasePath)/JipperResourcePack" SkipUnchangedFiles="true"/&gt; | SDK DLL을 직접 참조·동봉하고 현재 게임/모드 빌드 경로를 지정한다. |

## JipperResourcePack/KeyViewerContents/Key.cs

현재 파일: [JipperResourcePack/KeyViewerContents/Key.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/Key.cs)

예약 callback과 직접 적용이 겹쳐도 없는 요청/폐기 UI를 다시 처리하지 않는다.

### @@ -34,6 +34,9 @@

```diff
@@ -34,6 +34,9 @@ public class Key {
     }
 
     public void UpdateKey(bool force = false) {
+        // Scheduled callbacks and explicit updates may handle the same request.
+        // Skip destroyed UI, or unchanged UI when no update is forced.
+        if((!force && Volatile.Read(ref _updateRequested) == 0) || !GameObject) return;
         int current;
         do {
             current = Volatile.Read(ref _updateRequested);
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 37 | 주석: Scheduled callbacks and explicit updates may handle the same request. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 38 | 주석: Skip destroyed UI, or unchanged UI when no update is forced. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 39 | 조건을 만족할 때 뒤의 작업을 수행한다: if((!force &amp;&amp; Volatile.Read(ref _updateRequested) == 0) \|\| !GameObject) return; | 예약 callback과 직접 적용이 겹쳐도 없는 요청/폐기 UI를 다시 처리하지 않는다. |

## JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs

현재 파일: [JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs)

두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다.

### @@ -5,9 +5,9 @@

```diff
@@ -5,9 +5,9 @@ using System.Reflection;
 using System.Reflection.Emit;
 using System.Runtime.CompilerServices;
 using System.Threading;
-using HarmonyLib;
 using JALib.Core.Patch;
 using SkyHook;
+using TUFReplay.Overlay;
 using UnityEngine;
 using EventType = SkyHook.EventType;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 8 | 기존 Harmony namespace import를 제거한다. | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. HarmonyLibrary 참조 alias를 사용하도록 빌드 의존을 바꿨다. |
| 추가 | 현재 10 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |

### @@ -17,9 +17,12 @@

```diff
@@ -17,9 +17,12 @@ public partial class KeyViewer {
     private const long NoEventOffset = long.MinValue >> 1;
     private static readonly bool StopwatchTicksAreDateTimeTicks = System.Diagnostics.Stopwatch.Frequency == TimeSpan.TicksPerSecond;
 
+    private static long NativeTicks => StopwatchTicksAreDateTimeTicks ? Stopwatch.ElapsedTicks : Stopwatch.Elapsed.Ticks;
     public static long CurrentTicks {
         [MethodImpl(MethodImplOptions.AggressiveInlining)]
-        get => StopwatchTicksAreDateTimeTicks ? Stopwatch.ElapsedTicks : Stopwatch.Elapsed.Ticks;
+        get => TUFReplaySession.IsActive
+            ? (Instance?._replayTickOrigin ?? 0) + checked((long)(TUFReplaySession.ElapsedTime * TimeSpan.TicksPerSecond))
+            : NativeTicks + (Instance?._nativeTickOffset ?? 0);
     }
 
     private readonly bool[] _keyState = new bool[GhostOutIndex];
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 20 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private static long NativeTicks =&gt; StopwatchTicksAreDateTimeTicks ? Stopwatch.ElapsedTicks : Stopwatch.Elapsed.Ticks; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 삭제 | 이전 22 | 이 함수 호출/식 처리를 실행한다: get =&gt; StopwatchTicksAreDateTimeTicks ? Stopwatch.ElapsedTicks : Stopwatch.Elapsed.Ticks; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 23 | 연결된 식·선언·설정의 일부다: get =&gt; TUFReplaySession.IsActive | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 24 | SDK 경과 초를 기존 tick 단위로 바꾼다. | 같은 rain/KPS 계산식에 시각을 공급한다. |
| 추가 | 현재 25 | 이 함수 호출/식 처리를 실행한다: : NativeTicks + (Instance?._nativeTickOffset ?? 0); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |

### @@ -27,6 +30,9 @@

```diff
@@ -27,6 +30,9 @@ public partial class KeyViewer {
     private ConcurrentQueue<KeyEvent> _eventQueue;
     private SemaphoreSlim _eventSignal;
     private volatile bool _listening;
+    private readonly object _inputSync = new();
+    private volatile bool _renderActive;
+    private volatile int _inputGeneration;
     private KeyBinding _keyBinding;
     private long _eventOffsetTicks = NoEventOffset;
     private volatile int _capturedKeyCode;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 33 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private readonly object _inputSync = new(); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 34 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private volatile bool _renderActive; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 35 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private volatile int _inputGeneration; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |

### @@ -148,25 +154,54 @@

```diff
@@ -148,25 +154,54 @@ public partial class KeyViewer {
     }
 
     private void OnKeyEvent(SkyHookEvent hookEvent) {
+        long nativeTicks = hookEvent.GetTimeInTicks();
+        int generation = _inputGeneration;
+        if(_renderActive || TUFReplaySession.IsActive) {
+            RememberNativeInput(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, nativeTicks);
+            return;
+        }
+        EnqueueInput(new KeyEvent(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, ToStopwatchTicks(nativeTicks), nativeTicks, generation));
+    }
+
+    private void EnqueueInput(KeyEvent input) {
         ConcurrentQueue<KeyEvent> queue = _eventQueue;
         SemaphoreSlim signal = _eventSignal;
         if(queue == null || signal == null) return;
-        queue.Enqueue(new KeyEvent(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, ToStopwatchTicks(hookEvent.GetTimeInTicks())));
+        queue.Enqueue(input);
         signal.Release();
     }
 
     private void ProcessKeyEvents() {
         ConcurrentQueue<KeyEvent> queue = _eventQueue;
         SemaphoreSlim signal = _eventSignal;
+        TUFReplayFrameRequest processingRequest = null;
         while(_listening) {
             try {
+                processingRequest = null;
                 signal.Wait();
-                while(_listening && queue.TryDequeue(out KeyEvent keyEvent)) ProcessKeyEvent(keyEvent);
+                while(_listening && queue.TryDequeue(out KeyEvent keyEvent)) {
+                    processingRequest = keyEvent.Request ?? (keyEvent.IsReplay ? tufReplayConnection?.PendingFrame : null);
+                    lock(_inputSync) {
+                        if(keyEvent.Request != null) {
+                            if(keyEvent.Generation == _inputGeneration && _renderActive && !keyEvent.Request.IsCancelled)
+                                Volatile.Write(ref _processedFrame, keyEvent.Request);
+                        } else if(keyEvent.IsReplay) {
+                            if(keyEvent.Generation == _inputGeneration && _renderActive && tufReplayConnection.PendingFrame is { IsCancelled: false })
+                                ProcessKeyEvent(keyEvent);
+                        } else if(keyEvent.Generation != _inputGeneration || _renderActive || TUFReplaySession.IsActive) {
+                            RememberNativeInput(keyEvent.Label, keyEvent.Key, keyEvent.Pressed, keyEvent.NativeTicks);
+                        } else {
+                            if(_nativeResumeState != null) ResumeNativeInput();
+                            ProcessKeyEvent(keyEvent);
+                        }
+                    }
+                }
             } catch (ThreadAbortException) {
                 // Handle thread abort if necessary
             } catch (Exception e) {
                 if(!_listening) return;
                 Main.Instance.LogException(e);
+                if(processingRequest != null) tufReplayConnection?.ReportFailure(processingRequest, e);
             }
         }
     }
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 157 | 오른쪽 결과를 왼쪽 상태에 저장한다: long nativeTicks = hookEvent.GetTimeInTicks(); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 158 | 오른쪽 결과를 왼쪽 상태에 저장한다: int generation = _inputGeneration; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 159 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_renderActive \|\| TUFReplaySession.IsActive) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 160 | 실제 키의 최신 상태를 복귀용으로 기억한다. | 실제 입력을 영상에 섞지 않고 종료 후 복귀한다. |
| 추가 | 현재 161 | 함수를 종료하거나 식의 결과를 반환한다: return; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 162 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 163 | 이 함수 호출/식 처리를 실행한다: EnqueueInput(new KeyEvent(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, ToStopwatchTicks(nativeTicks), nativeTicks, generation)); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 164 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 165 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 166 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void EnqueueInput(KeyEvent input) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 삭제 | 이전 154 | 이 함수 호출/식 처리를 실행한다: queue.Enqueue(new KeyEvent(hookEvent.Label, hookEvent.Key, hookEvent.Type == EventType.KeyPressed, ToStopwatchTicks(hookEvent.GetTimeInTicks()))); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 170 | 이 함수 호출/식 처리를 실행한다: queue.Enqueue(input); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 177 | 오른쪽 결과를 왼쪽 상태에 저장한다: TUFReplayFrameRequest processingRequest = null; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 180 | 오른쪽 결과를 왼쪽 상태에 저장한다: processingRequest = null; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 삭제 | 이전 164 | 조건이 참인 동안 반복한다: while(_listening &amp;&amp; queue.TryDequeue(out KeyEvent keyEvent)) ProcessKeyEvent(keyEvent); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 182 | 조건이 참인 동안 반복한다: while(_listening &amp;&amp; queue.TryDequeue(out KeyEvent keyEvent)) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 183 | 오른쪽 결과를 왼쪽 상태에 저장한다: processingRequest = keyEvent.Request ?? (keyEvent.IsReplay ? tufReplayConnection?.PendingFrame : null); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 184 | 입력 처리·수명 전환·상태 폐기를 같은 lock으로 직렬화한다. | worker와 Unity 쪽이 같은 상태를 동시에 바꾸지 않는다. |
| 추가 | 현재 185 | 조건을 만족할 때 뒤의 작업을 수행한다: if(keyEvent.Request != null) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 186 | 조건을 만족할 때 뒤의 작업을 수행한다: if(keyEvent.Generation == _inputGeneration &amp;&amp; _renderActive &amp;&amp; !keyEvent.Request.IsCancelled) | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 187 | 처리 완료 요청을 다른 스레드가 볼 수 있게 공개하거나 비운다. | worker 처리 결과를 Unity 경계로 전달한다. |
| 추가 | 현재 188 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 189 | 조건을 만족할 때 뒤의 작업을 수행한다: if(keyEvent.Generation == _inputGeneration &amp;&amp; _renderActive &amp;&amp; tufReplayConnection.PendingFrame is { IsCancelled: false }) | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 190 | 이 함수 호출/식 처리를 실행한다: ProcessKeyEvent(keyEvent); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 191 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 192 | 실제 키의 최신 상태를 복귀용으로 기억한다. | 실제 입력을 영상에 섞지 않고 종료 후 복귀한다. |
| 추가 | 현재 193 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 194 | 비활성 SDK에서 기억한 실제 입력 상태를 기존 Work로 반영한다. | 종료 후 물리 상태와 표시 상태를 맞춘다. |
| 추가 | 현재 195 | 이 함수 호출/식 처리를 실행한다: ProcessKeyEvent(keyEvent); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 196 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 197 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 198 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 204 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |

### @@ -175,7 +210,7 @@

```diff
@@ -175,7 +210,7 @@ public partial class KeyViewer {
         KeyBinding binding = _keyBinding;
         if(binding == null || !Enabled) return;
         bool pressed = keyEvent.Pressed;
-        if(pressed && _selectedKey != -1 && _changeState != 1) {
+        if(!keyEvent.IsReplay && pressed && _selectedKey != -1 && _changeState != 1) {
             KeyCode capturedKey = VersionSafe.SkyHookKeyToUnityKey(keyEvent.Label);
             _capturedKeyCode = capturedKey switch {
                 KeyCode.None => keyEvent.Key + 0x1000,
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 178 | 조건을 만족할 때 뒤의 작업을 수행한다: if(pressed &amp;&amp; _selectedKey != -1 &amp;&amp; _changeState != 1) { | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 213 | 키 바인딩 변경은 실제 press에만 허용한다. | 기록된 입력으로 사용자 설정을 바꾸지 않는다. |

### @@ -187,7 +222,7 @@

```diff
@@ -187,7 +222,7 @@ public partial class KeyViewer {
         }
         bool counted = false;
         if(binding.LabelMap.TryGetValue(keyEvent.Label, out int[] indexes)) counted = Work(indexes, pressed, keyEvent.Ticks);
-        if(binding.NativeMap.TryGetValue(keyEvent.Key, out indexes)) counted |= Work(indexes, pressed, keyEvent.Ticks);
+        if(!keyEvent.IsReplay && binding.NativeMap.TryGetValue(keyEvent.Key, out indexes)) counted |= Work(indexes, pressed, keyEvent.Ticks);
         if(counted && Enabled) KeyCountData.Instance.Save();
     }
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 190 | 조건을 만족할 때 뒤의 작업을 수행한다: if(binding.NativeMap.TryGetValue(keyEvent.Key, out indexes)) counted \|= Work(indexes, pressed, keyEvent.Ticks); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 225 | 실제 입력에만 native 코드 바인딩을 적용한다. | 기록의 코드 0을 실제 native key code로 사용하지 않는다. |

### @@ -246,11 +281,16 @@

```diff
@@ -246,11 +281,16 @@ public partial class KeyViewer {
         return counted;
     }
 
-    private readonly struct KeyEvent(KeyLabel label, ushort key, bool pressed, long ticks) {
+    private readonly struct KeyEvent(KeyLabel label, ushort key, bool pressed, long ticks, long nativeTicks, int generation, bool replay = false) {
         public readonly KeyLabel Label = label;
         public readonly ushort Key = key;
         public readonly bool Pressed = pressed;
         public readonly long Ticks = ticks;
+        public readonly long NativeTicks = nativeTicks;
+        public readonly int Generation = generation;
+        public readonly TUFReplayFrameRequest Request;
+        public readonly bool IsReplay = replay;
+        public KeyEvent(TUFReplayFrameRequest request, int generation) : this(default, 0, false, 0, 0, generation) => Request = request;
     }
 
     private sealed class KeyBinding(Dictionary<KeyLabel, int[]> labelMap, Dictionary<ushort, int[]> nativeMap) {
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 249 | 값 타입 데이터/큐 항목을 선언한다: private readonly struct KeyEvent(KeyLabel label, ushort key, bool pressed, long ticks) { | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 284 | 값 타입 데이터/큐 항목을 선언한다: private readonly struct KeyEvent(KeyLabel label, ushort key, bool pressed, long ticks, long nativeTicks, int generation, bool replay = false) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 289 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly long NativeTicks = nativeTicks; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 290 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly int Generation = generation; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 291 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly TUFReplayFrameRequest Request; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 292 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly bool IsReplay = replay; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 293 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public KeyEvent(TUFReplayFrameRequest request, int generation) : this(default, 0, false, 0, 0, generation) =&gt; Request = request; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |

### @@ -260,6 +300,31 @@

```diff
@@ -260,6 +300,31 @@ public partial class KeyViewer {
 
     public class KeyViewerUpdater : MonoBehaviour {
         private void Update() {
+            KeyViewer instance = Instance;
+            TUFReplayFrameRequest request = Volatile.Read(ref instance._processedFrame);
+            if(TUFReplaySession.IsActive && (request == null || request.IsCancelled || !ReferenceEquals(request, instance.tufReplayConnection?.PendingFrame))) return;
+            try {
+                using IDisposable scope = request == null ? null : instance.tufReplayConnection.EnterFrame(request);
+                instance.ResumeNativeInput();
+                Refresh();
+                if(Settings.useRain) RainManager.Refresh();
+                foreach(Key key in instance.Keys) {
+                    if(key == null) continue;
+                    key.UpdateKey();
+                    key.Value?.ApplyText();
+                    key.Text?.ApplyText();
+                }
+                if(request != null) {
+                    instance.tufReplayConnection.CompleteFrame(request);
+                    Interlocked.CompareExchange(ref instance._processedFrame, null, request);
+                }
+            } catch(Exception e) {
+                if(request != null) instance.tufReplayConnection?.ReportFailure(request, e);
+                else Main.Instance.LogException(e);
+            }
+        }
+
+        internal void Refresh() {
             KeyViewer instance = Instance;
             ConcurrentQueue<long> pressTimes = instance._pressTimes;
             if(pressTimes == null) return;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 303 | 오른쪽 결과를 왼쪽 상태에 저장한다: KeyViewer instance = Instance; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 304 | worker가 입력 처리를 끝낸 요청을 읽는다. | 입력 처리 전에 Unity가 완료를 보내지 않는다. |
| 추가 | 현재 305 | 처리 요청과 SDK 대기 요청이 같은 객체인지 확인한다. | 이전·취소된 요청을 현재 요청 대신 완료하지 않는다. |
| 추가 | 현재 306 | 작업을 실행하고 오류를 catch에 전달한다. | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 307 | namespace 또는 타입 별칭을 가져온다: IDisposable scope = request == null ? null : instance.tufReplayConnection.EnterFrame(request); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 308 | 비활성 SDK에서 기억한 실제 입력 상태를 기존 Work로 반영한다. | 종료 후 물리 상태와 표시 상태를 맞춘다. |
| 추가 | 현재 309 | 이 함수 호출/식 처리를 실행한다: Refresh(); | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 310 | RawRain을 graphic으로 만들고 레이닝을 갱신한다. | 레이닝 반영이 요청 완료보다 뒤로 밀리지 않는다. |
| 추가 | 현재 311 | 각 컬렉션 항목을 순회한다: foreach(Key key in instance.Keys) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 312 | 조건을 만족할 때 뒤의 작업을 수행한다: if(key == null) continue; | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 313 | 기존 키 표시 갱신을 실행한다. | worker가 요청한 표시를 Unity UI에 반영한다. |
| 추가 | 현재 314 | 대기 중인 값을 TMP 텍스트 객체에 적용한다. | 예약만 된 상태로 프레임을 완료하지 않는다. |
| 추가 | 현재 315 | 대기 중인 값을 TMP 텍스트 객체에 적용한다. | 예약만 된 상태로 프레임을 완료하지 않는다. |
| 추가 | 현재 316 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 317 | 조건을 만족할 때 뒤의 작업을 수행한다: if(request != null) { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 318 | 실제로 처리한 이 요청에만 UI 갱신 완료를 알린다. | 다른/이전 요청의 완료로 현재 캡처를 허용하지 않는다. |
| 추가 | 현재 319 | 값이 방금 처리한 요청이면 공유 요청을 비운다. | 더 새 요청을 덮어쓰지 않는다. |
| 추가 | 현재 320 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 321 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 322 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |
| 추가 | 현재 323 | 앞 조건이 거짓일 때의 처리다. | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |
| 추가 | 현재 324 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 325 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 326 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 327 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal void Refresh() { | 두 공급원을 기존 입력 worker/Work에 전달하고 정확한 요청의 UI 적용 후 완료한다. |

## JipperResourcePack/KeyViewerContents/KeyViewer.cs

현재 파일: [JipperResourcePack/KeyViewerContents/KeyViewer.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.cs)

기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다.

### @@ -110,19 +110,27 @@

```diff
@@ -110,19 +110,27 @@ public partial class KeyViewer : Feature {
         RebuildKeyBinding();
         StartEventListener();
         UpdateKeyLimit();
+        ConnectReplay();
     }
 
     protected override void OnDisable() {
         if(!KeyViewerObject) return;
+        tufReplayConnection?.Dispose();
+        tufReplayConnection = null;
+        EndRender();
         StopEventListener();
         AsyncInputHook.Release();
+        lock(_inputSync) {
+            KeyViewerSizeObject = null;
+            Keys = null;
+            _keyBinding = null;
+            _pressTimes = null;
+            Array.Clear(_keyState, 0, _keyState.Length);
+            _nativeResumeState = null;
+            _nativeResumeTimes = null;
+        }
         Object.Destroy(KeyViewerObject);
         KeyViewerObject = null;
-        KeyViewerSizeObject = null;
-        Keys = null;
-        _keyBinding = null;
-        _pressTimes = null;
-        Array.Clear(_keyState, 0, _keyState.Length);
     }
 
     protected override void OnGUI() {
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 113 | 정상 객체 준비 뒤 SDK 연결을 등록한다. | 준비 전 UI·큐로 callback이 들어오지 않는다. |
| 추가 | 현재 118 | SDK 참가 연결을 해제한다. | 폐기된 표시 요소를 Renderer가 계속 기다리지 않는다. |
| 추가 | 현재 119 | 오른쪽 결과를 왼쪽 상태에 저장한다: tufReplayConnection = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 120 | 남아 있는 재생 수명을 종료한다. | 폐기 경로에서도 공급원 전환/표시 정리를 수행한다. |
| 추가 | 현재 123 | 입력 처리·수명 전환·상태 폐기를 같은 lock으로 직렬화한다. | worker와 Unity 쪽이 같은 상태를 동시에 바꾸지 않는다. |
| 추가 | 현재 124 | 오른쪽 결과를 왼쪽 상태에 저장한다: KeyViewerSizeObject = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 125 | 오른쪽 결과를 왼쪽 상태에 저장한다: Keys = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 126 | 오른쪽 결과를 왼쪽 상태에 저장한다: _keyBinding = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 127 | 오른쪽 결과를 왼쪽 상태에 저장한다: _pressTimes = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 128 | 이 함수 호출/식 처리를 실행한다: Array.Clear(_keyState, 0, _keyState.Length); | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 129 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeState = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 130 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeTimes = null; | 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 추가 | 현재 131 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 삭제 | 이전 121 | 오른쪽 결과를 왼쪽 상태에 저장한다: KeyViewerSizeObject = null; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 삭제 | 이전 122 | 오른쪽 결과를 왼쪽 상태에 저장한다: Keys = null; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 삭제 | 이전 123 | 오른쪽 결과를 왼쪽 상태에 저장한다: _keyBinding = null; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 삭제 | 이전 124 | 오른쪽 결과를 왼쪽 상태에 저장한다: _pressTimes = null; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |
| 삭제 | 이전 125 | 이 함수 호출/식 처리를 실행한다: Array.Clear(_keyState, 0, _keyState.Length); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 생성·폐기 수명에 SDK 등록/해제와 worker 동기화를 붙인다. |

## JipperResourcePack/KeyViewerContents/Rain.cs

현재 파일: [JipperResourcePack/KeyViewerContents/Rain.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/Rain.cs)

레이닝이 캡처 Canvas의 로컬 평면·회전·크기를 따르게 한다.

### @@ -15,7 +15,7 @@

```diff
@@ -15,7 +15,7 @@ public class Rain {
         Pool = pool;
         GameObject rainPrefab = GameObject = new GameObject("Rain");
         RectTransform rainTransform = Transform = rainPrefab.AddComponent<RectTransform>();
-        rainTransform.SetParent(pool.Transform);
+        rainTransform.SetParent(pool.Transform, false);
         rainTransform.anchorMin = rainTransform.anchorMax = rainTransform.pivot = new Vector2(0.5f, 1);
         rainTransform.anchoredPosition = rainTransform.sizeDelta = Vector2.zero;
         rainTransform.localScale = Vector3.one;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 18 | 이 함수 호출/식 처리를 실행한다: rainTransform.SetParent(pool.Transform); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 레이닝이 캡처 Canvas의 로컬 평면·회전·크기를 따르게 한다. |
| 추가 | 현재 18 | world transform을 유지하지 않고 부모 기준으로 붙인다. | 레이닝이 캡처 Canvas 평면·회전·크기를 따른다. |

## JipperResourcePack/KeyViewerContents/RainManager.cs

현재 파일: [JipperResourcePack/KeyViewerContents/RainManager.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/RainManager.cs)

RawRain의 실제 Unity graphic 반영이 요청 완료보다 먼저 끝나게 한다.

### @@ -8,7 +8,10 @@

```diff
@@ -8,7 +8,10 @@ public class RainManager : MonoBehaviour {
     public readonly List<Rain> RainList = [];
     public readonly ConcurrentQueue<RawRain> RawRainQueue = new();
 
-    private void Update() {
+    // 평소와 렌더 모두 KeyViewerUpdater가 호출한다. 별도 MonoBehaviour.Update에서
+    // 다시 이동시키면 같은 요청에서 rain이 두 번 진행하거나 ACK 전에 화면이 바뀐다.
+    // worker가 만든 RawRain은 이 Unity 경계에서 실제 pooled graphic으로 바꾼다.
+    internal void Refresh() {
         while(RawRainQueue.TryDequeue(out RawRain rawRain)) {
             Rain rainComponent = rawRain.Key.RainPool.GetOrNewRain(rawRain.IsGhost);
             rainComponent.Image.color = rawRain.Key.Color switch {
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 11 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void Update() { | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. RawRain의 실제 Unity graphic 반영이 요청 완료보다 먼저 끝나게 한다. |
| 추가 | 현재 11 | 주석: 평소와 렌더 모두 KeyViewerUpdater가 호출한다. 별도 MonoBehaviour.Update에서 | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 12 | 주석: 다시 이동시키면 같은 요청에서 rain이 두 번 진행하거나 ACK 전에 화면이 바뀐다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 13 | 주석: worker가 만든 RawRain은 이 Unity 경계에서 실제 pooled graphic으로 바꾼다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 14 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal void Refresh() { | RawRain의 실제 Unity graphic 반영이 요청 완료보다 먼저 끝나게 한다. |

## JipperResourcePack/Main.cs

현재 파일: [JipperResourcePack/Main.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/Main.cs)

일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다.

### @@ -100,8 +100,16 @@

```diff
@@ -100,8 +100,16 @@ public class Main() : JAMod(typeof(ResourcePackSetting)) {
     }
     
     protected override void OnUpdate(float deltaTime) {
-        Overlay.Instance.UpdateTime();
-        Overlay.Instance.UpdateComboSize();
+        // 렌더의 표시 갱신은 요청 callback이 맡는다. 일반 Update에서도 실행하면
+        // game hook 앞에서 갱신하거나 같은 요청을 중복 진행할 수 있다.
+        if(!TUFReplay.Overlay.TUFReplaySession.IsActive) {
+            KeyViewer.Instance?.ResumeNativeInput();
+            Overlay.Instance.UpdateTime();
+            Overlay.Instance.UpdateComboSize();
+        }
+        // JMain의 기존 기능에는 native delta 또는 SDK delta를 직접 선택해 전달한다.
+        // Renderer의 추가 대기 Update에서는 SDK가 0을 주므로 UI 시간이 더 진행되지 않는다.
+        if(TUFReplay.Overlay.TUFReplaySession.IsActive) deltaTime = TUFReplay.Overlay.TUFReplaySession.DeltaTime;
         JMain.Update(deltaTime);
     }
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 103 | 이 함수 호출/식 처리를 실행한다: Overlay.Instance.UpdateTime(); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다. |
| 삭제 | 이전 104 | 이 함수 호출/식 처리를 실행한다: Overlay.Instance.UpdateComboSize(); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다. |
| 추가 | 현재 103 | 주석: 렌더의 표시 갱신은 요청 callback이 맡는다. 일반 Update에서도 실행하면 | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 104 | 주석: game hook 앞에서 갱신하거나 같은 요청을 중복 진행할 수 있다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 105 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!TUFReplay.Overlay.TUFReplaySession.IsActive) { | 일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다. |
| 추가 | 현재 106 | 비활성 SDK에서 기억한 실제 입력 상태를 기존 Work로 반영한다. | 종료 후 물리 상태와 표시 상태를 맞춘다. |
| 추가 | 현재 107 | 이 함수 호출/식 처리를 실행한다: Overlay.Instance.UpdateTime(); | 일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다. |
| 추가 | 현재 108 | 이 함수 호출/식 처리를 실행한다: Overlay.Instance.UpdateComboSize(); | 일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다. |
| 추가 | 현재 109 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 110 | 주석: JMain의 기존 기능에는 native delta 또는 SDK delta를 직접 선택해 전달한다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 111 | 주석: Renderer의 추가 대기 Update에서는 SDK가 0을 주므로 UI 시간이 더 진행되지 않는다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 112 | 조건을 만족할 때 뒤의 작업을 수행한다: if(TUFReplay.Overlay.TUFReplaySession.IsActive) deltaTime = TUFReplay.Overlay.TUFReplaySession.DeltaTime; | 일반 표시 갱신과 재생 요청 갱신의 중복을 막고 delta 공급원을 고른다. |

## JipperResourcePack/OverlayContents/Overlay.cs

현재 파일: [JipperResourcePack/OverlayContents/Overlay.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.cs)

기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다.

### @@ -9,10 +9,11 @@

```diff
@@ -9,10 +9,11 @@ using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 using Object = UnityEngine.Object;
+using TUFReplay.Overlay;
 
 namespace JipperResourcePack.OverlayContents;
 
-public class Overlay {
+public partial class Overlay {
     public static Overlay Instance;
     public IOverlayTextManager OverlayTextManager;
     public readonly GameObject GameObject;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 12 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 삭제 | 이전 15 | 타입을 선언한다: public class Overlay { | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 16 | 타입을 선언한다: public partial class Overlay { | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |

### @@ -62,6 +63,8 @@

```diff
@@ -62,6 +63,8 @@ public class Overlay {
     private float _lastSavedStartProgress = -1;
     public float LastMultiplier = 1f;
     private ComboTier _current;
+    private double _comboStartedAt;
+    private bool _comboAnimating;
 
     public Overlay() {
         Instance = this;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 66 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private double _comboStartedAt; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 67 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private bool _comboAnimating; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |

### @@ -85,6 +88,7 @@

```diff
@@ -85,6 +88,7 @@ public class Overlay {
         UpdateSize();
         Object.DontDestroyOnLoad(GameObject);
         if(ADOBase.controller is { paused: false } && ADOBase.conductor is { isGameWorld: true }) Show(0);
+        ConnectReplay();
     }
 
     public void OnChangePlayers() {
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 91 | 정상 객체 준비 뒤 SDK 연결을 등록한다. | 준비 전 UI·큐로 callback이 들어오지 않는다. |

### @@ -365,7 +369,8 @@

```diff
@@ -365,7 +369,8 @@ public class Overlay {
             if(!ProgressBar.LineTransform) return;
             OverlayTextManager.UpdateProgressBar(this);
         } catch (Exception e) {
-            Main.Instance.LogException(e);
+            if(TUFReplaySession.IsActive) tufReplayConnection?.ReportFailure(e);
+            else Main.Instance.LogException(e);
         }
     }
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 368 | 이 함수 호출/식 처리를 실행한다: Main.Instance.LogException(e); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 372 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |
| 추가 | 현재 373 | 앞 조건이 거짓일 때의 처리다. | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |

### @@ -476,9 +481,15 @@

```diff
@@ -476,9 +481,15 @@ public class Overlay {
         ComboText.text = combo.ToString();
         ComboText.color = UpdateComboColor(combo);
         if(bump) {
-            _stopwatch.Restart();
+            _comboAnimating = true;
+            if(TUFReplaySession.IsActive) _comboStartedAt = TUFReplaySession.ElapsedTime;
+            else {
+                _nativeComboElapsed = 0;
+                _stopwatch.Restart();
+            }
             UpdateComboSize();
         } else {
+            _comboAnimating = false;
             _stopwatch.Stop();
             ComboText.fontSize = 78;
             ComboTransform.anchoredPosition = new Vector2(0, 43.505f);
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 479 | 이 함수 호출/식 처리를 실행한다: _stopwatch.Restart(); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 484 | 애니메이션 진행 여부를 별도로 기록한다. | SDK 시계는 Stopwatch.IsRunning과 독립적이다. |
| 추가 | 현재 485 | SDK 시간에 맞춰 콤보 시작 원점을 지정한다. | 영상 경과 시간으로 애니메이션을 계산한다. |
| 추가 | 현재 486 | 앞 조건이 거짓일 때의 처리다. | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 487 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeComboElapsed = 0; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 488 | 이 함수 호출/식 처리를 실행한다: _stopwatch.Restart(); | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 489 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 492 | 애니메이션 진행 여부를 별도로 기록한다. | SDK 시계는 Stopwatch.IsRunning과 독립적이다. |

### @@ -490,22 +501,32 @@

```diff
@@ -490,22 +501,32 @@ public class Overlay {
         return Combo.Settings.ComboColor.GetColor((float) combo / Combo.Settings.ComboColorMax);
     }
 
-    public void UpdateComboSize() {
-        if(!_stopwatch.IsRunning || !GameObject.activeSelf) return;
-        double t = _stopwatch.Elapsed.TotalMilliseconds / 500;
+    public void UpdateComboSize(Action completed = null) {
+        if(!_comboAnimating || !GameObject.activeSelf) {
+            completed?.Invoke();
+            return;
+        }
+        double t = (TUFReplaySession.IsActive
+            ? TUFReplaySession.ElapsedTime - _comboStartedAt
+            : _nativeComboElapsed + _stopwatch.Elapsed.TotalSeconds) / 0.5;
         if(t > 1) {
             t = 1;
             _stopwatch.Stop();
+            _comboAnimating = false;
         }
         ComboText.fontSize = 30 * OutExpoChange(t) + 78;
-        Task.Yield().OnCompleted(UpdateComboLocation);
+        Task.Yield().OnCompleted(() => {
+            UpdateComboLocation();
+            completed?.Invoke();
+        });
     }
 
     private void UpdateComboLocation() {
         try {
             ComboTransform.anchoredPosition = new Vector2(0, ComboTextTransform.sizeDelta.y / 2);
         } catch (Exception e) {
-            Main.Instance.LogReportException("Failed to update combo location", e);
+            if(TUFReplaySession.IsActive) tufReplayConnection?.ReportFailure(e);
+            else Main.Instance.LogReportException("Failed to update combo location", e);
         }
     }
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 493 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void UpdateComboSize() { | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 삭제 | 이전 494 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_stopwatch.IsRunning \|\| !GameObject.activeSelf) return; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 삭제 | 이전 495 | 오른쪽 결과를 왼쪽 상태에 저장한다: double t = _stopwatch.Elapsed.TotalMilliseconds / 500; | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 504 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void UpdateComboSize(Action completed = null) { | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 505 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_comboAnimating \|\| !GameObject.activeSelf) { | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 506 | 등록된 완료 callback을 호출한다. | 지연 UI 반영이 끝났거나 반영할 작업이 없을 때 완료한다. |
| 추가 | 현재 507 | 함수를 종료하거나 식의 결과를 반환한다: return; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 508 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 509 | 오른쪽 결과를 왼쪽 상태에 저장한다: double t = (TUFReplaySession.IsActive | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 510 | 설정 주석/전처리 지시/스크립트 경계다: ? TUFReplaySession.ElapsedTime - _comboStartedAt | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 511 | 이 함수 호출/식 처리를 실행한다: : _nativeComboElapsed + _stopwatch.Elapsed.TotalSeconds) / 0.5; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 515 | 애니메이션 진행 여부를 별도로 기록한다. | SDK 시계는 Stopwatch.IsRunning과 독립적이다. |
| 삭제 | 이전 501 | 기존처럼 지연 callback으로 콤보 위치 갱신을 예약한다. | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 그 callback 실행 뒤에 완료를 알린다. |
| 추가 | 현재 518 | 기존처럼 지연 callback으로 콤보 위치 갱신을 예약한다. | 그 callback 실행 뒤에 완료를 알린다. |
| 추가 | 현재 519 | 기존 콤보 위치 보정을 실행한다. | 크기 변경 뒤 위치까지 적용한다. |
| 추가 | 현재 520 | 등록된 완료 callback을 호출한다. | 지연 UI 반영이 끝났거나 반영할 작업이 없을 때 완료한다. |
| 추가 | 현재 521 | callback/호출 본문을 닫고 등록 또는 호출을 마친다. | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 삭제 | 이전 508 | 이 함수 호출/식 처리를 실행한다: Main.Instance.LogReportException("Failed to update combo location", e); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 528 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |
| 추가 | 현재 529 | 앞 조건이 거짓일 때의 처리다. | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |

### @@ -572,6 +593,7 @@

```diff
@@ -572,6 +593,7 @@ public class Overlay {
     }
     
     public virtual void Show(int floor) {
+        if(!_presentationEnabled) return;
         // ReSharper disable once CompareOfFloatsByEqualityOperator
         if(_lastSavedStartProgress != -1) {
             if(!AutoOnceEnabled) PlayCount.SetBest(LastHash, _lastSavedStartProgress, OverlayTextManager.GetProgress(), LastMultiplier);
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 596 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_presentationEnabled) return; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |

### @@ -662,6 +684,9 @@

```diff
@@ -662,6 +684,9 @@ public class Overlay {
     }
 
     public void Destroy() {
+        tufReplayConnection?.Dispose();
+        tufReplayConnection = null;
+        EndRender();
         Object.Destroy(GameObject);
         GC.SuppressFinalize(this);
     }
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 687 | SDK 참가 연결을 해제한다. | 폐기된 표시 요소를 Renderer가 계속 기다리지 않는다. |
| 추가 | 현재 688 | 오른쪽 결과를 왼쪽 상태에 저장한다: tufReplayConnection = null; | 기존 콤보 시계를 SDK/native에서 선택하고 지연 위치 반영 뒤 완료한다. |
| 추가 | 현재 689 | 남아 있는 재생 수명을 종료한다. | 폐기 경로에서도 공급원 전환/표시 정리를 수행한다. |

## JipperResourcePack/OverlayContents/PlayCount.cs

현재 파일: [JipperResourcePack/OverlayContents/PlayCount.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/PlayCount.cs)

별도 Harmony 참조 alias로 필요한 타입만 사용하도록 빌드 의존을 맞춘다.

### @@ -6,7 +6,6 @@

```diff
@@ -6,7 +6,6 @@ using System.Runtime.CompilerServices;
 using System.Security.Cryptography;
 using System.Text;
 using ADOFAI;
-using HarmonyLib;
 using JALib.Tools.ByteTool;
 
 namespace JipperResourcePack.OverlayContents;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 9 | 기존 Harmony namespace import를 제거한다. | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. HarmonyLibrary 참조 alias를 사용하도록 빌드 의존을 바꿨다. |

### @@ -243,7 +242,7 @@

```diff
@@ -243,7 +242,7 @@ public static class PlayCount {
         public static implicit operator Hash(byte[] hash) => new(hash);
         public static implicit operator byte[](Hash hash) => hash.Data;
 
-        public override string ToString() => Data.Join(b => b.ToString("x2"), "");
+        public override string ToString() => string.Concat(Data.Select(b => b.ToString("x2")));
     }
 
     #endregion
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 246 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override string ToString() =&gt; Data.Join(b =&gt; b.ToString("x2"), ""); | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. 별도 Harmony 참조 alias로 필요한 타입만 사용하도록 빌드 의존을 맞춘다. |
| 추가 | 현재 245 | 바이트를 두 자리 소문자 16진수로 바꾸어 붙인다. | Harmony Join 의존 없이 같은 문자열을 만든다. |

## JipperResourcePack/OverlayContents/Status.cs

현재 파일: [JipperResourcePack/OverlayContents/Status.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Status.cs)

별도 Harmony 참조 alias로 필요한 타입만 사용하도록 빌드 의존을 맞춘다.

### @@ -3,7 +3,6 @@

```diff
@@ -3,7 +3,6 @@ using System.Collections.Generic;
 using System.Linq;
 using System.Reflection;
 using System.Reflection.Emit;
-using HarmonyLib;
 using JALib.Core;
 using JALib.Core.Patch;
 using JALib.Core.Setting;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 6 | 기존 Harmony namespace import를 제거한다. | 이전 코드에서 하던 일이다. 아래 추가 줄과 파일 설명을 함께 보면 교체 이유를 확인할 수 있다. HarmonyLibrary 참조 alias를 사용하도록 빌드 의존을 바꿨다. |

## README.md

현재 파일: [README.md](/Users/kgh/dev/src/jipper-resourcepack/README.md)

현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다.

### @@ -83,3 +83,72 @@

```diff
@@ -83,3 +83,72 @@
 ---
 # [Join Jipper's Discord!](https://discord.gg/qTbnPhY7YA)
 # [Join My Discord!](https://discord.jongyeol.kr)
+
+## TUFReplay rendering
+
+This fork bundles `TUFReplay.Overlay.dll` with the mod. It preserves the normal
+SkyHook input backend when no render is running. During an offline render, the
+key viewer receives recorded input events through its ordinary label binding,
+input worker, `ProcessKeyEvent` and `Work` path. A request barrier follows that
+input batch; the existing Unity updater applies key/text, KPS and rain work before
+acknowledging the request. Subframe press/releases keep their event timestamps.
+The recording supplies Unity KeyCodes, so platform-only native codes are not
+synthesized.
+
+Rendering uses the existing key UI, rain manager, KPS queue, accumulated counters
+and play-record dictionary. It does not create a temporary presentation, zero
+the counters, force the overlay visible, or restore an earlier display afterward.
+Normal game hooks continue to decide overlay initialization and visibility.
+Replay input runs through the existing counter/rain code and saves `KeyCount.dat`;
+normal play-record updates can also save attempts and best progress to `Plays.dat`.
+Rendering therefore affects persistent statistics just like another play.
+The original Stopwatch, bindings and input worker remain in place. Clock origins
+are aligned at source changes so existing rain, KPS and combo animations keep
+their age. Physical input is blocked during rendering; its held state is retained
+and reconciled through the ordinary input body when the native source resumes.
+Error exclusion hides the affected UI until session end without restoring counters.
+No additional TUFReplay dependency mod is required; JALib remains required.
+
+The SDK DLL is pinned in `libs/TUFReplay.Overlay.dll`. This mod references that
+binary directly; building requires no Renderer checkout or SDK source. To update
+the SDK, extract a `TUFReplay.Overlay-<version>.zip` distribution, verify its
+`SHA256SUMS`, and replace the DLL and XML documentation in `libs/` unchanged.
+
+```sh
+bash scripts/run.sh build
+bash scripts/run.sh package
+bash scripts/run.sh check
+```
+
+The build finds the standard macOS Steam installation by default. Set
+`ADOFAI_MANAGED_PATH`, `ADOFAI_MODS_PATH`, and optionally `OVERLAY_SDK_PATH` for
+other installations. `package` creates an overlay of the compiled DLLs in
+`artifacts/render-support/JipperResourcePack`; combine it with an existing
+JipperResourcePack package so its bootstrap, Unity bundle, fonts, and localization
+remain available. Building or packaging does not modify the installed game.
+
+For local game verification, `bash scripts/run.sh install` backs up the program
+DLLs to `/private/tmp/tuf-overlay-validation-originals/jipper`, records their
+original and installed SHA-256 hashes, and copies only the main DLL, common SDK,
+and four VersionSafe DLLs into the existing mod installation. It preserves the
+bootstrap, `Info.json`, assets, settings, and counter files. Set
+`VALIDATION_BACKUP_PATH` to use another backup directory.
+
+The upstream optional KeyboardChatterBlocker integration needs its DLL as a
+compile-time reference. If that mod is not installed, set
+`KEYBOARD_CHATTER_BLOCKER_DLL` to a separately downloaded or backed-up DLL;
+the build neither installs nor bundles it. Existing native-only bindings encoded
+by this mod cannot be reproduced by the current recording format, which contains
+Unity key names. The render samples the ordinary Unity key bindings.
+
+`check` also compiles the production rain constructor and pool in a small
+standalone regression test. It checks that new normal/ghost rain and pooled
+graphics stay on the parent Canvas plane after the Canvas has been moved, scaled,
+and rotated for camera capture. This does not replace an in-game visual check.
+
+The linked-source SDK integration checks exercise the production input worker,
+existing key/rain presentation and overlay connection with small Unity/SkyHook
+boundary stubs. They verify queued UI completion, accumulated counter saving,
+physical source resumption, reuse of the same UI objects and play state, cancelled
+delayed callbacks, and error/exclusion cleanup. They do not execute the game or
+verify captured pixels.
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 86 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 87 | 설정 주석/전처리 지시/스크립트 경계다: ## TUFReplay rendering | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 88 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 89 | 연결된 식·선언·설정의 일부다: This fork bundles ˋTUFReplay.Overlay.dllˋ with the mod. It preserves the normal | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 90 | 연결된 식·선언·설정의 일부다: SkyHook input backend when no render is running. During an offline render, the | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 91 | 연결된 식·선언·설정의 일부다: key viewer receives recorded input events through its ordinary label binding, | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 92 | 연결된 식·선언·설정의 일부다: input worker, ˋProcessKeyEventˋ and ˋWorkˋ path. A request barrier follows that | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 93 | 연결된 식·선언·설정의 일부다: input batch; the existing Unity updater applies key/text, KPS and rain work before | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 94 | 연결된 식·선언·설정의 일부다: acknowledging the request. Subframe press/releases keep their event timestamps. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 95 | 연결된 식·선언·설정의 일부다: The recording supplies Unity KeyCodes, so platform-only native codes are not | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 96 | 연결된 식·선언·설정의 일부다: synthesized. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 97 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 98 | 연결된 식·선언·설정의 일부다: Rendering uses the existing key UI, rain manager, KPS queue, accumulated counters | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 99 | 연결된 식·선언·설정의 일부다: and play-record dictionary. It does not create a temporary presentation, zero | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 100 | 연결된 식·선언·설정의 일부다: the counters, force the overlay visible, or restore an earlier display afterward. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 101 | 연결된 식·선언·설정의 일부다: Normal game hooks continue to decide overlay initialization and visibility. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 102 | 이 함수 호출/식 처리를 실행한다: Replay input runs through the existing counter/rain code and saves ˋKeyCount.datˋ; | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 103 | 연결된 식·선언·설정의 일부다: normal play-record updates can also save attempts and best progress to ˋPlays.datˋ. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 104 | 연결된 식·선언·설정의 일부다: Rendering therefore affects persistent statistics just like another play. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 105 | 연결된 식·선언·설정의 일부다: The original Stopwatch, bindings and input worker remain in place. Clock origins | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 106 | 연결된 식·선언·설정의 일부다: are aligned at source changes so existing rain, KPS and combo animations keep | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 107 | 연결된 식·선언·설정의 일부다: their age. Physical input is blocked during rendering; its held state is retained | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 108 | 연결된 식·선언·설정의 일부다: and reconciled through the ordinary input body when the native source resumes. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 109 | 연결된 식·선언·설정의 일부다: Error exclusion hides the affected UI until session end without restoring counters. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 110 | 연결된 식·선언·설정의 일부다: No additional TUFReplay dependency mod is required; JALib remains required. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 111 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 112 | 연결된 식·선언·설정의 일부다: The SDK DLL is pinned in ˋlibs/TUFReplay.Overlay.dllˋ. This mod references that | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 113 | 연결된 식·선언·설정의 일부다: binary directly; building requires no Renderer checkout or SDK source. To update | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 114 | 연결된 식·선언·설정의 일부다: the SDK, extract a ˋTUFReplay.Overlay-&lt;version&gt;.zipˋ distribution, verify its | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 115 | 연결된 식·선언·설정의 일부다: ˋSHA256SUMSˋ, and replace the DLL and XML documentation in ˋlibs/ˋ unchanged. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 116 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 117 | 연결된 식·선언·설정의 일부다: ˋˋˋsh | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 118 | 연결된 식·선언·설정의 일부다: bash scripts/run.sh build | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 119 | 연결된 식·선언·설정의 일부다: bash scripts/run.sh package | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 120 | 연결된 식·선언·설정의 일부다: bash scripts/run.sh check | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 121 | 연결된 식·선언·설정의 일부다: ˋˋˋ | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 122 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 123 | 연결된 식·선언·설정의 일부다: The build finds the standard macOS Steam installation by default. Set | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 124 | 연결된 식·선언·설정의 일부다: ˋADOFAI_MANAGED_PATHˋ, ˋADOFAI_MODS_PATHˋ, and optionally ˋOVERLAY_SDK_PATHˋ for | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 125 | 연결된 식·선언·설정의 일부다: other installations. ˋpackageˋ creates an overlay of the compiled DLLs in | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 126 | 연결된 식·선언·설정의 일부다: ˋartifacts/render-support/JipperResourcePackˋ; combine it with an existing | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 127 | 연결된 식·선언·설정의 일부다: JipperResourcePack package so its bootstrap, Unity bundle, fonts, and localization | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 128 | 연결된 식·선언·설정의 일부다: remain available. Building or packaging does not modify the installed game. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 129 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 130 | 연결된 식·선언·설정의 일부다: For local game verification, ˋbash scripts/run.sh installˋ backs up the program | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 131 | 연결된 식·선언·설정의 일부다: DLLs to ˋ/private/tmp/tuf-overlay-validation-originals/jipperˋ, records their | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 132 | 연결된 식·선언·설정의 일부다: original and installed SHA-256 hashes, and copies only the main DLL, common SDK, | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 133 | 연결된 식·선언·설정의 일부다: and four VersionSafe DLLs into the existing mod installation. It preserves the | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 134 | 연결된 식·선언·설정의 일부다: bootstrap, ˋInfo.jsonˋ, assets, settings, and counter files. Set | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 135 | 연결된 식·선언·설정의 일부다: ˋVALIDATION_BACKUP_PATHˋ to use another backup directory. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 136 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 137 | 연결된 식·선언·설정의 일부다: The upstream optional KeyboardChatterBlocker integration needs its DLL as a | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 138 | 연결된 식·선언·설정의 일부다: compile-time reference. If that mod is not installed, set | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 139 | 이 함수 호출/식 처리를 실행한다: ˋKEYBOARD_CHATTER_BLOCKER_DLLˋ to a separately downloaded or backed-up DLL; | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 140 | 연결된 식·선언·설정의 일부다: the build neither installs nor bundles it. Existing native-only bindings encoded | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 141 | 연결된 식·선언·설정의 일부다: by this mod cannot be reproduced by the current recording format, which contains | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 142 | 연결된 식·선언·설정의 일부다: Unity key names. The render samples the ordinary Unity key bindings. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 143 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 144 | 연결된 식·선언·설정의 일부다: ˋcheckˋ also compiles the production rain constructor and pool in a small | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 145 | 연결된 식·선언·설정의 일부다: standalone regression test. It checks that new normal/ghost rain and pooled | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 146 | 연결된 식·선언·설정의 일부다: graphics stay on the parent Canvas plane after the Canvas has been moved, scaled, | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 147 | 연결된 식·선언·설정의 일부다: and rotated for camera capture. This does not replace an in-game visual check. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 148 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 149 | 연결된 식·선언·설정의 일부다: The linked-source SDK integration checks exercise the production input worker, | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 150 | 연결된 식·선언·설정의 일부다: existing key/rain presentation and overlay connection with small Unity/SkyHook | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 151 | 연결된 식·선언·설정의 일부다: boundary stubs. They verify queued UI completion, accumulated counter saving, | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 152 | 연결된 식·선언·설정의 일부다: physical source resumption, reuse of the same UI objects and play state, cancelled | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 153 | 연결된 식·선언·설정의 일부다: delayed callbacks, and error/exclusion cleanup. They do not execute the game or | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 154 | 연결된 식·선언·설정의 일부다: verify captured pixels. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |

## Directory.Build.targets

현재 파일: [Directory.Build.targets](/Users/kgh/dev/src/jipper-resourcepack/Directory.Build.targets)

Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다.

### @@ -0,0 +1,16 @@

```diff
@@ -0,0 +1,16 @@
+<Project>
+    <Target Name="RemoveUnavailablePlatformReferences" BeforeTargets="ResolveAssemblyReferences" Condition="'$(DisableImplicitFrameworkReferences)' == 'true'">
+        <ItemGroup>
+            <Reference Remove="@(Reference)" Condition="'%(Reference.HintPath)' != '' and !Exists('%(Reference.HintPath)') and '%(Reference.Identity)' != 'TUFReplay.Overlay' and '%(Reference.Identity)' != 'JALib' and '%(Reference.Identity)' != 'KeyboardChatterBlocker'"/>
+        </ItemGroup>
+    </Target>
+    <!-- Unity supplies the framework assemblies for these explicit-reference mod
+         projects. Define this after the SDK targets so nested builds use them too. -->
+    <Target Name="GetReferenceAssemblyPaths" Condition="'$(DisableImplicitFrameworkReferences)' == 'true' and Exists('$(GameManagedPath)/mscorlib.dll')">
+        <PropertyGroup>
+            <_TargetFrameworkDirectories>$(GameManagedPath)</_TargetFrameworkDirectories>
+            <_FullFrameworkReferenceAssemblyPaths>$(GameManagedPath)</_FullFrameworkReferenceAssemblyPaths>
+            <TargetFrameworkDirectory>$(GameManagedPath)</TargetFrameworkDirectory>
+        </PropertyGroup>
+    </Target>
+</Project>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Project&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Target Name="RemoveUnavailablePlatformReferences" BeforeTargets="ResolveAssemblyReferences" Condition="'$(DisableImplicitFrameworkReferences)' == 'true'"&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Remove="@(Reference)" Condition="'%(Reference.HintPath)' != '' and !Exists('%(Reference.HintPath)') and '%(Reference.Identity)' != 'TUFReplay.Overlay' and '%(Reference.Identity)' != 'JALib' and '%(Reference.Identity)' != 'KeyboardChatterBlocker'"/&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Target&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;!-- Unity supplies the framework assemblies for these explicit-reference mod | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 8 | 연결된 식·선언·설정의 일부다: projects. Define this after the SDK targets so nested builds use them too. --&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 9 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Target Name="GetReferenceAssemblyPaths" Condition="'$(DisableImplicitFrameworkReferences)' == 'true' and Exists('$(GameManagedPath)/mscorlib.dll')"&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 11 | 프로젝트·리소스·설정 요소를 지정한다: &lt;_TargetFrameworkDirectories&gt;$(GameManagedPath)&lt;/_TargetFrameworkDirectories&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 12 | 프로젝트·리소스·설정 요소를 지정한다: &lt;_FullFrameworkReferenceAssemblyPaths&gt;$(GameManagedPath)&lt;/_FullFrameworkReferenceAssemblyPaths&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 13 | 프로젝트·리소스·설정 요소를 지정한다: &lt;TargetFrameworkDirectory&gt;$(GameManagedPath)&lt;/TargetFrameworkDirectory&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 14 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 15 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Target&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |
| 추가 | 현재 16 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Project&gt; | Unity Managed framework 어셈블리로 빌드하고 현재 플랫폼의 참조를 선택한다. |

## JipperResourcePack/HarmonyAlias.cs

현재 파일: [JipperResourcePack/HarmonyAlias.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/HarmonyAlias.cs)

0Harmony의 framework helper와 다른 참조의 충돌을 피하고 CodeInstruction을 공개한다.

### @@ -0,0 +1,4 @@

```diff
@@ -0,0 +1,4 @@
+extern alias HarmonyLibrary;
+
+// Harmony may embed framework helpers; expose only the API this mod uses.
+global using CodeInstruction = HarmonyLibrary::HarmonyLib.CodeInstruction;
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 격리된 어셈블리 참조 이름을 선언한다. | Harmony 전체를 기본 참조에 섞지 않는다. |
| 추가 | 현재 2 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 3 | 주석: Harmony may embed framework helpers; expose only the API this mod uses. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 4 | 필요한 타입 별칭을 프로젝트 전역에 공개한다. | 기존 CodeInstruction 사용처를 유지한다. |

## JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs

현재 파일: [JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs)

SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다.

### @@ -0,0 +1,110 @@

```diff
@@ -0,0 +1,110 @@
+using System;
+using System.Collections.Concurrent;
+using System.Threading;
+using SkyHook;
+using TUFReplay.Overlay;
+using UnityEngine;
+
+namespace JipperResourcePack.KeyViewerContents;
+
+public partial class KeyViewer {
+    // Replay changes the input and clock sources, not the viewer or its counters.
+    private TUFReplayConnection tufReplayConnection;
+    private bool _presentationEnabled = true, _visibleBeforeExclusion;
+    private long _replayTickOrigin, _nativeTickOffset;
+    private bool[] _nativeResumeState;
+    private long[] _nativeResumeTimes;
+    private TUFReplayFrameRequest _processedFrame;
+
+    private void ConnectReplay() {
+        tufReplayConnection = new TUFReplayConnection("JipperResourcePack.KeyViewer", "JipperResourcePack KeyViewer");
+        tufReplayConnection.OnSessionStarted += _ => BeginRender();
+        tufReplayConnection.OnSessionEnded += EndRender;
+        tufReplayConnection.OnPresentationChanged += visible => {
+            if(!visible && _presentationEnabled) _visibleBeforeExclusion = KeyViewerSizeObject.activeSelf;
+            _presentationEnabled = visible;
+            KeyViewerSizeObject?.SetActive(visible);
+        };
+        tufReplayConnection.OnKeyEvent += input => EnqueueInput(new KeyEvent(
+            VersionSafe.UnityKeyToSkyHookKey((KeyCode)input.Key), 0, input.IsPressed,
+            _replayTickOrigin + checked((long)(input.ElapsedTime * TimeSpan.TicksPerSecond)), 0, _inputGeneration,
+            replay: true));
+        tufReplayConnection.OnFrameRequested += request => EnqueueInput(new KeyEvent(request, _inputGeneration));
+    }
+
+    private void BeginRender() {
+        lock(_inputSync) {
+            if(_renderActive) throw new InvalidOperationException("A key viewer render is already active.");
+            _replayTickOrigin = NativeTicks + _nativeTickOffset - checked((long)(TUFReplaySession.ElapsedTime * TimeSpan.TicksPerSecond));
+            _renderActive = true;
+            _inputGeneration++;
+            InitializeNativeResumeState();
+            DrainNativeInput();
+            for(int i = 0; i < _keyState.Length; i++)
+                if(_keyState[i]) Work(new[] { i }, false, CurrentTicks);
+        }
+    }
+
+    private void InitializeNativeResumeState() {
+        _nativeResumeState ??= (bool[])_keyState.Clone();
+        if(_nativeResumeTimes != null) return;
+        _nativeResumeTimes = new long[GhostOutIndex];
+        for(int i = 0; i < _nativeResumeTimes.Length; i++) _nativeResumeTimes[i] = long.MinValue;
+    }
+
+    private void DrainNativeInput() {
+        ConcurrentQueue<KeyEvent> queue = _eventQueue;
+        if(queue == null) return;
+        while(queue.TryDequeue(out KeyEvent input))
+            if(!input.IsReplay && input.Request == null)
+                RememberNativeInput(input.Label, input.Key, input.Pressed, input.NativeTicks);
+    }
+
+    private void RememberNativeInput(KeyLabel label, ushort key, bool pressed, long nativeTicks) {
+        lock(_inputSync) {
+            KeyBinding binding = _keyBinding;
+            if(binding == null) return;
+            InitializeNativeResumeState();
+            if(binding.LabelMap.TryGetValue(label, out int[] indexes)) SetState(indexes);
+            if(binding.NativeMap.TryGetValue(key, out indexes)) SetState(indexes);
+            return;
+            void SetState(int[] keys) {
+                foreach(int index in keys) {
+                    if(nativeTicks < _nativeResumeTimes[index]) continue;
+                    _nativeResumeTimes[index] = nativeTicks;
+                    _nativeResumeState[index] = pressed;
+                }
+            }
+        }
+    }
+
+    internal void ResumeNativeInput() {
+        if(_nativeResumeState == null || TUFReplaySession.IsActive) return;
+        lock(_inputSync) {
+            bool[] state = _nativeResumeState;
+            if(state == null || _renderActive || TUFReplaySession.IsActive || Keys == null) return;
+            _nativeResumeState = null;
+            long ticks = CurrentTicks;
+            bool counted = false;
+            for(int i = 0; i < state.Length; i++)
+                if(_keyState[i] != state[i]) counted |= Work(new[] { i }, state[i], ticks);
+            if(counted) KeyCountData.Instance.Save();
+            _nativeResumeTimes = null;
+        }
+    }
+
+    private void EndRender() {
+        lock(_inputSync) {
+            if(!_renderActive) return;
+            DrainNativeInput();
+            _nativeTickOffset = CurrentTicks - NativeTicks;
+            _eventOffsetTicks = NoEventOffset;
+            _inputGeneration++;
+            Volatile.Write(ref _processedFrame, null);
+            _renderActive = false;
+            if(!_presentationEnabled) KeyViewerSizeObject?.SetActive(_visibleBeforeExclusion);
+            _presentationEnabled = true;
+        }
+    }
+
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: System; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: System.Collections.Concurrent; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 3 | namespace 또는 타입 별칭을 가져온다: System.Threading; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 4 | namespace 또는 타입 별칭을 가져온다: SkyHook; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 5 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 추가 | 현재 6 | namespace 또는 타입 별칭을 가져온다: UnityEngine; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 7 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 8 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.KeyViewerContents; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 9 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 10 | 타입을 선언한다: public partial class KeyViewer { | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 11 | 주석: Replay changes the input and clock sources, not the viewer or its counters. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 12 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private TUFReplayConnection tufReplayConnection; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 13 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private bool _presentationEnabled = true, _visibleBeforeExclusion; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private long _replayTickOrigin, _nativeTickOffset; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 15 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private bool[] _nativeResumeState; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private long[] _nativeResumeTimes; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 17 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private TUFReplayFrameRequest _processedFrame; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 18 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 19 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void ConnectReplay() { | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 20 | 오른쪽 결과를 왼쪽 상태에 저장한다: tufReplayConnection = new TUFReplayConnection("JipperResourcePack.KeyViewer", "JipperResourcePack KeyViewer"); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 21 | 렌더 세션 시작 → BeginRender를 실행한다. | 공급원 전환을 SDK 수명에 연결한다. |
| 추가 | 현재 22 | 렌더 세션 종료 → EndRender를 실행한다. | 성공·취소·오류 뒤에도 실제 공급원으로 돌아간다. |
| 추가 | 현재 23 | 포함·제외 변경을 표시 활성 상태에 반영한다. | 문제 표시 요소를 제외하면 화면에서도 숨긴다. |
| 추가 | 현재 24 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!visible &amp;&amp; _presentationEnabled) _visibleBeforeExclusion = KeyViewerSizeObject.activeSelf; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: _presentationEnabled = visible; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 26 | 이 함수 호출/식 처리를 실행한다: KeyViewerSizeObject?.SetActive(visible); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 27 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 28 | 재생 키 이벤트 → 기존 KeyEvent 큐에 전달한다. | 기존 입력 바인딩과 worker 본체를 사용한다. |
| 추가 | 현재 29 | Unity 기록 키를 기존 SkyHook label로 변환한다. | 운영체제 native code를 만들어내지 않고 기존 LabelMap을 쓴다. |
| 추가 | 현재 30 | SDK 경과 초를 기존 tick 단위로 바꾼다. | 같은 rain/KPS 계산식에 시각을 공급한다. |
| 추가 | 현재 31 | 이 큐 항목은 재생 입력이라고 표시한다. | 실제 입력의 설정 변경/native binding 경로와 구분한다. |
| 추가 | 현재 32 | 요청이 오면 이 요청의 갱신·완료 경로를 실행한다. | 키뷰어는 FIFO 표식, 오버레이는 game hook 뒤 갱신으로 연결한다. |
| 추가 | 현재 33 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 34 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 35 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void BeginRender() { | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 36 | 입력 처리·수명 전환·상태 폐기를 같은 lock으로 직렬화한다. | worker와 Unity 쪽이 같은 상태를 동시에 바꾸지 않는다. |
| 추가 | 현재 37 | 오류를 발생시켜 현재 작업을 실패시킨다. | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 38 | 현재 native 표시 시각에 SDK 시계 원점을 맞춘다. | 진행 중인 rain/KPS 상태를 유지한다. |
| 추가 | 현재 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: _renderActive = true; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 40 | 입력 세대를 하나 올린다. | 이전 공급원에서 넣은 큐 항목을 구분한다. |
| 추가 | 현재 41 | 복귀용 키 상태·시각 배열을 필요할 때 준비한다. | 기능 상태 전체 snapshot 대신 입력 복귀 데이터만 보관한다. |
| 추가 | 현재 42 | 경계에 남은 큐를 비우고 실제 이벤트만 복귀 상태에 합친다. | 과거 재생 전이/요청을 다음 상태로 넘기지 않는다. |
| 추가 | 현재 43 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; _keyState.Length; i++) | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 44 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_keyState[i]) Work(new[] { i }, false, CurrentTicks); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 45 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 46 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 47 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 48 | 복귀용 키 상태·시각 배열을 필요할 때 준비한다. | 기능 상태 전체 snapshot 대신 입력 복귀 데이터만 보관한다. |
| 추가 | 현재 49 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeState ??= (bool[])_keyState.Clone(); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 50 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_nativeResumeTimes != null) return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeTimes = new long[GhostOutIndex]; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 52 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; _nativeResumeTimes.Length; i++) _nativeResumeTimes[i] = long.MinValue; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 53 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 54 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 55 | 경계에 남은 큐를 비우고 실제 이벤트만 복귀 상태에 합친다. | 과거 재생 전이/요청을 다음 상태로 넘기지 않는다. |
| 추가 | 현재 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: ConcurrentQueue&lt;KeyEvent&gt; queue = _eventQueue; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 57 | 조건을 만족할 때 뒤의 작업을 수행한다: if(queue == null) return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 58 | 조건이 참인 동안 반복한다: while(queue.TryDequeue(out KeyEvent input)) | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 59 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!input.IsReplay &amp;&amp; input.Request == null) | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 60 | 실제 키의 최신 상태를 복귀용으로 기억한다. | 실제 입력을 영상에 섞지 않고 종료 후 복귀한다. |
| 추가 | 현재 61 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 62 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 63 | 실제 키의 최신 상태를 복귀용으로 기억한다. | 실제 입력을 영상에 섞지 않고 종료 후 복귀한다. |
| 추가 | 현재 64 | 입력 처리·수명 전환·상태 폐기를 같은 lock으로 직렬화한다. | worker와 Unity 쪽이 같은 상태를 동시에 바꾸지 않는다. |
| 추가 | 현재 65 | 오른쪽 결과를 왼쪽 상태에 저장한다: KeyBinding binding = _keyBinding; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 66 | 조건을 만족할 때 뒤의 작업을 수행한다: if(binding == null) return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 67 | 복귀용 키 상태·시각 배열을 필요할 때 준비한다. | 기능 상태 전체 snapshot 대신 입력 복귀 데이터만 보관한다. |
| 추가 | 현재 68 | 조건을 만족할 때 뒤의 작업을 수행한다: if(binding.LabelMap.TryGetValue(label, out int[] indexes)) SetState(indexes); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 69 | 조건을 만족할 때 뒤의 작업을 수행한다: if(binding.NativeMap.TryGetValue(key, out indexes)) SetState(indexes); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 70 | 함수를 종료하거나 식의 결과를 반환한다: return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 71 | 연결된 식·선언·설정의 일부다: void SetState(int[] keys) { | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 72 | 각 컬렉션 항목을 순회한다: foreach(int index in keys) { | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 73 | 이미 기억한 상태보다 오래된 실제 이벤트이면 무시한다. | 늦은 worker 항목이 최신 hook 상태를 덮어쓰지 않는다. |
| 추가 | 현재 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeTimes[index] = nativeTicks; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 75 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeState[index] = pressed; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 76 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 77 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 78 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 79 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 80 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 81 | 비활성 SDK에서 기억한 실제 입력 상태를 기존 Work로 반영한다. | 종료 후 물리 상태와 표시 상태를 맞춘다. |
| 추가 | 현재 82 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_nativeResumeState == null \|\| TUFReplaySession.IsActive) return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 83 | 입력 처리·수명 전환·상태 폐기를 같은 lock으로 직렬화한다. | worker와 Unity 쪽이 같은 상태를 동시에 바꾸지 않는다. |
| 추가 | 현재 84 | 오른쪽 결과를 왼쪽 상태에 저장한다: bool[] state = _nativeResumeState; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 85 | 조건을 만족할 때 뒤의 작업을 수행한다: if(state == null \|\| _renderActive \|\| TUFReplaySession.IsActive \|\| Keys == null) return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 86 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeState = null; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 87 | 오른쪽 결과를 왼쪽 상태에 저장한다: long ticks = CurrentTicks; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 88 | 오른쪽 결과를 왼쪽 상태에 저장한다: bool counted = false; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 89 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; state.Length; i++) | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 90 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_keyState[i] != state[i]) counted \|= Work(new[] { i }, state[i], ticks); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 91 | 기존 누적 카운터의 저장 경로를 실행한다. | 현재 구현은 재생 카운터도 저장한다. 저장 억제 코드가 아니다. |
| 추가 | 현재 92 | 오른쪽 결과를 왼쪽 상태에 저장한다: _nativeResumeTimes = null; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 93 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 94 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 95 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 96 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void EndRender() { | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 97 | 입력 처리·수명 전환·상태 폐기를 같은 lock으로 직렬화한다. | worker와 Unity 쪽이 같은 상태를 동시에 바꾸지 않는다. |
| 추가 | 현재 98 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_renderActive) return; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 99 | 경계에 남은 큐를 비우고 실제 이벤트만 복귀 상태에 합친다. | 과거 재생 전이/요청을 다음 상태로 넘기지 않는다. |
| 추가 | 현재 100 | 표시 시각과 실제 Stopwatch 시각의 차이를 기억한다. | 실제 시계로 돌아갈 때 rain/KPS 나이가 튀지 않는다. |
| 추가 | 현재 101 | 이벤트 시계 원점 보정을 다음 native 입력에서 다시 계산한다. | 재생 종료 뒤 실제 시계 기준으로 정렬한다. |
| 추가 | 현재 102 | 입력 세대를 하나 올린다. | 이전 공급원에서 넣은 큐 항목을 구분한다. |
| 추가 | 현재 103 | 처리 완료 요청을 다른 스레드가 볼 수 있게 공개하거나 비운다. | worker 처리 결과를 Unity 경계로 전달한다. |
| 추가 | 현재 104 | 오른쪽 결과를 왼쪽 상태에 저장한다: _renderActive = false; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 105 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_presentationEnabled) KeyViewerSizeObject?.SetActive(_visibleBeforeExclusion); | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 106 | 오른쪽 결과를 왼쪽 상태에 저장한다: _presentationEnabled = true; | SDK 입력·수명·시계를 기존 기능에 연결하고 실제 입력으로 복귀한다. |
| 추가 | 현재 107 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 108 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 109 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 110 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## JipperResourcePack/OverlayContents/Overlay.Render.cs

현재 파일: [JipperResourcePack/OverlayContents/Overlay.Render.cs](/Users/kgh/dev/src/jipper-resourcepack/JipperResourcePack/OverlayContents/Overlay.Render.cs)

기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다.

### @@ -0,0 +1,54 @@

```diff
@@ -0,0 +1,54 @@
+using System;
+using TUFReplay.Overlay;
+
+namespace JipperResourcePack.OverlayContents;
+
+public partial class Overlay {
+    private TUFReplayConnection tufReplayConnection;
+    private bool _presentationEnabled = true, _visibleBeforeExclusion;
+    private double _nativeComboElapsed;
+    private bool _replayClockActive;
+
+    private void ConnectReplay() {
+        tufReplayConnection = new TUFReplayConnection("JipperResourcePack.Overlay", "JipperResourcePack Overlay");
+        tufReplayConnection.OnSessionStarted += _ => BeginRender();
+        tufReplayConnection.OnSessionEnded += EndRender;
+        tufReplayConnection.OnPresentationChanged += visible => {
+            if(!visible && _presentationEnabled) _visibleBeforeExclusion = GameObject.activeSelf;
+            _presentationEnabled = visible;
+            GameObject.SetActive(visible);
+        };
+        // 기존 game hook 뒤에서 같은 UpdateTime/UpdateComboSize를 실행한다.
+        // 지연 callback은 정확한 요청과 연결을 보관해 취소/다음 세션에 섞이지 않게 한다.
+        TUFReplayConnection connection = tufReplayConnection;
+        connection.OnFrameRequested += request => VersionSafe.RunAfter(() => {
+            if(request.IsCancelled) return;
+            try {
+                // The normal deferred layout callback completes this exact request.
+                using(connection.EnterFrame(request)) {
+                    UpdateTime();
+                    UpdateComboSize(() => connection.CompleteFrame(request));
+                }
+            } catch(Exception exception) { connection.ReportFailure(request, exception); }
+        });
+    }
+
+    private void BeginRender() {
+        _replayClockActive = true;
+        // Preserve animation age when changing clocks, without resetting the view.
+        if(_comboAnimating)
+            _comboStartedAt = TUFReplaySession.ElapsedTime - (_nativeComboElapsed + _stopwatch.Elapsed.TotalSeconds);
+    }
+
+    private void EndRender() {
+        if(!_replayClockActive) return;
+        _replayClockActive = false;
+        // Retain the current animation and play state; only change its clock source.
+        if(_comboAnimating && TUFReplaySession.IsActive) {
+            _nativeComboElapsed = TUFReplaySession.ElapsedTime - _comboStartedAt;
+            _stopwatch.Restart();
+        }
+        if(!_presentationEnabled) GameObject.SetActive(_visibleBeforeExclusion);
+        _presentationEnabled = true;
+    }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 2 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 추가 | 현재 3 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 4 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.OverlayContents; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 5 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 6 | 타입을 선언한다: public partial class Overlay { | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 7 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private TUFReplayConnection tufReplayConnection; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private bool _presentationEnabled = true, _visibleBeforeExclusion; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private double _nativeComboElapsed; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private bool _replayClockActive; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 11 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 12 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void ConnectReplay() { | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: tufReplayConnection = new TUFReplayConnection("JipperResourcePack.Overlay", "JipperResourcePack Overlay"); | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 14 | 렌더 세션 시작 → BeginRender를 실행한다. | 공급원 전환을 SDK 수명에 연결한다. |
| 추가 | 현재 15 | 렌더 세션 종료 → EndRender를 실행한다. | 성공·취소·오류 뒤에도 실제 공급원으로 돌아간다. |
| 추가 | 현재 16 | 포함·제외 변경을 표시 활성 상태에 반영한다. | 문제 표시 요소를 제외하면 화면에서도 숨긴다. |
| 추가 | 현재 17 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!visible &amp;&amp; _presentationEnabled) _visibleBeforeExclusion = GameObject.activeSelf; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 18 | 오른쪽 결과를 왼쪽 상태에 저장한다: _presentationEnabled = visible; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 19 | 이 함수 호출/식 처리를 실행한다: GameObject.SetActive(visible); | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 20 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 21 | 주석: 기존 game hook 뒤에서 같은 UpdateTime/UpdateComboSize를 실행한다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 22 | 주석: 지연 callback은 정확한 요청과 연결을 보관해 취소/다음 세션에 섞이지 않게 한다. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: TUFReplayConnection connection = tufReplayConnection; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 24 | 요청이 오면 이 요청의 갱신·완료 경로를 실행한다. | 키뷰어는 FIFO 표식, 오버레이는 game hook 뒤 갱신으로 연결한다. |
| 추가 | 현재 25 | 취소된 요청이면 그 작업을 실행하지 않는다. | 취소 뒤 남은 지연 callback을 구분한다. |
| 추가 | 현재 26 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 27 | 주석: The normal deferred layout callback completes this exact request. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 28 | 이 갱신 범위에서 요청의 시간·delta를 읽는다. | 대기 Update의 0 delta와 요청 갱신의 delta를 구분한다. |
| 추가 | 현재 29 | 이 함수 호출/식 처리를 실행한다: UpdateTime(); | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 30 | 실제로 처리한 이 요청에만 UI 갱신 완료를 알린다. | 다른/이전 요청의 완료로 현재 캡처를 허용하지 않는다. |
| 추가 | 현재 31 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 32 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |
| 추가 | 현재 33 | callback/호출 본문을 닫고 등록 또는 호출을 마친다. | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 34 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 35 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 36 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void BeginRender() { | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 37 | 오른쪽 결과를 왼쪽 상태에 저장한다: _replayClockActive = true; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 38 | 주석: Preserve animation age when changing clocks, without resetting the view. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 39 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_comboAnimating) | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 40 | SDK 시간에 맞춰 콤보 시작 원점을 지정한다. | 영상 경과 시간으로 애니메이션을 계산한다. |
| 추가 | 현재 41 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 42 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 43 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void EndRender() { | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 44 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_replayClockActive) return; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: _replayClockActive = false; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 46 | 주석: Retain the current animation and play state; only change its clock source. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 47 | 조건을 만족할 때 뒤의 작업을 수행한다: if(_comboAnimating &amp;&amp; TUFReplaySession.IsActive) { | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 48 | 재생에서 진행된 콤보 나이를 native 시계에 넘긴다. | 같은 애니메이션을 native에서 이어간다. |
| 추가 | 현재 49 | 이 함수 호출/식 처리를 실행한다: _stopwatch.Restart(); | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 50 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 51 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!_presentationEnabled) GameObject.SetActive(_visibleBeforeExclusion); | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: _presentationEnabled = true; | 기존 게임 hook 뒤의 표시 갱신을 해당 SDK 요청 완료로 연결한다. |
| 추가 | 현재 53 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 54 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## libs/LICENSE-EXCEPTION

현재 파일: [libs/LICENSE-EXCEPTION](/Users/kgh/dev/src/jipper-resourcepack/libs/LICENSE-EXCEPTION)

배포물의 ADOFAI/Unity linking exception을 그대로 동봉한다.

- 크기: 862 bytes
- SHA-256: 94cd773e1a032fae6486945712f2a209a9bbd04a2602f25a8d2392b4ca006c0a
- 코드 줄별 diff 대상이 아닌 바이너리/배포 라이선스다.

## libs/LICENSE.md

현재 파일: [libs/LICENSE.md](/Users/kgh/dev/src/jipper-resourcepack/libs/LICENSE.md)

SDK 배포물의 라이선스를 그대로 동봉한다.

- 크기: 1128 bytes
- SHA-256: 9ce3aaeea2d34d9c4764cf793be6ea086b852a97f8034c1c953572f63312b1f5
- 코드 줄별 diff 대상이 아닌 바이너리/배포 라이선스다.

## libs/README.md

현재 파일: [libs/README.md](/Users/kgh/dev/src/jipper-resourcepack/libs/README.md)

현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다.

### @@ -0,0 +1,13 @@

```diff
@@ -0,0 +1,13 @@
+# Overlay SDK binary dependency
+
+This directory pins TUFReplay.Overlay SDK 2.0.0 (`netstandard2.0`).
+`TUFReplay.Overlay.dll` is referenced directly and bundled with the mod.
+No Renderer checkout or SDK source project is needed to build this repository.
+
+The binary comes from the standalone `TUFReplay.Overlay-2.0.0.zip` distribution.
+Its SHA-256 is `fadf1491f0018481c6774050e6d9f42fb039eec650cf215b07d87eccf4e841fc`.
+The adjacent XML file supplies API documentation; `LICENSE.md` and
+`LICENSE-EXCEPTION` retain the distribution's license terms.
+
+To update, verify the new distribution's `SHA256SUMS`, replace the DLL, XML and
+license files unchanged, and update the version and digest recorded here.
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 설정 주석/전처리 지시/스크립트 경계다: # Overlay SDK binary dependency | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 2 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 3 | 연결된 식·선언·설정의 일부다: This directory pins TUFReplay.Overlay SDK 2.0.0 (ˋnetstandard2.0ˋ). | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 4 | 연결된 식·선언·설정의 일부다: ˋTUFReplay.Overlay.dllˋ is referenced directly and bundled with the mod. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 5 | 연결된 식·선언·설정의 일부다: No Renderer checkout or SDK source project is needed to build this repository. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 6 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 7 | 연결된 식·선언·설정의 일부다: The binary comes from the standalone ˋTUFReplay.Overlay-2.0.0.zipˋ distribution. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 8 | 연결된 식·선언·설정의 일부다: Its SHA-256 is ˋfadf1491f0018481c6774050e6d9f42fb039eec650cf215b07d87eccf4e841fcˋ. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 9 | 연결된 식·선언·설정의 일부다: The adjacent XML file supplies API documentation; ˋLICENSE.mdˋ and | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 10 | 연결된 식·선언·설정의 일부다: ˋLICENSE-EXCEPTIONˋ retain the distribution's license terms. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 11 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 12 | 연결된 식·선언·설정의 일부다: To update, verify the new distribution's ˋSHA256SUMSˋ, replace the DLL, XML and | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |
| 추가 | 현재 13 | 연결된 식·선언·설정의 일부다: license files unchanged, and update the version and digest recorded here. | 현재 구현·배포·검증 범위를 설명한다. 문서 자체는 실행되지 않는다. |

## libs/TUFReplay.Overlay.dll

현재 파일: [libs/TUFReplay.Overlay.dll](/Users/kgh/dev/src/jipper-resourcepack/libs/TUFReplay.Overlay.dll)

SDK 2.0.0의 동봉 바이너리다. 직접 참조하며 Renderer 소스 ProjectReference를 요구하지 않는다.

- 크기: 23552 bytes
- SHA-256: fadf1491f0018481c6774050e6d9f42fb039eec650cf215b07d87eccf4e841fc
- 코드 줄별 diff 대상이 아닌 바이너리/배포 라이선스다.

## libs/TUFReplay.Overlay.xml

현재 파일: [libs/TUFReplay.Overlay.xml](/Users/kgh/dev/src/jipper-resourcepack/libs/TUFReplay.Overlay.xml)

배포물의 API 설명·라이선스·설정을 제공한다.

### @@ -0,0 +1,116 @@

```diff
@@ -0,0 +1,116 @@
+<?xml version="1.0"?>
+<doc>
+    <assembly>
+        <name>TUFReplay.Overlay</name>
+    </assembly>
+    <members>
+        <member name="T:TUFReplay.Overlay.TUFReplayConnection">
+            <summary>모드의 등록·수명·입력·요청 완료를 연결한다. 기존 입력 backend나 Update 구조를 강제하지 않는다.</summary>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplayConnection.PendingFrame">
+            <summary>아직 끝내지 않은 이 연결의 요청. 별도 frame callback 없이 기존 Update에서도 처리할 수 있다.</summary>
+        </member>
+        <member name="E:TUFReplay.Overlay.TUFReplayConnection.OnSessionStarted">
+            <summary>Lifecycle and data callbacks are dispatched on the renderer's Unity thread.</summary>
+        </member>
+        <member name="E:TUFReplay.Overlay.TUFReplayConnection.OnPresentationChanged">
+            <summary>제외 시 false를 받는다. 렌더 표시를 숨기되 live 표시는 세션 종료까지 격리한다.</summary>
+        </member>
+        <member name="E:TUFReplay.Overlay.TUFReplayConnection.OnKeyEvent">
+            <summary>기록 순서로 입력을 전달한다. 이 콜백의 상태 조회에는 해당 전이까지 반영되어 있다.</summary>
+        </member>
+        <member name="E:TUFReplay.Overlay.TUFReplayConnection.OnFrameRequested">
+            <summary>이번 요청의 모든 입력 전달 후 발생한다. 최종 held 상태로 기존 갱신을 실행하고 완료한다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayConnection.CompleteFrame(TUFReplay.Overlay.TUFReplayFrameRequest)">
+            <summary>Call only after processing this exact request and applying its UI work. Safe from a worker thread.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayConnection.ReportFailure(System.Exception)">
+            <summary>현재 세션의 동기 hook에서 발생한 오류. 큐에 남는 작업에는 요청 인자가 있는 overload를 사용한다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayConnection.ReportFailure(TUFReplay.Overlay.TUFReplayFrameRequest,System.Exception)">
+            <summary>Use for queued work; an old or completed request cannot fault a later render.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayConnection.EnterFrame(TUFReplay.Overlay.TUFReplayFrameRequest)">
+            <summary>Optional scope for deferred Update/worker processing. Exposes this request's delta during its work, even in a zero-delta refresh.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplaySessionContext">
+            <summary>렌더 한 번의 식별자와 출력 크기, 논리 시뮬레이션 속도를 전달한다.</summary>
+            <remarks>모드가 임시 UI를 만들 때 사용하는 데이터이며, 이미지 생성이나 Canvas 반환을 요구하지 않는다.</remarks>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplaySessionContext.FrameRate">
+            <summary>Logical simulation rate, independent of repeated video samples.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplayKeyEvent">
+            <summary>Unity KeyCode로 기록된 눌림/뗌 전이. 모든 시간은 초 단위다.</summary>
+            <remarks>
+            RecordedTime은 원본 노래 기준, ElapsedTime은 출력 영상 기준이다. 두 시간축을 구분해야
+            countdown·출력 시작 offset에서도 기존 애니메이션과 입력 순서를 유지할 수 있다.
+            Sequence는 같은 기록 시각의 순서를 결정한다. native 키 코드나 장치 ID는 포함하지 않는다.
+            </remarks>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplayKeyEvent.RecordedTime">
+            <summary>Original song-relative recording time; countdown events may be negative.</summary>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplayKeyEvent.ElapsedTime">
+            <summary>The event's time on the output timeline; may precede output frame zero.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplayFrameRequest">
+            <summary>한 연결에 배정된 UI 갱신 요청. 작업을 마칠 때 이 객체 자체를 반환해야 한다.</summary>
+            <remarks>
+            같은 프레임을 받는 연결들도 각각 다른 요청 객체를 가진다. Id만 복사해서 완료하면
+            다른 연결이나 과거 요청을 완료할 수 있으므로 객체·연결·세션 세대를 함께 검증한다.
+            </remarks>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplayFrameRequest.Id">
+            <summary>Unity frameCount와 독립된 논리 요청 ID. 완료 권한은 ID가 아닌 이 객체에 있다.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplayFailure">
+            <summary>호스트가 사용자에게 제외 또는 렌더 취소를 물을 때 사용할 연결별 오류 정보.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplayFrame">
+            <summary>한 논리 시뮬레이션 프레임의 시간, 최종 held 상태, 순서가 있는 입력 이벤트 snapshot.</summary>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplayFrame.PressedKeys">
+            <summary>모든 이벤트 처리 후 눌려 있는 키. 이벤트 전달 중의 중간 상태와 구분한다.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplayRenderSession">
+            <summary>Renderer가 소유하는 세션. 데이터를 공급하고 각 모드의 실제 UI 완료를 기다린다.</summary>
+            <remarks>Prepare/Request/Refresh/종료는 호스트의 Unity 스레드에서 실행한다. 모드의 완료·오류는 worker에서 올 수 있다.</remarks>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplayRenderSession.ReadyForCapture">
+            <summary>입력 전달과 모든 유효 연결의 요청 완료가 끝났는지. Canvas layout 반영 자체는 호스트의 책임이다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayRenderSession.PrepareFrameClock(System.Int64,System.Double,System.Single)">
+            <summary>게임 시뮬레이션 직전에 UI 시각만 준비한다. 새 입력은 RequestFrame에서 전달한다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayRenderSession.RequestFrame(TUFReplay.Overlay.TUFReplayFrame)">
+            <summary>기록 입력 → 최종 상태 → 프레임 요청 순으로 전달한다. 이전 요청을 마쳐야 다음 요청을 보낼 수 있다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayRenderSession.EnterRefresh">
+            <summary>입력과 UI 시간을 고정한 채 Unity Update/layout이 반영될 시간을 허용한다. delta와 edge는 0이다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplayRenderSession.CheckCompletionTimeout(System.TimeSpan)">
+            <summary>Host polls real elapsed time. User decision time is excluded; no automatic retry.</summary>
+        </member>
+        <member name="T:TUFReplay.Overlay.TUFReplaySession">
+            <summary>재생 데이터만 제공한다. 비활성일 때 실제 시간·입력 API를 선택하는 책임은 모드에 있다.</summary>
+            <remarks>native getter를 등록하거나 실제 입력과 재생 입력을 OR로 합치지 않는다.</remarks>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplaySession.IsActive">
+            <summary>시작 콜백 전부터 모든 종료 콜백이 끝날 때까지 true다.</summary>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplaySession.ElapsedTime">
+            <summary>영상 경과 초(double). EnterFrame 안에서는 해당 요청의 시간이며, 비활성 조회는 예외다.</summary>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplaySession.DeltaTime">
+            <summary>UI 갱신 간격 초(float). 추가 refresh에서는 0, EnterFrame 안에서는 요청의 원래 delta다.</summary>
+        </member>
+        <member name="P:TUFReplay.Overlay.TUFReplaySession.IsRefreshing">
+            <summary>호스트가 새 입력 없이 화면 반영·완료를 기다리는 추가 refresh 상태인지 나타낸다.</summary>
+        </member>
+        <member name="M:TUFReplay.Overlay.TUFReplaySession.BeginRender(TUFReplay.Overlay.TUFReplaySessionContext)">
+            <summary>Host API. Call on the Unity thread, before capturing participating UI.</summary>
+        </member>
+    </members>
+</doc>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version="1.0"?&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;doc&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;assembly&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;name&gt;TUFReplay.Overlay&lt;/name&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/assembly&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;members&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplayConnection"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 8 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;모드의 등록·수명·입력·요청 완료를 연결한다. 기존 입력 backend나 Update 구조를 강제하지 않는다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 9 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplayConnection.PendingFrame"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 11 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;아직 끝내지 않은 이 연결의 요청. 별도 frame callback 없이 기존 Update에서도 처리할 수 있다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 12 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 13 | 렌더 세션 시작 → BeginRender를 실행한다. | 공급원 전환을 SDK 수명에 연결한다. |
| 추가 | 현재 14 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Lifecycle and data callbacks are dispatched on the renderer's Unity thread.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 15 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 16 | 포함·제외 변경을 표시 활성 상태에 반영한다. | 문제 표시 요소를 제외하면 화면에서도 숨긴다. |
| 추가 | 현재 17 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;제외 시 false를 받는다. 렌더 표시를 숨기되 live 표시는 세션 종료까지 격리한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 18 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 19 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="E:TUFReplay.Overlay.TUFReplayConnection.OnKeyEvent"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 20 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;기록 순서로 입력을 전달한다. 이 콜백의 상태 조회에는 해당 전이까지 반영되어 있다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 21 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 22 | 요청이 오면 이 요청의 갱신·완료 경로를 실행한다. | 키뷰어는 FIFO 표식, 오버레이는 game hook 뒤 갱신으로 연결한다. |
| 추가 | 현재 23 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;이번 요청의 모든 입력 전달 후 발생한다. 최종 held 상태로 기존 갱신을 실행하고 완료한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 24 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 25 | 실제로 처리한 이 요청에만 UI 갱신 완료를 알린다. | 다른/이전 요청의 완료로 현재 캡처를 허용하지 않는다. |
| 추가 | 현재 26 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Call only after processing this exact request and applying its UI work. Safe from a worker thread.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 27 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 28 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |
| 추가 | 현재 29 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;현재 세션의 동기 hook에서 발생한 오류. 큐에 남는 작업에는 요청 인자가 있는 overload를 사용한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 30 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 31 | 갱신 실패를 SDK에 전달한다. | 로그만 남기고 무한 대기하거나 성공 처리하지 않게 한다. |
| 추가 | 현재 32 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Use for queued work; an old or completed request cannot fault a later render.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 33 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 34 | 이 갱신 범위에서 요청의 시간·delta를 읽는다. | 대기 Update의 0 delta와 요청 갱신의 delta를 구분한다. |
| 추가 | 현재 35 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Optional scope for deferred Update/worker processing. Exposes this request's delta during its work, even in a zero-delta refresh.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 36 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 37 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplaySessionContext"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 38 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;렌더 한 번의 식별자와 출력 크기, 논리 시뮬레이션 속도를 전달한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 39 | 프로젝트·리소스·설정 요소를 지정한다: &lt;remarks&gt;모드가 임시 UI를 만들 때 사용하는 데이터이며, 이미지 생성이나 Canvas 반환을 요구하지 않는다.&lt;/remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 40 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 41 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplaySessionContext.FrameRate"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 42 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Logical simulation rate, independent of repeated video samples.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 43 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 44 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplayKeyEvent"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 45 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Unity KeyCode로 기록된 눌림/뗌 전이. 모든 시간은 초 단위다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 46 | 프로젝트·리소스·설정 요소를 지정한다: &lt;remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 47 | 연결된 식·선언·설정의 일부다: RecordedTime은 원본 노래 기준, ElapsedTime은 출력 영상 기준이다. 두 시간축을 구분해야 | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 48 | 연결된 식·선언·설정의 일부다: countdown·출력 시작 offset에서도 기존 애니메이션과 입력 순서를 유지할 수 있다. | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 49 | 연결된 식·선언·설정의 일부다: Sequence는 같은 기록 시각의 순서를 결정한다. native 키 코드나 장치 ID는 포함하지 않는다. | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 50 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 51 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 52 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplayKeyEvent.RecordedTime"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 53 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Original song-relative recording time; countdown events may be negative.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 54 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 55 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplayKeyEvent.ElapsedTime"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 56 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;The event's time on the output timeline; may precede output frame zero.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 57 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 58 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplayFrameRequest"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 59 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;한 연결에 배정된 UI 갱신 요청. 작업을 마칠 때 이 객체 자체를 반환해야 한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 60 | 프로젝트·리소스·설정 요소를 지정한다: &lt;remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 61 | 연결된 식·선언·설정의 일부다: 같은 프레임을 받는 연결들도 각각 다른 요청 객체를 가진다. Id만 복사해서 완료하면 | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 62 | 연결된 식·선언·설정의 일부다: 다른 연결이나 과거 요청을 완료할 수 있으므로 객체·연결·세션 세대를 함께 검증한다. | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 63 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 64 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 65 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplayFrameRequest.Id"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 66 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Unity frameCount와 독립된 논리 요청 ID. 완료 권한은 ID가 아닌 이 객체에 있다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 67 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 68 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplayFailure"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 69 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;호스트가 사용자에게 제외 또는 렌더 취소를 물을 때 사용할 연결별 오류 정보.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 70 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 71 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplayFrame"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 72 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;한 논리 시뮬레이션 프레임의 시간, 최종 held 상태, 순서가 있는 입력 이벤트 snapshot.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 73 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 74 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplayFrame.PressedKeys"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 75 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;모든 이벤트 처리 후 눌려 있는 키. 이벤트 전달 중의 중간 상태와 구분한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 76 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 77 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplayRenderSession"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 78 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Renderer가 소유하는 세션. 데이터를 공급하고 각 모드의 실제 UI 완료를 기다린다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 79 | 프로젝트·리소스·설정 요소를 지정한다: &lt;remarks&gt;Prepare/Request/Refresh/종료는 호스트의 Unity 스레드에서 실행한다. 모드의 완료·오류는 worker에서 올 수 있다.&lt;/remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 80 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 81 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplayRenderSession.ReadyForCapture"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 82 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;입력 전달과 모든 유효 연결의 요청 완료가 끝났는지. Canvas layout 반영 자체는 호스트의 책임이다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 83 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 84 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="M:TUFReplay.Overlay.TUFReplayRenderSession.PrepareFrameClock(System.Int64,System.Double,System.Single)"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 85 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;게임 시뮬레이션 직전에 UI 시각만 준비한다. 새 입력은 RequestFrame에서 전달한다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 86 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 87 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="M:TUFReplay.Overlay.TUFReplayRenderSession.RequestFrame(TUFReplay.Overlay.TUFReplayFrame)"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 88 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;기록 입력 → 최종 상태 → 프레임 요청 순으로 전달한다. 이전 요청을 마쳐야 다음 요청을 보낼 수 있다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 89 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 90 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="M:TUFReplay.Overlay.TUFReplayRenderSession.EnterRefresh"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 91 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;입력과 UI 시간을 고정한 채 Unity Update/layout이 반영될 시간을 허용한다. delta와 edge는 0이다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 92 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 93 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="M:TUFReplay.Overlay.TUFReplayRenderSession.CheckCompletionTimeout(System.TimeSpan)"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 94 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Host polls real elapsed time. User decision time is excluded; no automatic retry.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 95 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 96 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="T:TUFReplay.Overlay.TUFReplaySession"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 97 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;재생 데이터만 제공한다. 비활성일 때 실제 시간·입력 API를 선택하는 책임은 모드에 있다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 98 | 프로젝트·리소스·설정 요소를 지정한다: &lt;remarks&gt;native getter를 등록하거나 실제 입력과 재생 입력을 OR로 합치지 않는다.&lt;/remarks&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 99 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 100 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplaySession.IsActive"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 101 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;시작 콜백 전부터 모든 종료 콜백이 끝날 때까지 true다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 102 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 103 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplaySession.ElapsedTime"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 104 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;영상 경과 초(double). EnterFrame 안에서는 해당 요청의 시간이며, 비활성 조회는 예외다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 105 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 106 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplaySession.DeltaTime"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 107 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;UI 갱신 간격 초(float). 추가 refresh에서는 0, EnterFrame 안에서는 요청의 원래 delta다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 108 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 109 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="P:TUFReplay.Overlay.TUFReplaySession.IsRefreshing"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 110 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;호스트가 새 입력 없이 화면 반영·완료를 기다리는 추가 refresh 상태인지 나타낸다.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 111 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 112 | 프로젝트·리소스·설정 요소를 지정한다: &lt;member name="M:TUFReplay.Overlay.TUFReplaySession.BeginRender(TUFReplay.Overlay.TUFReplaySessionContext)"&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 113 | 프로젝트·리소스·설정 요소를 지정한다: &lt;summary&gt;Host API. Call on the Unity thread, before capturing participating UI.&lt;/summary&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 114 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/member&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 115 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/members&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |
| 추가 | 현재 116 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/doc&gt; | 배포물의 API 설명·라이선스·설정을 제공한다. |

## scripts/run.sh

현재 파일: [scripts/run.sh](/Users/kgh/dev/src/jipper-resourcepack/scripts/run.sh)

빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다.

### @@ -0,0 +1,92 @@

```diff
@@ -0,0 +1,92 @@
+#!/usr/bin/env bash
+set -euo pipefail
+
+repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
+command_name="${1:-build}"
+game_root="${ADOFAI_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
+managed_path="${ADOFAI_MANAGED_PATH:-$game_root/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed}"
+mods_path="${ADOFAI_MODS_PATH:-$game_root/Mods}"
+sdk_path="${OVERLAY_SDK_PATH:-$repo_root/libs/TUFReplay.Overlay.dll}"
+keyboard_chatter_path="${KEYBOARD_CHATTER_BLOCKER_DLL:-$mods_path/KeyboardChatterBlocker/KeyboardChatterBlocker.dll}"
+configuration="${CONFIGURATION:-Release}"
+dotnet_command="${DOTNET:-dotnet}"
+
+if [[ "$command_name" == check ]]; then
+    git -C "$repo_root" diff --check
+    bash -n "$repo_root/scripts/run.sh"
+    "$dotnet_command" run --project "$repo_root/tests/RainLayout/RainLayout.csproj" -c "$configuration"
+    "$dotnet_command" run --project "$repo_root/tests/ViewerReplay/ViewerReplay.csproj" -c "$configuration" -p:OverlaySdkPath="$sdk_path"
+    exit 0
+fi
+if [[ "$command_name" != build && "$command_name" != package && "$command_name" != install ]]; then
+    printf 'Usage: bash scripts/run.sh [build|package|install|check]\n' >&2
+    exit 2
+fi
+if [[ ! -f "$managed_path/Assembly-CSharp.dll" || ! -f "$mods_path/JALib/JALib.dll" ]]; then
+    printf 'Set ADOFAI_MANAGED_PATH and ADOFAI_MODS_PATH to an ADOFAI installation with JALib.\n' >&2
+    exit 1
+fi
+if [[ ! -f "$sdk_path" ]]; then
+    printf 'Place the SDK distribution DLL at libs/TUFReplay.Overlay.dll, or set OVERLAY_SDK_PATH to that DLL.\n' >&2
+    exit 1
+fi
+if [[ ! -f "$keyboard_chatter_path" ]]; then
+    printf 'Set KEYBOARD_CHATTER_BLOCKER_DLL to the optional KeyboardChatterBlocker DLL used as a compile-time reference.\n' >&2
+    exit 1
+fi
+
+"$dotnet_command" build "$repo_root/JipperResourcePack/JipperResourcePack.csproj" -c "$configuration" \
+    -p:GameManagedPath="$managed_path" -p:GameModsPath="$mods_path" -p:OverlaySdkPath="$sdk_path" \
+    -p:KeyboardChatterBlockerPath="$keyboard_chatter_path" \
+    -p:ModPath= -p:ReleasePath= -v:minimal
+
+if [[ "$command_name" == package || "$command_name" == install ]]; then
+    package_root="${PACKAGE_PATH:-$repo_root/artifacts/render-support/JipperResourcePack}"
+    mkdir -p "$package_root/VersionSafe"
+    cp "$repo_root/JipperResourcePack/bin/$configuration/net481/JipperResourcePack.dll" "$package_root/"
+    cp "$sdk_path" "$package_root/"
+    for release in R136 R141 R146 R149; do
+        cp "$repo_root/JipperResourcePack.VersionSafe/bin/$configuration/$release/JipperResourcePack.VersionSafe.dll" "$package_root/VersionSafe/JipperResourcePack.VersionSafe.$release.dll"
+    done
+    printf 'Render support files: %s\nCopy these files over an existing JipperResourcePack package; keep its bootstrap, bundle, fonts, and localization.\n' "$package_root"
+fi
+
+if [[ "$command_name" == install ]]; then
+    installed_root="$mods_path/JipperResourcePack"
+    backup_root="${VALIDATION_BACKUP_PATH:-/private/tmp/tuf-overlay-validation-originals/jipper}"
+    if [[ ! -f "$installed_root/Info.json" ]]; then
+        printf 'install requires an existing JipperResourcePack installation at %s.\n' "$installed_root" >&2
+        exit 1
+    fi
+    payload=(JipperResourcePack.dll TUFReplay.Overlay.dll)
+    for release in R136 R141 R146 R149; do
+        payload+=("VersionSafe/JipperResourcePack.VersionSafe.$release.dll")
+    done
+    mkdir -p "$backup_root"
+    if [[ ! -f "$backup_root/original-sha256.tsv" ]]; then
+        printf 'sha256\tpath\n' > "$backup_root/original-sha256.tsv"
+    fi
+    for relative_path in "${payload[@]}"; do
+        if [[ -e "$backup_root/$relative_path" || -e "$backup_root/missing/$relative_path" ]]; then
+            continue
+        fi
+        if [[ -f "$installed_root/$relative_path" ]]; then
+            mkdir -p "$(dirname "$backup_root/$relative_path")"
+            cp -p "$installed_root/$relative_path" "$backup_root/$relative_path"
+            digest="$(shasum -a 256 "$backup_root/$relative_path")"
+            printf '%s\t%s\n' "${digest%% *}" "$relative_path" >> "$backup_root/original-sha256.tsv"
+        else
+            mkdir -p "$(dirname "$backup_root/missing/$relative_path")"
+            touch "$backup_root/missing/$relative_path"
+            printf 'absent\t%s\n' "$relative_path" >> "$backup_root/original-sha256.tsv"
+        fi
+    done
+    printf 'sha256\tpath\n' > "$backup_root/installed-sha256.tsv"
+    for relative_path in "${payload[@]}"; do
+        mkdir -p "$(dirname "$installed_root/$relative_path")"
+        cp -p "$package_root/$relative_path" "$installed_root/$relative_path"
+        digest="$(shasum -a 256 "$installed_root/$relative_path")"
+        printf '%s\t%s\n' "${digest%% *}" "$relative_path" >> "$backup_root/installed-sha256.tsv"
+    done
+    printf 'Installed program DLLs at %s\nOriginal program files and hashes: %s\n' "$installed_root" "$backup_root"
+fi
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 설정 주석/전처리 지시/스크립트 경계다: #!/usr/bin/env bash | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 2 | 연결된 식·선언·설정의 일부다: set -euo pipefail | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 3 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 4 | 연결된 식·선언·설정의 일부다: repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." &amp;&amp; pwd)" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 5 | 연결된 식·선언·설정의 일부다: command_name="${1:-build}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 6 | 연결된 식·선언·설정의 일부다: game_root="${ADOFAI_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 7 | 연결된 식·선언·설정의 일부다: managed_path="${ADOFAI_MANAGED_PATH:-$game_root/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 8 | 연결된 식·선언·설정의 일부다: mods_path="${ADOFAI_MODS_PATH:-$game_root/Mods}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 9 | 연결된 식·선언·설정의 일부다: sdk_path="${OVERLAY_SDK_PATH:-$repo_root/libs/TUFReplay.Overlay.dll}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 10 | 연결된 식·선언·설정의 일부다: keyboard_chatter_path="${KEYBOARD_CHATTER_BLOCKER_DLL:-$mods_path/KeyboardChatterBlocker/KeyboardChatterBlocker.dll}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 11 | 연결된 식·선언·설정의 일부다: configuration="${CONFIGURATION:-Release}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 12 | 연결된 식·선언·설정의 일부다: dotnet_command="${DOTNET:-dotnet}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 13 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 14 | 연결된 식·선언·설정의 일부다: if [[ "$command_name" == check ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 15 | 연결된 식·선언·설정의 일부다: git -C "$repo_root" diff --check | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 16 | 연결된 식·선언·설정의 일부다: bash -n "$repo_root/scripts/run.sh" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 17 | 연결된 식·선언·설정의 일부다: "$dotnet_command" run --project "$repo_root/tests/RainLayout/RainLayout.csproj" -c "$configuration" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 18 | 연결된 식·선언·설정의 일부다: "$dotnet_command" run --project "$repo_root/tests/ViewerReplay/ViewerReplay.csproj" -c "$configuration" -p:OverlaySdkPath="$sdk_path" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 19 | 연결된 식·선언·설정의 일부다: exit 0 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 20 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 21 | 연결된 식·선언·설정의 일부다: if [[ "$command_name" != build &amp;&amp; "$command_name" != package &amp;&amp; "$command_name" != install ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 22 | 연결된 식·선언·설정의 일부다: printf 'Usage: bash scripts/run.sh [build\|package\|install\|check]\n' &gt;&amp;2 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 23 | 연결된 식·선언·설정의 일부다: exit 2 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 24 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 25 | 연결된 식·선언·설정의 일부다: if [[ ! -f "$managed_path/Assembly-CSharp.dll" \|\| ! -f "$mods_path/JALib/JALib.dll" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 26 | 연결된 식·선언·설정의 일부다: printf 'Set ADOFAI_MANAGED_PATH and ADOFAI_MODS_PATH to an ADOFAI installation with JALib.\n' &gt;&amp;2 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 27 | 연결된 식·선언·설정의 일부다: exit 1 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 28 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 29 | 연결된 식·선언·설정의 일부다: if [[ ! -f "$sdk_path" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 30 | 연결된 식·선언·설정의 일부다: printf 'Place the SDK distribution DLL at libs/TUFReplay.Overlay.dll, or set OVERLAY_SDK_PATH to that DLL.\n' &gt;&amp;2 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 31 | 연결된 식·선언·설정의 일부다: exit 1 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 32 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 33 | 연결된 식·선언·설정의 일부다: if [[ ! -f "$keyboard_chatter_path" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 34 | 연결된 식·선언·설정의 일부다: printf 'Set KEYBOARD_CHATTER_BLOCKER_DLL to the optional KeyboardChatterBlocker DLL used as a compile-time reference.\n' &gt;&amp;2 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 35 | 연결된 식·선언·설정의 일부다: exit 1 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 36 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 37 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 38 | 연결된 식·선언·설정의 일부다: "$dotnet_command" build "$repo_root/JipperResourcePack/JipperResourcePack.csproj" -c "$configuration" \ | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 39 | 연결된 식·선언·설정의 일부다: -p:GameManagedPath="$managed_path" -p:GameModsPath="$mods_path" -p:OverlaySdkPath="$sdk_path" \ | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 40 | 연결된 식·선언·설정의 일부다: -p:KeyboardChatterBlockerPath="$keyboard_chatter_path" \ | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 41 | 연결된 식·선언·설정의 일부다: -p:ModPath= -p:ReleasePath= -v:minimal | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 42 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 43 | 연결된 식·선언·설정의 일부다: if [[ "$command_name" == package \|\| "$command_name" == install ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 44 | 연결된 식·선언·설정의 일부다: package_root="${PACKAGE_PATH:-$repo_root/artifacts/render-support/JipperResourcePack}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 45 | 연결된 식·선언·설정의 일부다: mkdir -p "$package_root/VersionSafe" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 46 | 연결된 식·선언·설정의 일부다: cp "$repo_root/JipperResourcePack/bin/$configuration/net481/JipperResourcePack.dll" "$package_root/" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 47 | 연결된 식·선언·설정의 일부다: cp "$sdk_path" "$package_root/" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 48 | 연결된 식·선언·설정의 일부다: for release in R136 R141 R146 R149; do | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 49 | 연결된 식·선언·설정의 일부다: cp "$repo_root/JipperResourcePack.VersionSafe/bin/$configuration/$release/JipperResourcePack.VersionSafe.dll" "$package_root/VersionSafe/JipperResourcePack.VersionSafe.$release.dll" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 50 | 연결된 식·선언·설정의 일부다: done | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 51 | 연결된 식·선언·설정의 일부다: printf 'Render support files: %s\nCopy these files over an existing JipperResourcePack package; keep its bootstrap, bundle, fonts, and localization.\n' "$package_root" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 52 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 53 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 54 | 연결된 식·선언·설정의 일부다: if [[ "$command_name" == install ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 55 | 연결된 식·선언·설정의 일부다: installed_root="$mods_path/JipperResourcePack" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 56 | 연결된 식·선언·설정의 일부다: backup_root="${VALIDATION_BACKUP_PATH:-/private/tmp/tuf-overlay-validation-originals/jipper}" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 57 | 연결된 식·선언·설정의 일부다: if [[ ! -f "$installed_root/Info.json" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 58 | 연결된 식·선언·설정의 일부다: printf 'install requires an existing JipperResourcePack installation at %s.\n' "$installed_root" &gt;&amp;2 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 59 | 연결된 식·선언·설정의 일부다: exit 1 | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 60 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 61 | 연결된 식·선언·설정의 일부다: payload=(JipperResourcePack.dll TUFReplay.Overlay.dll) | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 62 | 연결된 식·선언·설정의 일부다: for release in R136 R141 R146 R149; do | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 63 | 연결된 식·선언·설정의 일부다: payload+=("VersionSafe/JipperResourcePack.VersionSafe.$release.dll") | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 64 | 연결된 식·선언·설정의 일부다: done | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 65 | 연결된 식·선언·설정의 일부다: mkdir -p "$backup_root" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 66 | 연결된 식·선언·설정의 일부다: if [[ ! -f "$backup_root/original-sha256.tsv" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 67 | 연결된 식·선언·설정의 일부다: printf 'sha256\tpath\n' &gt; "$backup_root/original-sha256.tsv" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 68 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 69 | 연결된 식·선언·설정의 일부다: for relative_path in "${payload[@]}"; do | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 70 | 연결된 식·선언·설정의 일부다: if [[ -e "$backup_root/$relative_path" \|\| -e "$backup_root/missing/$relative_path" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 71 | 연결된 식·선언·설정의 일부다: continue | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 72 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 73 | 연결된 식·선언·설정의 일부다: if [[ -f "$installed_root/$relative_path" ]]; then | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 74 | 연결된 식·선언·설정의 일부다: mkdir -p "$(dirname "$backup_root/$relative_path")" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 75 | 연결된 식·선언·설정의 일부다: cp -p "$installed_root/$relative_path" "$backup_root/$relative_path" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 76 | 연결된 식·선언·설정의 일부다: digest="$(shasum -a 256 "$backup_root/$relative_path")" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 77 | 연결된 식·선언·설정의 일부다: printf '%s\t%s\n' "${digest%% *}" "$relative_path" &gt;&gt; "$backup_root/original-sha256.tsv" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 78 | 앞 조건이 거짓일 때의 처리다. | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 79 | 연결된 식·선언·설정의 일부다: mkdir -p "$(dirname "$backup_root/missing/$relative_path")" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 80 | 연결된 식·선언·설정의 일부다: touch "$backup_root/missing/$relative_path" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 81 | 연결된 식·선언·설정의 일부다: printf 'absent\t%s\n' "$relative_path" &gt;&gt; "$backup_root/original-sha256.tsv" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 82 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 83 | 연결된 식·선언·설정의 일부다: done | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 84 | 연결된 식·선언·설정의 일부다: printf 'sha256\tpath\n' &gt; "$backup_root/installed-sha256.tsv" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 85 | 연결된 식·선언·설정의 일부다: for relative_path in "${payload[@]}"; do | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 86 | 연결된 식·선언·설정의 일부다: mkdir -p "$(dirname "$installed_root/$relative_path")" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 87 | 연결된 식·선언·설정의 일부다: cp -p "$package_root/$relative_path" "$installed_root/$relative_path" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 88 | 연결된 식·선언·설정의 일부다: digest="$(shasum -a 256 "$installed_root/$relative_path")" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 89 | 연결된 식·선언·설정의 일부다: printf '%s\t%s\n' "${digest%% *}" "$relative_path" &gt;&gt; "$backup_root/installed-sha256.tsv" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 90 | 연결된 식·선언·설정의 일부다: done | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 91 | 연결된 식·선언·설정의 일부다: printf 'Installed program DLLs at %s\nOriginal program files and hashes: %s\n' "$installed_root" "$backup_root" | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |
| 추가 | 현재 92 | 연결된 식·선언·설정의 일부다: fi | 빌드·검사·패키지·설치를 한 진입점에서 수행하고 설치 부작용을 구분한다. |

## tests/RainLayout/Program.cs

현재 파일: [tests/RainLayout/Program.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/RainLayout/Program.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,53 @@

```diff
@@ -0,0 +1,53 @@
+using JipperResourcePack.KeyViewerContents;
+using UnityEngine;
+using UnityEngine.UI;
+
+// A camera-space Canvas moves/scales its descendants away from the world origin.
+// Link the production constructor and pool so this checks dynamic creation,
+// rather than an independent copy of the parenting code.
+RectTransform parent = new GameObject("Camera-space rain line").AddComponent<RectTransform>();
+parent.localPosition = new Vector3(12, -4, -9);
+parent.localScale = new Vector3(0.01f, 0.01f, 0.01f);
+parent.localRotation = Quaternion.EulerZ(25);
+RainPool pool = new(parent);
+
+foreach(bool ghost in new[] { false, true }) {
+    Rain rain = pool.GetOrNewRain(ghost);
+    CheckLayout(rain, ghost, "new");
+    Check(ghost ? rain.Image is Image : rain.Image is RawImage, "Graphic type");
+    if(ghost) {
+        Image image = (Image) rain.Image;
+        Check(ReferenceEquals(image.sprite, JipperResourcePack.BundleLoader.GhostRain), "Ghost sprite");
+        Check(image.type == Image.Type.Tiled, "Ghost tiling");
+    }
+
+    rain.Transform.sizeDelta = new Vector2(50, 100);
+    pool.AddPool(rain, ghost);
+    Check(!rain.GameObject.activeSelf, "Pooled rain hidden");
+    Rain reused = pool.GetOrNewRain(ghost);
+    Check(ReferenceEquals(rain, reused), "Reuse existing rain");
+    CheckLayout(reused, ghost, "reused");
+}
+
+Console.WriteLine("Rain layout regression passed: normal, ghost, and pooled graphics stay on the translated/scaled/rotated parent plane.");
+
+void CheckLayout(Rain rain, bool ghost, string stage) {
+    string kind = (ghost ? "ghost" : "normal") + " " + stage;
+    Check(ReferenceEquals(rain.Transform.parent, parent), kind + " parent");
+    Check(Near(rain.Transform.localPosition.z, 0), kind + " local depth must remain on its Canvas plane");
+    Check(Near(rain.Transform.position.z, parent.position.z), kind + " world depth follows the parent");
+    Check(rain.Transform.localRotation.IsIdentity, kind + " local rotation follows the parent");
+    Check(rain.Transform.localScale == Vector3.one, kind + " local scale");
+    Check(rain.Transform.anchoredPosition == Vector2.zero, kind + " anchor position");
+    Check(rain.Transform.sizeDelta == Vector2.zero, kind + " fresh size");
+    Check(rain.Transform.anchorMin == new Vector2(0.5f, 1) && rain.Transform.anchorMax == rain.Transform.anchorMin,
+        kind + " anchors");
+    Check(rain.Transform.pivot == new Vector2(0.5f, 1), kind + " pivot");
+    Check(rain.GameObject.activeSelf, kind + " visible");
+    Check(rain.IsGhost == ghost, kind + " ghost flag");
+}
+
+static bool Near(float value, float expected) => Math.Abs(value - expected) < 0.0001f;
+static void Check(bool success, string message) {
+    if(!success) throw new InvalidOperationException("Rain layout regression: " + message);
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.KeyViewerContents; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: UnityEngine; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | namespace 또는 타입 별칭을 가져온다: UnityEngine.UI; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 4 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 5 | 주석: A camera-space Canvas moves/scales its descendants away from the world origin. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 6 | 주석: Link the production constructor and pool so this checks dynamic creation, | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 7 | 주석: rather than an independent copy of the parenting code. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 8 | 오른쪽 결과를 왼쪽 상태에 저장한다: RectTransform parent = new GameObject("Camera-space rain line").AddComponent&lt;RectTransform&gt;(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 오른쪽 결과를 왼쪽 상태에 저장한다: parent.localPosition = new Vector3(12, -4, -9); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 오른쪽 결과를 왼쪽 상태에 저장한다: parent.localScale = new Vector3(0.01f, 0.01f, 0.01f); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 오른쪽 결과를 왼쪽 상태에 저장한다: parent.localRotation = Quaternion.EulerZ(25); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 오른쪽 결과를 왼쪽 상태에 저장한다: RainPool pool = new(parent); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 14 | 각 컬렉션 항목을 순회한다: foreach(bool ghost in new[] { false, true }) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 오른쪽 결과를 왼쪽 상태에 저장한다: Rain rain = pool.GetOrNewRain(ghost); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 이 함수 호출/식 처리를 실행한다: CheckLayout(rain, ghost, "new"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 조건을 만족할 때 뒤의 작업을 수행한다: if(ghost) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 오른쪽 결과를 왼쪽 상태에 저장한다: Image image = (Image) rain.Image; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 처리 요청과 SDK 대기 요청이 같은 객체인지 확인한다. | 이전·취소된 요청을 현재 요청 대신 완료하지 않는다. |
| 추가 | 현재 21 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 23 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 24 | 오른쪽 결과를 왼쪽 상태에 저장한다: rain.Transform.sizeDelta = new Vector2(50, 100); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 25 | 이 함수 호출/식 처리를 실행한다: pool.AddPool(rain, ghost); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 오른쪽 결과를 왼쪽 상태에 저장한다: Rain reused = pool.GetOrNewRain(ghost); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 처리 요청과 SDK 대기 요청이 같은 객체인지 확인한다. | 이전·취소된 요청을 현재 요청 대신 완료하지 않는다. |
| 추가 | 현재 29 | 이 함수 호출/식 처리를 실행한다: CheckLayout(reused, ghost, "reused"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 31 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 32 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 33 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 34 | 연결된 식·선언·설정의 일부다: void CheckLayout(Rain rain, bool ghost, string stage) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 오른쪽 결과를 왼쪽 상태에 저장한다: string kind = (ghost ? "ghost" : "normal") + " " + stage; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 처리 요청과 SDK 대기 요청이 같은 객체인지 확인한다. | 이전·취소된 요청을 현재 요청 대신 완료하지 않는다. |
| 추가 | 현재 37 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 38 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 39 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 40 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 41 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 42 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 43 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 44 | 이 함수 호출/식 처리를 실행한다: kind + " anchors"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 45 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 46 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 47 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 48 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 49 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 50 | 이 함수 호출/식 처리를 실행한다: static bool Near(float value, float expected) =&gt; Math.Abs(value - expected) &lt; 0.0001f; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 51 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 52 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 53 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/RainLayout/RainLayout.csproj

현재 파일: [tests/RainLayout/RainLayout.csproj](/Users/kgh/dev/src/jipper-resourcepack/tests/RainLayout/RainLayout.csproj)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,12 @@

```diff
@@ -0,0 +1,12 @@
+<Project Sdk="Microsoft.NET.Sdk">
+  <PropertyGroup>
+    <OutputType>Exe</OutputType>
+    <TargetFramework>net10.0</TargetFramework>
+    <ImplicitUsings>enable</ImplicitUsings>
+    <Nullable>disable</Nullable>
+  </PropertyGroup>
+  <ItemGroup>
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/Rain.cs" Link="Rain.cs" />
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/RainPool.cs" Link="RainPool.cs" />
+  </ItemGroup>
+</Project>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Project Sdk="Microsoft.NET.Sdk"&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OutputType&gt;Exe&lt;/OutputType&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;TargetFramework&gt;net10.0&lt;/TargetFramework&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Nullable&gt;disable&lt;/Nullable&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 8 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/Rain.cs" Link="Rain.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/RainPool.cs" Link="RainPool.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Project&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |

## tests/RainLayout/UnityStubs.cs

현재 파일: [tests/RainLayout/UnityStubs.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/RainLayout/UnityStubs.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,94 @@

```diff
@@ -0,0 +1,94 @@
+using NumericsMatrix = System.Numerics.Matrix4x4;
+using NumericsQuaternion = System.Numerics.Quaternion;
+using NumericsVector = System.Numerics.Vector3;
+
+// Only the Unity surface used by Rain/RainPool is modeled. Affine parenting
+// follows Transform.SetParent's worldPositionStays contract; no rendering is
+// simulated. RectTransform's 2D setter deliberately preserves local depth.
+namespace UnityEngine {
+    public readonly record struct Vector2(float x, float y) {
+        public static Vector2 zero => new(0, 0);
+    }
+
+    public readonly record struct Vector3(float x, float y, float z) {
+        public static Vector3 zero => new(0, 0, 0);
+        public static Vector3 one => new(1, 1, 1);
+        internal NumericsVector Native => new(x, y, z);
+        internal static Vector3 From(NumericsVector value) => new(value.X, value.Y, value.Z);
+    }
+
+    public readonly record struct Quaternion(float x, float y, float z, float w) {
+        public static Quaternion identity => new(0, 0, 0, 1);
+        public bool IsIdentity => Math.Abs(x) + Math.Abs(y) + Math.Abs(z) + Math.Abs(w - 1) < 0.0001f;
+        internal NumericsQuaternion Native => new(x, y, z, w);
+        internal static Quaternion From(NumericsQuaternion value) => new(value.X, value.Y, value.Z, value.W);
+        public static Quaternion EulerZ(float degrees) => From(NumericsQuaternion.CreateFromAxisAngle(
+            NumericsVector.UnitZ, degrees * MathF.PI / 180));
+    }
+
+    public class Component {
+        public GameObject gameObject;
+    }
+
+    public class Transform : Component {
+        public Transform parent { get; private set; }
+        public Vector3 localPosition = Vector3.zero;
+        public Vector3 localScale = Vector3.one;
+        public Quaternion localRotation = Quaternion.identity;
+        private NumericsMatrix WorldMatrix => NumericsMatrix.CreateScale(localScale.Native)
+            * NumericsMatrix.CreateFromQuaternion(localRotation.Native)
+            * NumericsMatrix.CreateTranslation(localPosition.Native)
+            * (parent?.WorldMatrix ?? NumericsMatrix.Identity);
+        public Vector3 position => Vector3.From(NumericsVector.Transform(NumericsVector.Zero, WorldMatrix));
+
+        public void SetParent(Transform target, bool worldPositionStays = true) {
+            NumericsMatrix world = WorldMatrix;
+            parent = target;
+            if(!worldPositionStays) return;
+            if(!NumericsMatrix.Invert(target?.WorldMatrix ?? NumericsMatrix.Identity, out NumericsMatrix inverse)
+                || !NumericsMatrix.Decompose(world * inverse, out NumericsVector scale,
+                    out NumericsQuaternion rotation, out NumericsVector position))
+                throw new InvalidOperationException("Invalid test transform.");
+            localPosition = Vector3.From(position);
+            localScale = Vector3.From(scale);
+            localRotation = Quaternion.From(rotation);
+        }
+    }
+
+    public class RectTransform : Transform {
+        public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
+        public Vector2 anchoredPosition {
+            get => new(localPosition.x, localPosition.y);
+            set => localPosition = new Vector3(value.x, value.y, localPosition.z);
+        }
+    }
+
+    public class GameObject {
+        public bool activeSelf { get; private set; } = true;
+        public GameObject(string name) { }
+        public T AddComponent<T>() where T : Component, new() => new T { gameObject = this };
+        public void SetActive(bool active) => activeSelf = active;
+    }
+
+    public class Sprite { }
+}
+
+namespace UnityEngine.UI {
+    public class Graphic : UnityEngine.Component { }
+    public class RawImage : Graphic { }
+    public class Image : Graphic {
+        public enum Type { Simple, Tiled }
+        public UnityEngine.Sprite sprite;
+        public Type type;
+    }
+}
+
+namespace JipperResourcePack {
+    public static class BundleLoader {
+        public static readonly UnityEngine.Sprite GhostRain = new();
+    }
+}
+
+namespace JipperResourcePack.KeyViewerContents {
+    public class RawRain { }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: NumericsMatrix = System.Numerics.Matrix4x4; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: NumericsQuaternion = System.Numerics.Quaternion; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | namespace 또는 타입 별칭을 가져온다: NumericsVector = System.Numerics.Vector3; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 4 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 5 | 주석: Only the Unity surface used by Rain/RainPool is modeled. Affine parenting | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 6 | 주석: follows Transform.SetParent's worldPositionStays contract; no rendering is | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 7 | 주석: simulated. RectTransform's 2D setter deliberately preserves local depth. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 8 | 이 타입의 소속 namespace를 지정한다: namespace UnityEngine { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct Vector2(float x, float y) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Vector2 zero =&gt; new(0, 0); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 12 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 13 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct Vector3(float x, float y, float z) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Vector3 zero =&gt; new(0, 0, 0); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Vector3 one =&gt; new(1, 1, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal NumericsVector Native =&gt; new(x, y, z); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal static Vector3 From(NumericsVector value) =&gt; new(value.X, value.Y, value.Z); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 19 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 20 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct Quaternion(float x, float y, float z, float w) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Quaternion identity =&gt; new(0, 0, 0, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsIdentity =&gt; Math.Abs(x) + Math.Abs(y) + Math.Abs(z) + Math.Abs(w - 1) &lt; 0.0001f; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal NumericsQuaternion Native =&gt; new(x, y, z, w); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal static Quaternion From(NumericsQuaternion value) =&gt; new(value.X, value.Y, value.Z, value.W); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 25 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static Quaternion EulerZ(float degrees) =&gt; From(NumericsQuaternion.CreateFromAxisAngle( | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 이 함수 호출/식 처리를 실행한다: NumericsVector.UnitZ, degrees * MathF.PI / 180)); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 28 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 29 | 타입을 선언한다: public class Component { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public GameObject gameObject; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 31 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 32 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 33 | 타입을 선언한다: public class Transform : Component { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 34 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Transform parent { get; private set; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Vector3 localPosition = Vector3.zero; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Vector3 localScale = Vector3.one; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 37 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Quaternion localRotation = Quaternion.identity; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 38 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private NumericsMatrix WorldMatrix =&gt; NumericsMatrix.CreateScale(localScale.Native) | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 39 | 연결된 식·선언·설정의 일부다: * NumericsMatrix.CreateFromQuaternion(localRotation.Native) | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 40 | 연결된 식·선언·설정의 일부다: * NumericsMatrix.CreateTranslation(localPosition.Native) | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 41 | 이 함수 호출/식 처리를 실행한다: * (parent?.WorldMatrix ?? NumericsMatrix.Identity); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 42 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Vector3 position =&gt; Vector3.From(NumericsVector.Transform(NumericsVector.Zero, WorldMatrix)); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 43 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 44 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void SetParent(Transform target, bool worldPositionStays = true) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: NumericsMatrix world = WorldMatrix; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 46 | 오른쪽 결과를 왼쪽 상태에 저장한다: parent = target; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 47 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!worldPositionStays) return; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 48 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!NumericsMatrix.Invert(target?.WorldMatrix ?? NumericsMatrix.Identity, out NumericsMatrix inverse) | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 49 | 연결된 식·선언·설정의 일부다: \|\| !NumericsMatrix.Decompose(world * inverse, out NumericsVector scale, | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 50 | 연결된 식·선언·설정의 일부다: out NumericsQuaternion rotation, out NumericsVector position)) | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 51 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: localPosition = Vector3.From(position); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 53 | 오른쪽 결과를 왼쪽 상태에 저장한다: localScale = Vector3.From(scale); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: localRotation = Quaternion.From(rotation); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 55 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 56 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 57 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 58 | 타입을 선언한다: public class RectTransform : Transform { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 59 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Vector2 anchorMin, anchorMax, pivot, sizeDelta; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 60 | 연결된 식·선언·설정의 일부다: public Vector2 anchoredPosition { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 61 | 이 함수 호출/식 처리를 실행한다: get =&gt; new(localPosition.x, localPosition.y); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: set =&gt; localPosition = new Vector3(value.x, value.y, localPosition.z); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 63 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 64 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 65 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 66 | 타입을 선언한다: public class GameObject { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 67 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool activeSelf { get; private set; } = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 68 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public GameObject(string name) { } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 69 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public T AddComponent&lt;T&gt;() where T : Component, new() =&gt; new T { gameObject = this }; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 70 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void SetActive(bool active) =&gt; activeSelf = active; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 71 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 72 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 73 | 타입을 선언한다: public class Sprite { } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 74 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 75 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 76 | 이 타입의 소속 namespace를 지정한다: namespace UnityEngine.UI { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 77 | 타입을 선언한다: public class Graphic : UnityEngine.Component { } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 78 | 타입을 선언한다: public class RawImage : Graphic { } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 79 | 타입을 선언한다: public class Image : Graphic { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 80 | 연결된 식·선언·설정의 일부다: public enum Type { Simple, Tiled } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 81 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public UnityEngine.Sprite sprite; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 82 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Type type; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 83 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 84 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 85 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 86 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 87 | 타입을 선언한다: public static class BundleLoader { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 88 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static readonly UnityEngine.Sprite GhostRain = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 89 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 90 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 91 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 92 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.KeyViewerContents { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 93 | 타입을 선언한다: public class RawRain { } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 94 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/ViewerReplay/OverlayReplayTests.cs

현재 파일: [tests/ViewerReplay/OverlayReplayTests.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/OverlayReplayTests.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,79 @@

```diff
@@ -0,0 +1,79 @@
+using JipperResourcePack;
+using JipperResourcePack.OverlayContents;
+using TUFReplay.Overlay;
+using UnityEngine;
+
+internal static class OverlayReplayTests {
+    private static void Require(bool value, string message) { if(!value) throw new Exception(message); }
+    private static TUFReplayRenderSession Begin(int id) => TUFReplaySession.BeginRender(new(id, 1920, 1080, 120));
+    private static void Frame(TUFReplayRenderSession session, long index) => session.RequestFrame(new(index, index * 0.1, 0.1f, Array.Empty<int>(), Array.Empty<TUFReplayKeyEvent>()));
+
+    internal static void Run() {
+        using var overlay = new Overlay();
+        var root = overlay.GameObject;
+        var text = overlay.Label;
+        var nativeManager = overlay.OverlayTextManager;
+        using(var session = Begin(10)) {
+            Require(Overlay.Instance == overlay && overlay.GameObject == root && overlay.Label == text && overlay.OverlayTextManager == nativeManager && Combo.ComboCount == 17,
+                "retain Overlay, text manager, Canvas and current combo without initialization");
+            Frame(session, 0);
+            session.EnterRefresh();
+            Require(!session.ReadyForCapture && text.text == "native", "queued UI is not yet complete");
+            VersionSafe.FlushAfter();
+            Require(!session.ReadyForCapture && text.text == "render-1", "text update alone cannot complete deferred layout");
+            overlay.CompleteLayout();
+            Require(session.ReadyForCapture && text.text == "render-1" && overlay.ObservedDelta == 0.1f, "UI applied with request delta before completion");
+            Frame(session, 1);
+            VersionSafe.FlushAfter();
+            overlay.CompleteLayout();
+            Require(session.ReadyForCapture && overlay.Updates == 2, "same Unity frame can process separate requests");
+            session.Exclude("JipperResourcePack.Overlay");
+            Frame(session, 2);
+            Require(!root.activeSelf && overlay.Updates == 2 && session.ReadyForCapture, "excluded same UI stays hidden");
+        }
+        Require(root.activeSelf && text.text == "render-2" && text.fontSize == 100 && overlay.Rect.anchoredPosition == new Vector2(100, 200) && overlay.AnimationRunning, "end retains the latest presentation and animation");
+        Require(overlay.LastHash.Data[0] == 99 && overlay.StartTile == 77 && Combo.ComboCount == 19 && overlay.OverlayTextManager == nativeManager, "end does not roll back play context or combo");
+        Console.WriteLine("PASS JRP same Overlay/UI, continued play state, delayed completion and exclusion");
+
+        using(var oldSession = Begin(11)) { Frame(oldSession, 0); }
+        using(var newSession = Begin(12)) {
+            Frame(newSession, 0);
+            VersionSafe.FlushAfter();
+            overlay.CompleteLayout();
+            Require(newSession.ReadyForCapture && overlay.Updates == 3, "cancelled callback cannot mutate next session");
+        }
+        Console.WriteLine("PASS JRP cancelled delayed callbacks and back-to-back sessions");
+
+        overlay.FailUpdate = true;
+        using(var session = Begin(13)) {
+            Frame(session, 0); VersionSafe.FlushAfter(); overlay.CompleteLayout();
+            Require(!session.ReadyForCapture && session.TryDequeueFailure(out var failure) && failure.Stage == "integration", "UI failure blocks capture");
+            session.Exclude("JipperResourcePack.Overlay");
+            Require(!root.activeSelf && session.ReadyForCapture, "error exclusion does not show native UI");
+        }
+        overlay.FailUpdate = false;
+        Require(root.activeSelf && text.text == "render-3", "failure does not roll back the previous frame");
+        root.FailComponentLookup = true;
+        using(var session = Begin(14)) {
+            Require(!session.TryDequeueFailure(out _), "render never snapshots or searches UI objects");
+            Frame(session, 0); VersionSafe.FlushAfter(); overlay.CompleteLayout();
+            Require(session.ReadyForCapture && root.activeSelf, "existing UI works without snapshot traversal");
+        }
+        root.FailComponentLookup = false;
+        Require(root.activeSelf && text.text == "render-4", "latest presentation remains after session end");
+        Console.WriteLine("PASS JRP UI failure, exclusion and rendering without display snapshots");
+
+        using(var oldSession = Begin(15)) {
+            Frame(oldSession, 0); VersionSafe.FlushAfter();
+            Require(!oldSession.ReadyForCapture, "old layout remains pending");
+        }
+        using(var newSession = Begin(16)) {
+            Frame(newSession, 0); VersionSafe.FlushAfter();
+            overlay.CompleteOneLayout();
+            Require(!newSession.ReadyForCapture, "an old layout callback cannot complete the new request");
+            overlay.CompleteOneLayout();
+            Require(newSession.ReadyForCapture, "only this request's layout completion permits capture");
+        }
+        Console.WriteLine("PASS JRP exact layout completion across cancelled and new sessions");
+    }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.OverlayContents; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 추가 | 현재 4 | namespace 또는 타입 별칭을 가져온다: UnityEngine; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 5 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 6 | 타입을 선언한다: internal static class OverlayReplayTests { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 7 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 8 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static TUFReplayRenderSession Begin(int id) =&gt; TUFReplaySession.BeginRender(new(id, 1920, 1080, 120)); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static void Frame(TUFReplayRenderSession session, long index) =&gt; session.RequestFrame(new(index, index * 0.1, 0.1f, Array.Empty&lt;int&gt;(), Array.Empty&lt;TUFReplayKeyEvent&gt;())); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 11 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal static void Run() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | namespace 또는 타입 별칭을 가져온다: var overlay = new Overlay(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: var root = overlay.GameObject; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 14 | 오른쪽 결과를 왼쪽 상태에 저장한다: var text = overlay.Label; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 오른쪽 결과를 왼쪽 상태에 저장한다: var nativeManager = overlay.OverlayTextManager; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(10)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 이 함수 호출/식 처리를 실행한다: "retain Overlay, text manager, Canvas and current combo without initialization"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 이 함수 호출/식 처리를 실행한다: Frame(session, 0); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 이 함수 호출/식 처리를 실행한다: session.EnterRefresh(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 이 함수 호출/식 처리를 실행한다: VersionSafe.FlushAfter(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 이 함수 호출/식 처리를 실행한다: overlay.CompleteLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 25 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 이 함수 호출/식 처리를 실행한다: Frame(session, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 이 함수 호출/식 처리를 실행한다: VersionSafe.FlushAfter(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 이 함수 호출/식 처리를 실행한다: overlay.CompleteLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 29 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 이 함수 호출/식 처리를 실행한다: session.Exclude("JipperResourcePack.Overlay"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 31 | 이 함수 호출/식 처리를 실행한다: Frame(session, 2); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 32 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 33 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 34 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 37 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 38 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var oldSession = Begin(11)) { Frame(oldSession, 0); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var newSession = Begin(12)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 40 | 이 함수 호출/식 처리를 실행한다: Frame(newSession, 0); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 41 | 이 함수 호출/식 처리를 실행한다: VersionSafe.FlushAfter(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 42 | 이 함수 호출/식 처리를 실행한다: overlay.CompleteLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 43 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 44 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 45 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 46 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 47 | 오른쪽 결과를 왼쪽 상태에 저장한다: overlay.FailUpdate = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 48 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(13)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 49 | 이 함수 호출/식 처리를 실행한다: Frame(session, 0); VersionSafe.FlushAfter(); overlay.CompleteLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 50 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 51 | 이 함수 호출/식 처리를 실행한다: session.Exclude("JipperResourcePack.Overlay"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 52 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 53 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: overlay.FailUpdate = false; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 55 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: root.FailComponentLookup = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(14)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 58 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 59 | 이 함수 호출/식 처리를 실행한다: Frame(session, 0); VersionSafe.FlushAfter(); overlay.CompleteLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 60 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 61 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: root.FailComponentLookup = false; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 63 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 64 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 65 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 66 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var oldSession = Begin(15)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 67 | 이 함수 호출/식 처리를 실행한다: Frame(oldSession, 0); VersionSafe.FlushAfter(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 68 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 69 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 70 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var newSession = Begin(16)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 71 | 이 함수 호출/식 처리를 실행한다: Frame(newSession, 0); VersionSafe.FlushAfter(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 72 | 이 함수 호출/식 처리를 실행한다: overlay.CompleteOneLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 73 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 74 | 이 함수 호출/식 처리를 실행한다: overlay.CompleteOneLayout(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 75 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 76 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 77 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 78 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 79 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/ViewerReplay/OverlayStubs.cs

현재 파일: [tests/ViewerReplay/OverlayStubs.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/OverlayStubs.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,66 @@

```diff
@@ -0,0 +1,66 @@
+using System.Diagnostics;
+using TMPro;
+using TUFReplay.Overlay;
+using UnityEngine;
+
+// Only the normal game/UI boundary is modeled here; Overlay.Render.cs is linked
+// from production to test lifecycle, same-object preservation and delayed ACK.
+namespace JipperResourcePack.OverlayContents;
+
+public enum ComboTier { White, Green }
+public sealed class IOverlayTextManager;
+public static class Combo { public static int ComboCount; }
+
+public partial class Overlay : IDisposable {
+    public static Overlay Instance;
+    public readonly GameObject GameObject = new("Overlay");
+    internal readonly TextMeshProUGUI Label;
+    internal readonly RectTransform Rect;
+    private int _lastTime = -1, _lastMapTime = -1;
+    private float LastTileBpm = -1, LastCurBpm = -1, MapTotalTime = -1;
+    private string MusicTimeCache = null, MapTimeCache = null;
+    private int[] Checkpoints = null, _scorableTiles = null;
+    private double _comboStartedAt = 0;
+    private bool _comboAnimating = false;
+    private readonly Stopwatch _stopwatch = new();
+    public PlayCount.Hash LastHash = new(new byte[16] { 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
+    public float StartProgress = 0.25f, LastMultiplier = 1;
+    public int StartTile = 5, NoCheckStartTile = 5;
+    public bool AutoOnceEnabled = false, SongPlaying = true, IsDeath = false;
+    public IOverlayTextManager OverlayTextManager = new();
+    internal int Updates;
+    private readonly Queue<Action> _pendingLayout = new();
+    internal bool FailUpdate;
+    internal float ObservedDelta;
+    internal bool CachesInvalidated => _lastTime == -1 && _lastMapTime == -1
+        && LastTileBpm == -1 && LastCurBpm == -1 && MapTotalTime == -1
+        && MusicTimeCache == null && MapTimeCache == null && Checkpoints == null && _scorableTiles == null;
+    internal bool AnimationRunning => _comboAnimating;
+
+    internal Overlay() {
+        Instance = this;
+        Label = GameObject.AddComponent<TextMeshProUGUI>();
+        Label.text = "native"; Label.color = new(1); Label.fontSize = 25;
+        Rect = GameObject.AddComponent<RectTransform>();
+        Rect.anchoredPosition = new(10, 20); Rect.sizeDelta = new(30, 40);
+        Combo.ComboCount = 17;
+        ConnectReplay();
+    }
+    private void SetupTextManager() => OverlayTextManager = new();
+    public void UpdateTime() {
+        if(FailUpdate) throw new InvalidOperationException("UI failure");
+        ObservedDelta = TUFReplaySession.DeltaTime;
+        _comboStartedAt = TUFReplaySession.ElapsedTime;
+        _comboAnimating = _comboStartedAt >= 0;
+        Updates++;
+        Label.text = "render-" + Updates; Label.fontSize = 100;
+        Rect.anchoredPosition = new(100, 200);
+        LastHash = new(new byte[16] { 99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); StartTile = 77; Combo.ComboCount++;
+    }
+    public void UpdateComboSize(Action completed = null) => _pendingLayout.Enqueue(() => completed?.Invoke());
+    internal void CompleteLayout() { while(_pendingLayout.TryDequeue(out var callback)) callback(); }
+    internal void CompleteOneLayout() { if(_pendingLayout.TryDequeue(out var callback)) callback(); }
+    public void Dispose() {
+        tufReplayConnection.Dispose(); EndRender(); UnityEngine.Object.Destroy(GameObject);
+    }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: System.Diagnostics; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: TMPro; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 추가 | 현재 4 | namespace 또는 타입 별칭을 가져온다: UnityEngine; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 5 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 6 | 주석: Only the normal game/UI boundary is modeled here; Overlay.Render.cs is linked | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 7 | 주석: from production to test lifecycle, same-object preservation and delayed ACK. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 8 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.OverlayContents; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 10 | 연결된 식·선언·설정의 일부다: public enum ComboTier { White, Green } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 타입을 선언한다: public sealed class IOverlayTextManager; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 타입을 선언한다: public static class Combo { public static int ComboCount; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 14 | 타입을 선언한다: public partial class Overlay : IDisposable { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Overlay Instance; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly GameObject GameObject = new("Overlay"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal readonly TextMeshProUGUI Label; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal readonly RectTransform Rect; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private int _lastTime = -1, _lastMapTime = -1; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private float LastTileBpm = -1, LastCurBpm = -1, MapTotalTime = -1; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private string MusicTimeCache = null, MapTimeCache = null; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private int[] Checkpoints = null, _scorableTiles = null; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private double _comboStartedAt = 0; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 애니메이션 진행 여부를 별도로 기록한다. | SDK 시계는 Stopwatch.IsRunning과 독립적이다. |
| 추가 | 현재 25 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private readonly Stopwatch _stopwatch = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public PlayCount.Hash LastHash = new(new byte[16] { 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public float StartProgress = 0.25f, LastMultiplier = 1; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int StartTile = 5, NoCheckStartTile = 5; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 29 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool AutoOnceEnabled = false, SongPlaying = true, IsDeath = false; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public IOverlayTextManager OverlayTextManager = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 31 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal int Updates; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 32 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private readonly Queue&lt;Action&gt; _pendingLayout = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 33 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal bool FailUpdate; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 34 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal float ObservedDelta; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal bool CachesInvalidated =&gt; _lastTime == -1 &amp;&amp; _lastMapTime == -1 | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 연결된 식·선언·설정의 일부다: &amp;&amp; LastTileBpm == -1 &amp;&amp; LastCurBpm == -1 &amp;&amp; MapTotalTime == -1 | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 37 | 이 함수 호출/식 처리를 실행한다: &amp;&amp; MusicTimeCache == null &amp;&amp; MapTimeCache == null &amp;&amp; Checkpoints == null &amp;&amp; _scorableTiles == null; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 38 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal bool AnimationRunning =&gt; _comboAnimating; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 39 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 40 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal Overlay() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 41 | 오른쪽 결과를 왼쪽 상태에 저장한다: Instance = this; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label = GameObject.AddComponent&lt;TextMeshProUGUI&gt;(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label.text = "native"; Label.color = new(1); Label.fontSize = 25; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 44 | 오른쪽 결과를 왼쪽 상태에 저장한다: Rect = GameObject.AddComponent&lt;RectTransform&gt;(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: Rect.anchoredPosition = new(10, 20); Rect.sizeDelta = new(30, 40); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 46 | 오른쪽 결과를 왼쪽 상태에 저장한다: Combo.ComboCount = 17; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 47 | 정상 객체 준비 뒤 SDK 연결을 등록한다. | 준비 전 UI·큐로 callback이 들어오지 않는다. |
| 추가 | 현재 48 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 49 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void SetupTextManager() =&gt; OverlayTextManager = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 50 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void UpdateTime() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 51 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: ObservedDelta = TUFReplaySession.DeltaTime; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 53 | SDK 시간에 맞춰 콤보 시작 원점을 지정한다. | 영상 경과 시간으로 애니메이션을 계산한다. |
| 추가 | 현재 54 | 애니메이션 진행 여부를 별도로 기록한다. | SDK 시계는 Stopwatch.IsRunning과 독립적이다. |
| 추가 | 현재 55 | 이 함수 호출/식 처리를 실행한다: Updates++; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label.text = "render-" + Updates; Label.fontSize = 100; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: Rect.anchoredPosition = new(100, 200); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 58 | 오른쪽 결과를 왼쪽 상태에 저장한다: LastHash = new(new byte[16] { 99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); StartTile = 77; Combo.ComboCount++; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 59 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 60 | 등록된 완료 callback을 호출한다. | 지연 UI 반영이 끝났거나 반영할 작업이 없을 때 완료한다. |
| 추가 | 현재 61 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal void CompleteLayout() { while(_pendingLayout.TryDequeue(out var callback)) callback(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 62 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal void CompleteOneLayout() { if(_pendingLayout.TryDequeue(out var callback)) callback(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 63 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void Dispose() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 64 | 남아 있는 재생 수명을 종료한다. | 폐기 경로에서도 공급원 전환/표시 정리를 수행한다. |
| 추가 | 현재 65 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 66 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/ViewerReplay/PlayCountReplayTests.cs

현재 파일: [tests/ViewerReplay/PlayCountReplayTests.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/PlayCountReplayTests.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,32 @@

```diff
@@ -0,0 +1,32 @@
+using JipperResourcePack;
+using JipperResourcePack.OverlayContents;
+using TUFReplay.Overlay;
+
+internal static class PlayCountReplayTests {
+    private static void Require(bool value, string message) { if(!value) throw new Exception(message); }
+
+    internal static void Run() {
+        PlayCount.Load();
+        var hash = PlayCount.GetMapHash();
+        var original = PlayCount.GetData(hash);
+        PlayCount.AddAttempts(hash, 0, 1);
+        PlayCount.SetBest(hash, 0, 0.5f, 1);
+        PlayCount.Save();
+        string path = Path.Combine(Main.Instance.Path, "Plays.dat");
+        byte[] before = File.ReadAllBytes(path);
+        using(var session = TUFReplaySession.BeginRender(new(20, 1920, 1080, 120))) {
+            Require(ReferenceEquals(PlayCount.GetData(hash), original) && original.GetAttempts() == 1 && original.GetBest(0, 1) == 0.5f,
+                "render reads existing attempts and best progress");
+            PlayCount.AddAttempts(hash, 0, 1);
+            PlayCount.SetBest(hash, 0, 0.75f, 1);
+            Require(original.GetAttempts() == 2 && original.GetBest(0, 1) == 0.75f, "render updates the same play record");
+            Require(!before.SequenceEqual(File.ReadAllBytes(path)) && File.Exists(path + ".bak"), "normal persistence and backup run during rendering");
+        }
+        PlayCount.Dispose();
+        PlayCount.Load();
+        Require(PlayCount.GetData(hash).GetAttempts() == 2 && PlayCount.GetData(hash).GetBest(0, 1) == 0.75f,
+            "render progress remains after reloading the saved profile");
+        Require(Main.Instance.Errors.IsEmpty, "play-record fixture had a persistence error");
+        Console.WriteLine("PASS JRP existing attempts/best record, accumulation, normal save and reload during rendering");
+    }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.OverlayContents; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 추가 | 현재 4 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 5 | 타입을 선언한다: internal static class PlayCountReplayTests { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 6 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 7 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 8 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal static void Run() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 이 함수 호출/식 처리를 실행한다: PlayCount.Load(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 오른쪽 결과를 왼쪽 상태에 저장한다: var hash = PlayCount.GetMapHash(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 오른쪽 결과를 왼쪽 상태에 저장한다: var original = PlayCount.GetData(hash); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 이 함수 호출/식 처리를 실행한다: PlayCount.AddAttempts(hash, 0, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 이 함수 호출/식 처리를 실행한다: PlayCount.SetBest(hash, 0, 0.5f, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 14 | 이 함수 호출/식 처리를 실행한다: PlayCount.Save(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 오른쪽 결과를 왼쪽 상태에 저장한다: string path = Path.Combine(Main.Instance.Path, "Plays.dat"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 오른쪽 결과를 왼쪽 상태에 저장한다: byte[] before = File.ReadAllBytes(path); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = TUFReplaySession.BeginRender(new(20, 1920, 1080, 120))) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 처리 요청과 SDK 대기 요청이 같은 객체인지 확인한다. | 이전·취소된 요청을 현재 요청 대신 완료하지 않는다. |
| 추가 | 현재 19 | 이 함수 호출/식 처리를 실행한다: "render reads existing attempts and best progress"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 이 함수 호출/식 처리를 실행한다: PlayCount.AddAttempts(hash, 0, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 이 함수 호출/식 처리를 실행한다: PlayCount.SetBest(hash, 0, 0.75f, 1); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 25 | 이 함수 호출/식 처리를 실행한다: PlayCount.Dispose(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 이 함수 호출/식 처리를 실행한다: PlayCount.Load(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 이 함수 호출/식 처리를 실행한다: "render progress remains after reloading the saved profile"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 29 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 31 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 32 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/ViewerReplay/PlayCountStubs.cs

현재 파일: [tests/ViewerReplay/PlayCountStubs.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/PlayCountStubs.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,40 @@

```diff
@@ -0,0 +1,40 @@
+using System.Text;
+
+// Game map hashing and JALib stream helpers are boundaries; persistence and
+// accumulation are the production PlayCount implementation linked by this test.
+public static class ADOBase {
+    public static bool isOfficialLevel = true;
+    public static string currentLevel = "fixture";
+    public static scrLevelMaker lm = new();
+    public static ADOFAI.CustomLevel customLevel = new();
+}
+public sealed class scrLevelMaker {
+    public bool isOldLevel;
+    public string leveldata = "";
+    public object floorAngles;
+}
+namespace ADOFAI {
+    public enum LevelEventType { SetSpeed, Twirl, Hold, MultiPlanet, Pause, AutoPlayTiles, ScaleMargin, Multitap, KillPlayer }
+    public enum SpeedType { Bpm }
+    public enum PlanetCount { Two }
+    public sealed class CustomLevel { public readonly List<LevelEvent> events = new(); }
+    public sealed class LevelEvent {
+        public LevelEventType eventType;
+        public int floor;
+        public object this[string key] => throw new NotSupportedException("Custom map hashing is outside this fixture.");
+    }
+}
+namespace JALib.Tools.ByteTool {
+    public static class StreamHelpers {
+        private static BinaryReader Reader(Stream stream) => new(stream, Encoding.UTF8, true);
+        private static BinaryWriter Writer(Stream stream) => new(stream, Encoding.UTF8, true);
+        public static int ReadInt(this Stream stream) => Reader(stream).ReadInt32();
+        public static float ReadFloat(this Stream stream) => Reader(stream).ReadSingle();
+        public static byte[] ReadBytes(this Stream stream, int count) => Reader(stream).ReadBytes(count);
+        public static void WriteInt(this Stream stream, int value) => Writer(stream).Write(value);
+        public static void WriteFloat(this Stream stream, float value) => Writer(stream).Write(value);
+        public static void WriteBoolean(this Stream stream, bool value) => Writer(stream).Write(value);
+        public static void WriteUTF(this Stream stream, string value) => Writer(stream).Write(value);
+        public static void WriteObject(this Stream stream, object value) => throw new NotSupportedException("Custom map hashing is outside this fixture.");
+    }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: System.Text; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 3 | 주석: Game map hashing and JALib stream helpers are boundaries; persistence and | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 4 | 주석: accumulation are the production PlayCount implementation linked by this test. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 5 | 타입을 선언한다: public static class ADOBase { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 6 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static bool isOfficialLevel = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 7 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static string currentLevel = "fixture"; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static scrLevelMaker lm = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static ADOFAI.CustomLevel customLevel = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 11 | 타입을 선언한다: public sealed class scrLevelMaker { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool isOldLevel; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string leveldata = ""; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public object floorAngles; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 16 | 이 타입의 소속 namespace를 지정한다: namespace ADOFAI { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 연결된 식·선언·설정의 일부다: public enum LevelEventType { SetSpeed, Twirl, Hold, MultiPlanet, Pause, AutoPlayTiles, ScaleMargin, Multitap, KillPlayer } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 연결된 식·선언·설정의 일부다: public enum SpeedType { Bpm } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 연결된 식·선언·설정의 일부다: public enum PlanetCount { Two } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 타입을 선언한다: public sealed class CustomLevel { public readonly List&lt;LevelEvent&gt; events = new(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 타입을 선언한다: public sealed class LevelEvent { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public LevelEventType eventType; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int floor; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 25 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 26 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 27 | 이 타입의 소속 namespace를 지정한다: namespace JALib.Tools.ByteTool { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 타입을 선언한다: public static class StreamHelpers { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 29 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static BinaryReader Reader(Stream stream) =&gt; new(stream, Encoding.UTF8, true); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static BinaryWriter Writer(Stream stream) =&gt; new(stream, Encoding.UTF8, true); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 31 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static int ReadInt(this Stream stream) =&gt; Reader(stream).ReadInt32(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 32 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static float ReadFloat(this Stream stream) =&gt; Reader(stream).ReadSingle(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 33 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static byte[] ReadBytes(this Stream stream, int count) =&gt; Reader(stream).ReadBytes(count); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 34 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void WriteInt(this Stream stream, int value) =&gt; Writer(stream).Write(value); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void WriteFloat(this Stream stream, float value) =&gt; Writer(stream).Write(value); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void WriteBoolean(this Stream stream, bool value) =&gt; Writer(stream).Write(value); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 37 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void WriteUTF(this Stream stream, string value) =&gt; Writer(stream).Write(value); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 38 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 39 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 40 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/ViewerReplay/Program.cs

현재 파일: [tests/ViewerReplay/Program.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/Program.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,93 @@

```diff
@@ -0,0 +1,93 @@
+using JipperResourcePack;
+using JipperResourcePack.KeyViewerContents;
+using SkyHook;
+using TUFReplay.Overlay;
+using UnityEngine;
+
+static void Require(bool value, string message) { if(!value) throw new Exception(message); }
+static TUFReplayFrame Frame(long index, params TUFReplayKeyEvent[] events) => new(index, 1 + index, 0.1f, Array.Empty<int>(), events);
+static TUFReplayKeyEvent Input(int key, bool pressed, double time, long sequence) => new(key, pressed, time, time, sequence);
+static TUFReplayRenderSession Begin(int id) => TUFReplaySession.BeginRender(new(id, 1920, 1080, 120));
+static void AwaitUi(KeyViewer viewer, TUFReplayRenderSession session) {
+    session.EnterRefresh();
+    Require(SpinWait.SpinUntil(() => { viewer.Tick(); return session.ReadyForCapture; }, TimeSpan.FromSeconds(2)), "worker/UI did not complete");
+    Require(Main.Instance.Errors.IsEmpty, "unexpected viewer error");
+}
+
+KeyViewer viewer = new();
+Directory.CreateDirectory(Main.Instance.Path);
+try {
+    var liveRoot = viewer.KeyViewerSizeObject;
+    var liveKeys = viewer.Keys;
+    var liveCounts = viewer.Counts;
+    var liveRain = KeyViewer.RainManager;
+    var livePressTimes = viewer.PressTimes;
+    var nativeClock = KeyViewer.Stopwatch;
+    long nativeTicks = KeyViewer.CurrentTicks;
+    using(var session = Begin(1)) {
+        Require(viewer.KeyViewerSizeObject == liveRoot && liveRoot.activeSelf && viewer.Keys == liveKeys && viewer.Counts == liveCounts && viewer.Counts.TotalCount == 17,
+            "render keeps the existing UI and accumulated counters");
+        Require(KeyViewer.CurrentTicks >= nativeTicks && KeyViewer.CurrentTicks - nativeTicks < TimeSpan.TicksPerSecond, "source change preserves the clock origin");
+        session.RequestFrame(Frame(0, Input(97, true, 0.10, 0), Input(97, false, 0.11, 1), Input(98, true, 0.12, 2), Input(98, false, 0.13, 3)));
+        Require(!session.ReadyForCapture, "worker alone cannot acknowledge queued Unity changes");
+        AwaitUi(viewer, session);
+        Require(viewer.Counts.TotalCount == 19 && viewer.Counts.Count[0] == 1 && viewer.Counts.Count[20] == 1, "shared binding/Work adds hand and foot counts to existing totals");
+        Require(viewer.Keys[0].Value.TMP.text == "1" && viewer.Total.Value.TMP.text == "19", "queued production AsyncText applied before capture");
+        Require(KeyViewer.RainManager.Rendered.Count == 2 && KeyViewer.RainManager.Rendered.All(r => r.FinishSize), "shared Work creates normal and ghost rain for short taps");
+        Require(Math.Abs(KeyViewer.RainManager.Rendered[0].FinalSizeY - (0.01 * TimeSpan.TicksPerSecond / RawRain.RainTickUnit * KeyViewer.Settings.rainSpeed)) < 0.001, "rain uses recorded event duration");
+        SkyHookManager.KeyUpdated.Fire(KeyLabel.A, true);
+        Require(viewer.Counts.TotalCount == 19 && liveCounts.TotalCount == 19, "physical input is blocked while replay owns the source");
+        string countPath = System.IO.Path.Combine(Main.Instance.Path, "KeyCount.dat");
+        Require(SpinWait.SpinUntil(() => {
+            try {
+                byte[] data = File.ReadAllBytes(countPath);
+                return data.Length == KeyViewer.FootOutIndex * 4 + 8 && BitConverter.ToInt32(data, 4) == 19;
+            } catch(IOException) { return false; }
+        }, TimeSpan.FromSeconds(3)), "existing accumulated counters use the normal file save path");
+    }
+    viewer.Tick();
+    Require(viewer.KeyViewerSizeObject == liveRoot && viewer.Keys == liveKeys && KeyViewer.Stopwatch == nativeClock && liveRoot.activeSelf && liveCounts.TotalCount == 21,
+        "end retains replay counts and reconciles the physical held keys through Work");
+    Require(liveKeys[0].Background.color == KeyViewer.Settings.BackgroundClicked && liveKeys[0].LastRain != null && viewer.PressTimes == livePressTimes && KeyViewer.RainManager == liveRain && liveRain.Rendered.Count == 3,
+        "native resume uses the same KPS queue, rain manager and key presentation");
+    Console.WriteLine("PASS shared hand/foot/ghost input, queued UI acknowledgement, event-time rain, counter saving and native resume");
+
+    using(var session = Begin(2)) {
+        var renderRoot = viewer.KeyViewerSizeObject;
+        var renderCounts = viewer.Counts;
+        Require(renderRoot == liveRoot && renderCounts == liveCounts && renderCounts.TotalCount == 21, "next render continues on the same objects and counts");
+        session.RequestFrame(Frame(0)); AwaitUi(viewer, session);
+        session.Exclude("JipperResourcePack.KeyViewer");
+        Require(!renderRoot.activeSelf, "exclusion hides the existing presentation");
+        session.RequestFrame(Frame(1, Input(97, true, 1.1, 0), Input(97, false, 1.2, 1)));
+        Require(session.ReadyForCapture && renderCounts.TotalCount == 21, "excluded queue receives no work");
+    }
+    Require(viewer.KeyViewerSizeObject == liveRoot && viewer.Keys == liveKeys && KeyViewer.Stopwatch == nativeClock && liveRoot.activeSelf, "exclusion restores only visibility at end");
+    Console.WriteLine("PASS continued state, exclusion and cancellation cleanup");
+
+    viewer.FailCreation = true;
+    using(var session = Begin(3)) {
+        Require(!session.TryDequeueFailure(out _), "render never invokes the UI factory");
+        session.RequestFrame(Frame(0)); AwaitUi(viewer, session);
+        Require(viewer.Keys == liveKeys && session.ReadyForCapture, "existing viewer still completes its request");
+    }
+    viewer.FailCreation = false;
+    Require(viewer.KeyViewerSizeObject == liveRoot && viewer.Keys == liveKeys && liveCounts.TotalCount == 21, "no reset or reconstruction across sessions");
+    Console.WriteLine("PASS rendering without UI reconstruction");
+    viewer.Tick();
+    int beforeHeldReplay = viewer.Counts.TotalCount;
+    Require(liveKeys[0].Background.color == KeyViewer.Settings.BackgroundClicked, "physical source is held before the next session");
+    using(var session = Begin(4)) {
+        session.RequestFrame(Frame(0, Input(97, true, 0.1, 0), Input(97, false, 0.11, 1)));
+        AwaitUi(viewer, session);
+        Require(viewer.Counts.TotalCount == beforeHeldReplay + 2 && liveKeys[0].Background.color == KeyViewer.Settings.Background,
+            "physical hold cannot mask replay press/release on the same keys");
+        SkyHookManager.KeyUpdated.Fire(KeyLabel.A, false);
+    }
+    viewer.Tick();
+    Require(viewer.Counts.TotalCount == beforeHeldReplay + 2, "returning to a released physical source does not alter counters");
+    Console.WriteLine("PASS source transition from held physical input to replay and back");
+} finally { viewer.Shutdown(); }
+
+OverlayReplayTests.Run();
+PlayCountReplayTests.Run();
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.KeyViewerContents; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | namespace 또는 타입 별칭을 가져온다: SkyHook; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 4 | SDK의 세션·연결·요청 타입 이름을 사용할 수 있게 한다. | SDK 공급 데이터와 완료 계약에 접근한다. |
| 추가 | 현재 5 | namespace 또는 타입 별칭을 가져온다: UnityEngine; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 6 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 7 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 8 | 이 함수 호출/식 처리를 실행한다: static TUFReplayFrame Frame(long index, params TUFReplayKeyEvent[] events) =&gt; new(index, 1 + index, 0.1f, Array.Empty&lt;int&gt;(), events); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 이 함수 호출/식 처리를 실행한다: static TUFReplayKeyEvent Input(int key, bool pressed, double time, long sequence) =&gt; new(key, pressed, time, time, sequence); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 이 함수 호출/식 처리를 실행한다: static TUFReplayRenderSession Begin(int id) =&gt; TUFReplaySession.BeginRender(new(id, 1920, 1080, 120)); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 연결된 식·선언·설정의 일부다: static void AwaitUi(KeyViewer viewer, TUFReplayRenderSession session) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 이 함수 호출/식 처리를 실행한다: session.EnterRefresh(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 14 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 16 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 17 | 오른쪽 결과를 왼쪽 상태에 저장한다: KeyViewer viewer = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 이 함수 호출/식 처리를 실행한다: Directory.CreateDirectory(Main.Instance.Path); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 작업을 실행하고 오류를 catch에 전달한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 오른쪽 결과를 왼쪽 상태에 저장한다: var liveRoot = viewer.KeyViewerSizeObject; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 오른쪽 결과를 왼쪽 상태에 저장한다: var liveKeys = viewer.Keys; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 오른쪽 결과를 왼쪽 상태에 저장한다: var liveCounts = viewer.Counts; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: var liveRain = KeyViewer.RainManager; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 오른쪽 결과를 왼쪽 상태에 저장한다: var livePressTimes = viewer.PressTimes; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: var nativeClock = KeyViewer.Stopwatch; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 오른쪽 결과를 왼쪽 상태에 저장한다: long nativeTicks = KeyViewer.CurrentTicks; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(1)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 29 | 이 함수 호출/식 처리를 실행한다: "render keeps the existing UI and accumulated counters"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 31 | 이 함수 호출/식 처리를 실행한다: session.RequestFrame(Frame(0, Input(97, true, 0.10, 0), Input(97, false, 0.11, 1), Input(98, true, 0.12, 2), Input(98, false, 0.13, 3))); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 32 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 33 | 이 함수 호출/식 처리를 실행한다: AwaitUi(viewer, session); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 34 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 37 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 38 | 이 함수 호출/식 처리를 실행한다: SkyHookManager.KeyUpdated.Fire(KeyLabel.A, true); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 39 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 40 | 오른쪽 결과를 왼쪽 상태에 저장한다: string countPath = System.IO.Path.Combine(Main.Instance.Path, "KeyCount.dat"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 41 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 42 | 작업을 실행하고 오류를 catch에 전달한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: byte[] data = File.ReadAllBytes(countPath); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 44 | 함수를 종료하거나 식의 결과를 반환한다: return data.Length == KeyViewer.FootOutIndex * 4 + 8 &amp;&amp; BitConverter.ToInt32(data, 4) == 19; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 45 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 46 | 이 함수 호출/식 처리를 실행한다: }, TimeSpan.FromSeconds(3)), "existing accumulated counters use the normal file save path"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 47 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 48 | 이 함수 호출/식 처리를 실행한다: viewer.Tick(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 49 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 50 | 이 함수 호출/식 처리를 실행한다: "end retains replay counts and reconciles the physical held keys through Work"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 51 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 52 | 이 함수 호출/식 처리를 실행한다: "native resume uses the same KPS queue, rain manager and key presentation"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 53 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 54 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 55 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(2)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: var renderRoot = viewer.KeyViewerSizeObject; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: var renderCounts = viewer.Counts; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 58 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 59 | 이 함수 호출/식 처리를 실행한다: session.RequestFrame(Frame(0)); AwaitUi(viewer, session); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 60 | 이 함수 호출/식 처리를 실행한다: session.Exclude("JipperResourcePack.KeyViewer"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 61 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 62 | 이 함수 호출/식 처리를 실행한다: session.RequestFrame(Frame(1, Input(97, true, 1.1, 0), Input(97, false, 1.2, 1))); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 63 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 64 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 65 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 66 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 67 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: viewer.FailCreation = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(3)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 70 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 71 | 이 함수 호출/식 처리를 실행한다: session.RequestFrame(Frame(0)); AwaitUi(viewer, session); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 72 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 73 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: viewer.FailCreation = false; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 75 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 76 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 77 | 이 함수 호출/식 처리를 실행한다: viewer.Tick(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 78 | 오른쪽 결과를 왼쪽 상태에 저장한다: int beforeHeldReplay = viewer.Counts.TotalCount; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 79 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 80 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(var session = Begin(4)) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 81 | 이 함수 호출/식 처리를 실행한다: session.RequestFrame(Frame(0, Input(97, true, 0.1, 0), Input(97, false, 0.11, 1))); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 82 | 이 함수 호출/식 처리를 실행한다: AwaitUi(viewer, session); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 83 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 84 | 이 함수 호출/식 처리를 실행한다: "physical hold cannot mask replay press/release on the same keys"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 85 | 이 함수 호출/식 처리를 실행한다: SkyHookManager.KeyUpdated.Fire(KeyLabel.A, false); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 86 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 87 | 이 함수 호출/식 처리를 실행한다: viewer.Tick(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 88 | 이 조건을 검사하고 만족하지 않으면 테스트를 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 89 | 작업/검사 결과를 출력한다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 90 | 연결된 식·선언·설정의 일부다: } finally { viewer.Shutdown(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 91 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 92 | 이 함수 호출/식 처리를 실행한다: OverlayReplayTests.Run(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 93 | 이 함수 호출/식 처리를 실행한다: PlayCountReplayTests.Run(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |

## tests/ViewerReplay/Stubs.cs

현재 파일: [tests/ViewerReplay/Stubs.cs](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/Stubs.cs)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,156 @@

```diff
@@ -0,0 +1,156 @@
+using System.Collections.Concurrent;
+using System.Reflection.Emit;
+using JipperResourcePack.Async;
+using JipperResourcePack.KeyViewerContents;
+
+// Models Unity and SkyHook boundaries only. The production listener/worker,
+// ProcessKeyEvent, Work, ordinary presentation creation, completion and resume code are linked.
+namespace UnityEngine {
+    public class Object {
+        internal bool Destroyed;
+        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
+        public static void Destroy(Object value) { if(value != null) value.Destroyed = true; }
+    }
+    public class Component : Object { public GameObject gameObject; }
+    public class MonoBehaviour : Component { public bool enabled = true; }
+    public class Transform : Component {
+        public Vector3 localScale;
+        public void SetParent(Transform parent, bool worldPositionStays = true) { }
+    }
+    public class RectTransform : Transform { public Vector2 anchorMin, anchorMax, offsetMin, offsetMax, anchoredPosition, sizeDelta; }
+    public class GameObject(string name) : Object {
+        public readonly string Name = name;
+        public bool activeSelf = true;
+        public Transform transform = new();
+        private readonly List<Component> _components = new();
+        public bool FailComponentLookup;
+        public T[] GetComponentsInChildren<T>(bool includeInactive) {
+            if(FailComponentLookup) throw new InvalidOperationException("lookup failure");
+            return _components.OfType<T>().ToArray();
+        }
+        public void SetActive(bool value) => activeSelf = value;
+        public T AddComponent<T>() where T : Component, new() {
+            var component = new T { gameObject = this };
+            _components.Add(component);
+            if(component is Transform t) transform = t;
+            return component;
+        }
+    }
+    public readonly record struct Vector2(float x, float y) { public static Vector2 zero => default; }
+    public readonly record struct Vector3(float x, float y, float z);
+    public readonly record struct Color(int Value);
+    public enum KeyCode { None = 0, A = 97, B = 98, Less = 60, Greater = 62, Pipe = 124, Comma = 44, Equals = 61, Backslash = 92 }
+}
+namespace UnityEngine.UI { public class Image : UnityEngine.Component { public UnityEngine.Color color; } }
+namespace TMPro { public class TextMeshProUGUI : UnityEngine.Component { public string text; public UnityEngine.Color color; public float fontSize; } }
+namespace SkyHook {
+    public enum KeyLabel { Unknown, A, B }
+    public enum EventType { KeyPressed, KeyReleased }
+    public struct AsyncKeyCode { public KeyLabel label; public ushort key; }
+    public readonly record struct SkyHookEvent(KeyLabel Label, ushort Key, EventType Type, long Ticks) { public long GetTimeInTicks() => Ticks; }
+    public sealed class HookListeners {
+        private event Action<SkyHookEvent> listeners;
+        public void AddListener(Action<SkyHookEvent> listener) => listeners += listener;
+        public void RemoveListener(Action<SkyHookEvent> listener) => listeners -= listener;
+        public void Fire(KeyLabel label, bool pressed) => listeners?.Invoke(new SkyHookEvent(label, 0, pressed ? EventType.KeyPressed : EventType.KeyReleased, DateTime.UtcNow.Ticks));
+    }
+    public static class SkyHookManager { public static readonly HookListeners KeyUpdated = new(); }
+}
+namespace JALib.Tools {
+    public static class MainThread {
+        internal static readonly ConcurrentQueue<Action> Pending = new();
+        public static void Run(JipperResourcePack.Main main, Action action) => Pending.Enqueue(action);
+    }
+}
+namespace JALib.Core.Patch {
+    public enum PatchType { Transpiler }
+    [AttributeUsage(AttributeTargets.Method)]
+    public sealed class JAPatchAttribute(Type type, string name, PatchType kind, bool flag) : Attribute {
+        public readonly Type Type = type;
+        public readonly string Name = name;
+        public readonly PatchType Kind = kind;
+        public readonly bool Flag = flag;
+    }
+}
+public sealed class CodeInstruction {
+    public OpCode opcode; public object operand; public readonly List<Label> labels = new();
+    public CodeInstruction(OpCode opcode, object operand) { this.opcode = opcode; this.operand = operand; }
+}
+public sealed class RDInputType_Keyboard { public void MainIgnoreActive() { } }
+public sealed class RDInputType_AsyncKeyboard { public void Main() { } }
+public static class RDInput { public static bool useKeyLimiter; }
+public static class Persistence { public static readonly KeyLimiter keyLimiterKeys = new(); }
+public sealed class KeyLimiter { public HashSet<ushort> asyncKeysCache = new(); }
+namespace JipperResourcePack {
+    public sealed class Main {
+        public static readonly Main Instance = new();
+        public string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jrp-viewer-test-" + Guid.NewGuid().ToString("N"));
+        public readonly ConcurrentQueue<Exception> Errors = new();
+        public void LogException(Exception error) => Errors.Enqueue(error);
+        public void LogException(string message, Exception error) => Errors.Enqueue(error);
+        public void LogReportException(Exception error) => Errors.Enqueue(error);
+    }
+    public static class VersionSafe {
+        internal static readonly ConcurrentQueue<Action> After = new();
+        public static void RunAfter(Action action) => After.Enqueue(action);
+        internal static void FlushAfter() { while(After.TryDequeue(out var action)) action(); }
+        public static SkyHook.KeyLabel UnityKeyToSkyHookKey(UnityEngine.KeyCode key) => key switch { UnityEngine.KeyCode.A => SkyHook.KeyLabel.A, UnityEngine.KeyCode.B => SkyHook.KeyLabel.B, _ => SkyHook.KeyLabel.Unknown };
+        public static UnityEngine.KeyCode SkyHookKeyToUnityKey(SkyHook.KeyLabel key) => key switch { SkyHook.KeyLabel.A => UnityEngine.KeyCode.A, SkyHook.KeyLabel.B => UnityEngine.KeyCode.B, _ => UnityEngine.KeyCode.None };
+    }
+}
+namespace JipperResourcePack.KeyViewerContents {
+    public enum KeyviewerStyle { Key10, Key12, Key16, Key20 }
+    public sealed class KeyViewerSetting {
+        public bool useRain = true, useGhostRain = true, AutoSetupKeyLimit;
+        public float Size = 1, rainSpeed = 100;
+        public KeyviewerStyle KeyViewerStyle = KeyviewerStyle.Key12;
+        public UnityEngine.Color Background = new(1), BackgroundClicked = new(2), Outline = new(3), OutlineClicked = new(4), Text = new(5), TextClicked = new(6);
+    }
+    public sealed class RainManager : UnityEngine.MonoBehaviour {
+        public readonly ConcurrentQueue<RawRain> RawRainQueue = new();
+        public readonly List<RawRain> Rendered = new();
+        internal void Refresh() { while(RawRainQueue.TryDequeue(out var rain)) Rendered.Add(rain); }
+    }
+    public sealed class RainPool;
+    public partial class KeyViewer {
+        public const int HandOutIndex = 20, FootOutIndex = 36, GhostOutIndex = 56;
+        public static KeyViewer Instance;
+        public static KeyViewerSetting Settings = new();
+        public bool Enabled = true;
+        public UnityEngine.GameObject KeyViewerObject = new("Viewer");
+        public KeyViewerUpdater Updater = new();
+        private int _selectedKey = -1, _changeState = 0;
+        public static RainManager RainManager;
+        public UnityEngine.GameObject KeyViewerSizeObject;
+        public Key[] Keys;
+        public Key Kps, Total;
+        public static System.Diagnostics.Stopwatch Stopwatch = System.Diagnostics.Stopwatch.StartNew();
+        private static UnityEngine.KeyCode[] GetKeyCode() => [UnityEngine.KeyCode.A];
+        private static UnityEngine.KeyCode[] GetFootKeyCode() => [UnityEngine.KeyCode.A];
+        private static UnityEngine.KeyCode[] GetGhostKeyCode() => [UnityEngine.KeyCode.B];
+        internal bool FailCreation;
+        private void InitializeKeyViewer() {
+            if(FailCreation) throw new InvalidOperationException("creation failure");
+            Keys[0] = MakeKey(); Kps = MakeKey(); Total = MakeKey();
+        }
+        private void InitializeFootKeyViewer() => Keys[HandOutIndex] = MakeKey();
+        private static Key MakeKey() => new(new UnityEngine.GameObject("Key")) {
+            Color = 1, Background = new(), Outline = new(), Text = new(new TMPro.TextMeshProUGUI()), Value = new(new TMPro.TextMeshProUGUI())
+        };
+        internal KeyViewer() {
+            Instance = this; KeyCountData.Instance = new() { TotalCount = 17 };
+            _asyncKeyLimitKeys = new();
+            RainManager = KeyViewerObject.AddComponent<RainManager>();
+            KeyViewerSizeObject = new UnityEngine.GameObject("SizeObject");
+            KeyViewerSizeObject.AddComponent<UnityEngine.RectTransform>().SetParent(KeyViewerObject.transform);
+            Keys = new Key[FootOutIndex];
+            InitializeKeyViewer(); InitializeFootKeyViewer();
+            _pressTimes = new ConcurrentQueue<long>();
+            RebuildKeyBinding(); StartEventListener(); ConnectReplay();
+        }
+        internal KeyCountData Counts => KeyCountData.Instance;
+        internal ConcurrentQueue<long> PressTimes => _pressTimes;
+        internal void Tick() => typeof(KeyViewerUpdater).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Updater, null);
+        internal void Shutdown() { tufReplayConnection.Dispose(); EndRender(); StopEventListener(); UnityEngine.Object.Destroy(KeyViewerObject); }
+    }
+}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | namespace 또는 타입 별칭을 가져온다: System.Collections.Concurrent; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | namespace 또는 타입 별칭을 가져온다: System.Reflection.Emit; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Async; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 4 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.KeyViewerContents; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 5 | 빈 줄을 추가한다. | 실행 로직에 영향이 없는 구분 줄이다. |
| 추가 | 현재 6 | 주석: Models Unity and SkyHook boundaries only. The production listener/worker, | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 7 | 주석: ProcessKeyEvent, Work, ordinary presentation creation, completion and resume code are linked. | 역할·제약을 설명하며 실행되지 않는다. |
| 추가 | 현재 8 | 이 타입의 소속 namespace를 지정한다: namespace UnityEngine { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 타입을 선언한다: public class Object { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal bool Destroyed; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static implicit operator bool(Object value) =&gt; value != null &amp;&amp; !value.Destroyed; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void Destroy(Object value) { if(value != null) value.Destroyed = true; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 14 | 타입을 선언한다: public class Component : Object { public GameObject gameObject; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 타입을 선언한다: public class MonoBehaviour : Component { public bool enabled = true; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 타입을 선언한다: public class Transform : Component { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Vector3 localScale; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void SetParent(Transform parent, bool worldPositionStays = true) { } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 20 | 타입을 선언한다: public class RectTransform : Transform { public Vector2 anchorMin, anchorMax, offsetMin, offsetMax, anchoredPosition, sizeDelta; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 21 | 타입을 선언한다: public class GameObject(string name) : Object { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 22 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly string Name = name; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool activeSelf = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 24 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Transform transform = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 25 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private readonly List&lt;Component&gt; _components = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 26 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool FailComponentLookup; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 27 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public T[] GetComponentsInChildren&lt;T&gt;(bool includeInactive) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 28 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 29 | 함수를 종료하거나 식의 결과를 반환한다: return _components.OfType&lt;T&gt;().ToArray(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 30 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 31 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void SetActive(bool value) =&gt; activeSelf = value; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 32 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public T AddComponent&lt;T&gt;() where T : Component, new() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 33 | 오른쪽 결과를 왼쪽 상태에 저장한다: var component = new T { gameObject = this }; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 34 | 이 함수 호출/식 처리를 실행한다: _components.Add(component); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 35 | 조건을 만족할 때 뒤의 작업을 수행한다: if(component is Transform t) transform = t; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 36 | 함수를 종료하거나 식의 결과를 반환한다: return component; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 37 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 38 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 39 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct Vector2(float x, float y) { public static Vector2 zero =&gt; default; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 40 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct Vector3(float x, float y, float z); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 41 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct Color(int Value); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: public enum KeyCode { None = 0, A = 97, B = 98, Less = 60, Greater = 62, Pipe = 124, Comma = 44, Equals = 61, Backslash = 92 } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 43 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 44 | 이 타입의 소속 namespace를 지정한다: namespace UnityEngine.UI { public class Image : UnityEngine.Component { public UnityEngine.Color color; } } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 45 | 이 타입의 소속 namespace를 지정한다: namespace TMPro { public class TextMeshProUGUI : UnityEngine.Component { public string text; public UnityEngine.Color color; public float fontSize; } } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 46 | 이 타입의 소속 namespace를 지정한다: namespace SkyHook { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 47 | 연결된 식·선언·설정의 일부다: public enum KeyLabel { Unknown, A, B } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 48 | 연결된 식·선언·설정의 일부다: public enum EventType { KeyPressed, KeyReleased } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 49 | 값 타입 데이터/큐 항목을 선언한다: public struct AsyncKeyCode { public KeyLabel label; public ushort key; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 50 | 값 타입 데이터/큐 항목을 선언한다: public readonly record struct SkyHookEvent(KeyLabel Label, ushort Key, EventType Type, long Ticks) { public long GetTimeInTicks() =&gt; Ticks; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 51 | 타입을 선언한다: public sealed class HookListeners { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 52 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private event Action&lt;SkyHookEvent&gt; listeners; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 53 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void AddListener(Action&lt;SkyHookEvent&gt; listener) =&gt; listeners += listener; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 54 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void RemoveListener(Action&lt;SkyHookEvent&gt; listener) =&gt; listeners -= listener; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 55 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void Fire(KeyLabel label, bool pressed) =&gt; listeners?.Invoke(new SkyHookEvent(label, 0, pressed ? EventType.KeyPressed : EventType.KeyReleased, DateTime.UtcNow.Ticks)); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 56 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 57 | 타입을 선언한다: public static class SkyHookManager { public static readonly HookListeners KeyUpdated = new(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 58 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 59 | 이 타입의 소속 namespace를 지정한다: namespace JALib.Tools { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 60 | 타입을 선언한다: public static class MainThread { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 61 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal static readonly ConcurrentQueue&lt;Action&gt; Pending = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 62 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void Run(JipperResourcePack.Main main, Action action) =&gt; Pending.Enqueue(action); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 63 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 64 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 65 | 이 타입의 소속 namespace를 지정한다: namespace JALib.Core.Patch { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 66 | 연결된 식·선언·설정의 일부다: public enum PatchType { Transpiler } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 67 | 메타데이터/compiler 지시를 지정한다: [AttributeUsage(AttributeTargets.Method)] | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 68 | 타입을 선언한다: public sealed class JAPatchAttribute(Type type, string name, PatchType kind, bool flag) : Attribute { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 69 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly Type Type = type; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 70 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly string Name = name; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 71 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly PatchType Kind = kind; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 72 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly bool Flag = flag; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 73 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 74 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 75 | 타입을 선언한다: public sealed class CodeInstruction { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 76 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public OpCode opcode; public object operand; public readonly List&lt;Label&gt; labels = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 77 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public CodeInstruction(OpCode opcode, object operand) { this.opcode = opcode; this.operand = operand; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 78 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 79 | 타입을 선언한다: public sealed class RDInputType_Keyboard { public void MainIgnoreActive() { } } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 80 | 타입을 선언한다: public sealed class RDInputType_AsyncKeyboard { public void Main() { } } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 81 | 타입을 선언한다: public static class RDInput { public static bool useKeyLimiter; } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 82 | 타입을 선언한다: public static class Persistence { public static readonly KeyLimiter keyLimiterKeys = new(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 83 | 타입을 선언한다: public sealed class KeyLimiter { public HashSet&lt;ushort&gt; asyncKeysCache = new(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 84 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 85 | 타입을 선언한다: public sealed class Main { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 86 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static readonly Main Instance = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 87 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jrp-viewer-test-" + Guid.NewGuid().ToString("N")); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 88 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly ConcurrentQueue&lt;Exception&gt; Errors = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 89 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void LogException(Exception error) =&gt; Errors.Enqueue(error); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 90 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void LogException(string message, Exception error) =&gt; Errors.Enqueue(error); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 91 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void LogReportException(Exception error) =&gt; Errors.Enqueue(error); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 92 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 93 | 타입을 선언한다: public static class VersionSafe { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 94 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal static readonly ConcurrentQueue&lt;Action&gt; After = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 95 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void RunAfter(Action action) =&gt; After.Enqueue(action); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 96 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal static void FlushAfter() { while(After.TryDequeue(out var action)) action(); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 97 | Unity 기록 키를 기존 SkyHook label로 변환한다. | 운영체제 native code를 만들어내지 않고 기존 LabelMap을 쓴다. |
| 추가 | 현재 98 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static UnityEngine.KeyCode SkyHookKeyToUnityKey(SkyHook.KeyLabel key) =&gt; key switch { SkyHook.KeyLabel.A =&gt; UnityEngine.KeyCode.A, SkyHook.KeyLabel.B =&gt; UnityEngine.KeyCode.B, _ =&gt; UnityEngine.KeyCode.None }; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 99 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 100 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 101 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.KeyViewerContents { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 102 | 연결된 식·선언·설정의 일부다: public enum KeyviewerStyle { Key10, Key12, Key16, Key20 } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 103 | 타입을 선언한다: public sealed class KeyViewerSetting { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 104 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool useRain = true, useGhostRain = true, AutoSetupKeyLimit; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 105 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public float Size = 1, rainSpeed = 100; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 106 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public KeyviewerStyle KeyViewerStyle = KeyviewerStyle.Key12; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 107 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public UnityEngine.Color Background = new(1), BackgroundClicked = new(2), Outline = new(3), OutlineClicked = new(4), Text = new(5), TextClicked = new(6); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 108 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 109 | 타입을 선언한다: public sealed class RainManager : UnityEngine.MonoBehaviour { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 110 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly ConcurrentQueue&lt;RawRain&gt; RawRainQueue = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 111 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public readonly List&lt;RawRain&gt; Rendered = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 112 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal void Refresh() { while(RawRainQueue.TryDequeue(out var rain)) Rendered.Add(rain); } | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 113 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 114 | 타입을 선언한다: public sealed class RainPool; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 115 | 타입을 선언한다: public partial class KeyViewer { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 116 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public const int HandOutIndex = 20, FootOutIndex = 36, GhostOutIndex = 56; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 117 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static KeyViewer Instance; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 118 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static KeyViewerSetting Settings = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 119 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool Enabled = true; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 120 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public UnityEngine.GameObject KeyViewerObject = new("Viewer"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 121 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public KeyViewerUpdater Updater = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 122 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private int _selectedKey = -1, _changeState = 0; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 123 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static RainManager RainManager; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 124 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public UnityEngine.GameObject KeyViewerSizeObject; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 125 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Key[] Keys; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 126 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Key Kps, Total; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 127 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static System.Diagnostics.Stopwatch Stopwatch = System.Diagnostics.Stopwatch.StartNew(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 128 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static UnityEngine.KeyCode[] GetKeyCode() =&gt; [UnityEngine.KeyCode.A]; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 129 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static UnityEngine.KeyCode[] GetFootKeyCode() =&gt; [UnityEngine.KeyCode.A]; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 130 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static UnityEngine.KeyCode[] GetGhostKeyCode() =&gt; [UnityEngine.KeyCode.B]; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 131 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal bool FailCreation; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 132 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void InitializeKeyViewer() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 133 | 오류를 발생시켜 현재 작업을 실패시킨다. | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 134 | 오른쪽 결과를 왼쪽 상태에 저장한다: Keys[0] = MakeKey(); Kps = MakeKey(); Total = MakeKey(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 135 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 136 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void InitializeFootKeyViewer() =&gt; Keys[HandOutIndex] = MakeKey(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 137 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static Key MakeKey() =&gt; new(new UnityEngine.GameObject("Key")) { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 138 | 오른쪽 결과를 왼쪽 상태에 저장한다: Color = 1, Background = new(), Outline = new(), Text = new(new TMPro.TextMeshProUGUI()), Value = new(new TMPro.TextMeshProUGUI()) | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 139 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 140 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal KeyViewer() { | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 141 | 오른쪽 결과를 왼쪽 상태에 저장한다: Instance = this; KeyCountData.Instance = new() { TotalCount = 17 }; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 142 | 오른쪽 결과를 왼쪽 상태에 저장한다: _asyncKeyLimitKeys = new(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 143 | 오른쪽 결과를 왼쪽 상태에 저장한다: RainManager = KeyViewerObject.AddComponent&lt;RainManager&gt;(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 144 | 오른쪽 결과를 왼쪽 상태에 저장한다: KeyViewerSizeObject = new UnityEngine.GameObject("SizeObject"); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 145 | 이 함수 호출/식 처리를 실행한다: KeyViewerSizeObject.AddComponent&lt;UnityEngine.RectTransform&gt;().SetParent(KeyViewerObject.transform); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 146 | 오른쪽 결과를 왼쪽 상태에 저장한다: Keys = new Key[FootOutIndex]; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 147 | 이 함수 호출/식 처리를 실행한다: InitializeKeyViewer(); InitializeFootKeyViewer(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 148 | 오른쪽 결과를 왼쪽 상태에 저장한다: _pressTimes = new ConcurrentQueue&lt;long&gt;(); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 149 | 정상 객체 준비 뒤 SDK 연결을 등록한다. | 준비 전 UI·큐로 callback이 들어오지 않는다. |
| 추가 | 현재 150 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 151 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal KeyCountData Counts =&gt; KeyCountData.Instance; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 152 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: internal ConcurrentQueue&lt;long&gt; PressTimes =&gt; _pressTimes; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 153 | 함수/생성자의 이름·입력·반환 형식을 선언한다: internal void Tick() =&gt; typeof(KeyViewerUpdater).GetMethod("Update", System.Reflection.BindingFlags.Instance \| System.Reflection.BindingFlags.NonPublic).Invoke(Updater, null); | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 154 | 남아 있는 재생 수명을 종료한다. | 폐기 경로에서도 공급원 전환/표시 정리를 수행한다. |
| 추가 | 현재 155 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |
| 추가 | 현재 156 | 현재 블록/타입의 끝이다. | 괄호는 실행/타입 범위를 지정한다. |

## tests/ViewerReplay/ViewerReplay.csproj

현재 파일: [tests/ViewerReplay/ViewerReplay.csproj](/Users/kgh/dev/src/jipper-resourcepack/tests/ViewerReplay/ViewerReplay.csproj)

실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다.

### @@ -0,0 +1,20 @@

```diff
@@ -0,0 +1,20 @@
+<Project Sdk="Microsoft.NET.Sdk">
+  <PropertyGroup>
+    <OutputType>Exe</OutputType>
+    <TargetFramework>net10.0</TargetFramework>
+    <ImplicitUsings>enable</ImplicitUsings>
+    <Nullable>disable</Nullable>
+    <OverlaySdkPath Condition="'$(OverlaySdkPath)' == ''">$(MSBuildThisFileDirectory)../../libs/TUFReplay.Overlay.dll</OverlaySdkPath>
+  </PropertyGroup>
+  <ItemGroup>
+    <Reference Include="TUFReplay.Overlay"><HintPath>$(OverlaySdkPath)</HintPath></Reference>
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs" Link="KeyViewer.Update.cs" />
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs" Link="KeyViewer.Render.cs" />
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/KeyCountData.cs" Link="KeyCountData.cs" />
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/Key.cs" Link="Key.cs" />
+    <Compile Include="../../JipperResourcePack/KeyViewerContents/RawRain.cs" Link="RawRain.cs" />
+    <Compile Include="../../JipperResourcePack/Async/AsyncText.cs" Link="AsyncText.cs" />
+    <Compile Include="../../JipperResourcePack/OverlayContents/Overlay.Render.cs" Link="Overlay.Render.cs" />
+    <Compile Include="../../JipperResourcePack/OverlayContents/PlayCount.cs" Link="PlayCount.cs" />
+  </ItemGroup>
+</Project>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 추가 | 현재 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Project Sdk="Microsoft.NET.Sdk"&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OutputType&gt;Exe&lt;/OutputType&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;TargetFramework&gt;net10.0&lt;/TargetFramework&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Nullable&gt;disable&lt;/Nullable&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OverlaySdkPath Condition="'$(OverlaySdkPath)' == ''"&gt;$(MSBuildThisFileDirectory)../../libs/TUFReplay.Overlay.dll&lt;/OverlaySdkPath&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 8 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 9 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="TUFReplay.Overlay"&gt;&lt;HintPath&gt;$(OverlaySdkPath)&lt;/HintPath&gt;&lt;/Reference&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 11 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/KeyViewer.Update.cs" Link="KeyViewer.Update.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 12 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/KeyViewer.Render.cs" Link="KeyViewer.Render.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 13 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/KeyCountData.cs" Link="KeyCountData.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 14 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/Key.cs" Link="Key.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 15 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/KeyViewerContents/RawRain.cs" Link="RawRain.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 16 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/Async/AsyncText.cs" Link="AsyncText.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 17 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/OverlayContents/Overlay.Render.cs" Link="Overlay.Render.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 18 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="../../JipperResourcePack/OverlayContents/PlayCount.cs" Link="PlayCount.cs" /&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 19 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |
| 추가 | 현재 20 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Project&gt; | 실제 기능 코드와 SDK를 연결하고 외부 경계는 stub으로 검사한다. |


## 줄별 설명 포함 여부

현재 소스의 파일별 SHA-256을 다시 비교했으며 문서 작성 중 대상 파일의 변경은 없었다. 본문에는 추가·삭제 변경 줄 1,336개에 대응하는 설명 행 1,336개가 있다. Installer 부록에는 삭제 줄 2,764개에 대응하는 설명 행 2,764개가 있다. 바이너리와 라이선스 동봉물은 줄별 코드 설명 대신 역할과 해시로 포함했다. HEAD 기준은 위 문서 시작 부분에 기록했다.

