# JipperResourcePack Installer 삭제 줄별 설명

작성일: 2026-10-13 (Asia/Seoul)

비교 기준 HEAD ac6d6410571ee7d585aedce7f5085de34f615af2. 대상은 기존 Installer/ 삭제 41개 파일이다. [SDK 연동 설명 본문](./jrp-sdk-diff-line-explanation-2026-10-13.md)과 같은 작업 트리 기준이다.

각 텍스트 파일의 삭제 diff 전체와 삭제 줄별 의사코드를 붙였다. 파일을 없앤 이유는 현재 소스 diff에서 확인할 수 없다. 아래 이유 칸이 삭제를 SDK 필수 작업으로 정당화하지 않는다. 이 작업에서는 삭제를 새로 실행하거나 되돌리지 않았다.

## Installer/App.config

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,6 +0,0 @@

```diff
@@ -1,6 +0,0 @@
-﻿<?xml version="1.0" encoding="utf-8" ?>
-<configuration>
-    <startup> 
-        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8.1" />
-    </startup>
-</configuration>
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version="1.0" encoding="utf-8" ?&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;configuration&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;startup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8.1" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/startup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/configuration&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Constants.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,15 +0,0 @@

```diff
@@ -1,15 +0,0 @@
-﻿using System.Drawing;
-
-namespace JipperResourcePack.Installer;
-
-public static class Constants {
-    public static Font Arial10 = new("Arial", 10);
-    public static Font Arial11 = new("Arial", 11);
-    public static Font Arial12 = new("Arial", 12);
-    public static Font Arial13 = new("Arial", 13);
-    public static Font Arial16 = new("Arial", 16);
-    public static Font Arial16B = new("Arial", 16, FontStyle.Bold);
-    public static Font Arial24B = new("Arial", 24, FontStyle.Bold);
-
-    public static Color SubColor = Color.FromArgb(109, 109, 109);
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 타입을 선언한다: public static class Constants { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial10 = new("Arial", 10); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial11 = new("Arial", 11); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial12 = new("Arial", 12); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial13 = new("Arial", 13); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial16 = new("Arial", 16); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial16B = new("Arial", 16, FontStyle.Bold); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Font Arial24B = new("Arial", 24, FontStyle.Bold); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Color SubColor = Color.FromArgb(109, 109, 109); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/GlobalSetting.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,27 +0,0 @@

```diff
@@ -1,27 +0,0 @@
-﻿using System.Collections.Generic;
-using System.Threading.Tasks;
-
-namespace JipperResourcePack.Installer;
-
-public class GlobalSetting {
-    public static GlobalSetting Instance = new();
-    public string InstallPath;
-    public Task<ModData[]> AdditionMods;
-    public int AdofaiRevision = -1;
-
-    public bool SelectSaved;
-    public bool IsUninstall;
-    
-    // Install
-    public bool InstallUnityModManager;
-    public bool InstallDoorstop;
-    public bool InstallJalib;
-    public bool InstallJipperResourcePack;
-    public List<ModData> SelectedMods;
-    
-    // Uninstall
-    public int UninstallOption;
-    
-    // All
-    public List<string> RemoveRequestMods;
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System.Collections.Generic; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Threading.Tasks; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 타입을 선언한다: public class GlobalSetting { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static GlobalSetting Instance = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string InstallPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Task&lt;ModData[]&gt; AdditionMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int AdofaiRevision = -1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool SelectSaved; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsUninstall; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 주석: Install | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool InstallUnityModManager; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool InstallDoorstop; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool InstallJalib; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool InstallJipperResourcePack; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public List&lt;ModData&gt; SelectedMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 주석: Uninstall | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int UninstallOption; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 주석: All | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public List&lt;string&gt; RemoveRequestMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Installer.csproj

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,119 +0,0 @@

```diff
@@ -1,119 +0,0 @@
-<?xml version="1.0" encoding="utf-8"?>
-<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
-    <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
-    <PropertyGroup>
-        <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
-        <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
-        <ProjectGuid>{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}</ProjectGuid>
-        <OutputType>WinExe</OutputType>
-        <RootNamespace>JipperResourcePack.Installer</RootNamespace>
-        <AssemblyName>JipperResourcePack Installer</AssemblyName>
-        <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>
-        <FileAlignment>512</FileAlignment>
-        <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
-        <Deterministic>true</Deterministic>
-        <LangVersion>14</LangVersion>
-        <ApplicationIcon>Resource\JipperProfile.ico</ApplicationIcon>
-    </PropertyGroup>
-    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
-        <PlatformTarget>AnyCPU</PlatformTarget>
-        <DebugSymbols>true</DebugSymbols>
-        <DebugType>full</DebugType>
-        <Optimize>false</Optimize>
-        <OutputPath>bin\Debug\</OutputPath>
-        <DefineConstants>DEBUG;TRACE</DefineConstants>
-        <ErrorReport>prompt</ErrorReport>
-        <WarningLevel>4</WarningLevel>
-    </PropertyGroup>
-    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
-        <OutputType>WinExe</OutputType>
-        <PlatformTarget>AnyCPU</PlatformTarget>
-        <DebugType>pdbonly</DebugType>
-        <Optimize>true</Optimize>
-        <OutputPath>bin\Release\</OutputPath>
-        <DefineConstants>TRACE</DefineConstants>
-        <ErrorReport>prompt</ErrorReport>
-        <WarningLevel>4</WarningLevel>
-    </PropertyGroup>
-    <ItemGroup>
-        <Reference Include="System" />
-        <Reference Include="System.Core" />
-        <Reference Include="System.IO.Compression" />
-        <Reference Include="System.Xml.Linq" />
-        <Reference Include="System.Data.DataSetExtensions" />
-        <Reference Include="Microsoft.CSharp" />
-        <Reference Include="System.Data" />
-        <Reference Include="System.Deployment" />
-        <Reference Include="System.Drawing" />
-        <Reference Include="System.Net.Http" />
-        <Reference Include="System.Windows.Forms" />
-        <Reference Include="System.Web.Extensions" />
-        <Reference Include="System.Xml" />
-    </ItemGroup>
-    <ItemGroup>
-        <Compile Include="Constants.cs" />
-        <Compile Include="GlobalSetting.cs" />
-        <Compile Include="InstallerForm.cs">
-            <SubType>Form</SubType>
-        </Compile>
-        <Compile Include="InstallerForm.Designer.cs">
-            <DependentUpon>InstallerForm.cs</DependentUpon>
-        </Compile>
-        <Compile Include="KakaoPayQrForm.cs" />
-        <Compile Include="LinkedImageButton.cs" />
-        <Compile Include="ModInfo.cs" />
-        <Compile Include="Program.cs" />
-        <Compile Include="Properties\AssemblyInfo.cs" />
-        <Compile Include="ModData.cs" />
-        <Compile Include="RequirementStatus.cs" />
-        <Compile Include="Resource\Resources.cs" />
-        <Compile Include="Screen\FinishScreen.cs" />
-        <Compile Include="Screen\InstallScreen.cs" />
-        <Compile Include="Screen\Screen.cs" />
-        <Compile Include="Screen\MainScreen.cs" />
-        <Compile Include="Screen\SelectLocationScreen.cs" />
-        <Compile Include="Screen\SelectScreen.cs" />
-        <Compile Include="Utility.cs" />
-        <None Include="packages.config" />
-        <None Include="Properties\Settings.settings">
-            <Generator>SettingsSingleFileGenerator</Generator>
-            <LastGenOutput>Settings.Designer.cs</LastGenOutput>
-        </None>
-        <Compile Include="Properties\Settings.Designer.cs">
-            <AutoGen>True</AutoGen>
-            <DependentUpon>Settings.settings</DependentUpon>
-            <DesignTimeSharedInput>True</DesignTimeSharedInput>
-        </Compile>
-    </ItemGroup>
-    <ItemGroup>
-        <None Include="App.config" />
-    </ItemGroup>
-    <ItemGroup>
-    </ItemGroup>
-    <ItemGroup>
-      <Content Include="mods.json" />
-      <Content Include="Resource\README.md" />
-      <EmbeddedResource Include="Resource\check.png" />
-      <EmbeddedResource Include="Resource\close.png" />
-      <EmbeddedResource Include="Resource\mail.png" />
-      <EmbeddedResource Include="Resource\warning.png" />
-      <EmbeddedResource Include="Resource\JipperResourceSide.png" />
-      <EmbeddedResource Include="Resource\JipperProfile.ico" />
-      <EmbeddedResource Include="Resource\KakaoPay_QR.png" />
-      <EmbeddedResource Include="Resource\kofi_logo.png" />
-      <EmbeddedResource Include="Resource\payment_icon_yellow_small.png" />
-      <EmbeddedResource Include="Resource\GitHub_Invertocat_Black.png" />
-      <EmbeddedResource Include="Resource\Discord-Symbol-Blurple.png" />
-      <EmbeddedResource Include="Resource\UMM\doorstop_config.ini" />
-      <EmbeddedResource Include="Resource\UMM\Config.xml" />
-    </ItemGroup>
-    <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
-    <Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild">
-      <PropertyGroup>
-        <ErrorText>This project references NuGet package(s) that are missing on this computer. Enable NuGet Package Restore to download them.  For more information, see http://go.microsoft.com/fwlink/?LinkID=322105.The missing file is {0}.</ErrorText>
-      </PropertyGroup>
-      <Error Condition="!Exists('..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets')" Text="$([System.String]::Format('$(ErrorText)', '..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets'))" />
-    </Target>
-    
-    <Import Project="..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets" Condition="Exists('..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets')" />
-</Project>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version="1.0" encoding="utf-8"?&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Configuration Condition=" '$(Configuration)' == '' "&gt;Debug&lt;/Configuration&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Platform Condition=" '$(Platform)' == '' "&gt;AnyCPU&lt;/Platform&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ProjectGuid&gt;{B1BD79F4-9B8C-4295-A2EF-5314ED412AC2}&lt;/ProjectGuid&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OutputType&gt;WinExe&lt;/OutputType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 프로젝트·리소스·설정 요소를 지정한다: &lt;RootNamespace&gt;JipperResourcePack.Installer&lt;/RootNamespace&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;AssemblyName&gt;JipperResourcePack Installer&lt;/AssemblyName&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 프로젝트·리소스·설정 요소를 지정한다: &lt;TargetFrameworkVersion&gt;v4.8.1&lt;/TargetFrameworkVersion&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 프로젝트·리소스·설정 요소를 지정한다: &lt;FileAlignment&gt;512&lt;/FileAlignment&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 프로젝트·리소스·설정 요소를 지정한다: &lt;AutoGenerateBindingRedirects&gt;true&lt;/AutoGenerateBindingRedirects&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Deterministic&gt;true&lt;/Deterministic&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 프로젝트·리소스·설정 요소를 지정한다: &lt;LangVersion&gt;14&lt;/LangVersion&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ApplicationIcon&gt;Resource\JipperProfile.ico&lt;/ApplicationIcon&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup Condition=" '$(Configuration)\|$(Platform)' == 'Debug\|AnyCPU' "&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PlatformTarget&gt;AnyCPU&lt;/PlatformTarget&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DebugSymbols&gt;true&lt;/DebugSymbols&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DebugType&gt;full&lt;/DebugType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Optimize&gt;false&lt;/Optimize&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OutputPath&gt;bin\Debug\&lt;/OutputPath&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DefineConstants&gt;DEBUG;TRACE&lt;/DefineConstants&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ErrorReport&gt;prompt&lt;/ErrorReport&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 프로젝트·리소스·설정 요소를 지정한다: &lt;WarningLevel&gt;4&lt;/WarningLevel&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup Condition=" '$(Configuration)\|$(Platform)' == 'Release\|AnyCPU' "&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OutputType&gt;WinExe&lt;/OutputType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PlatformTarget&gt;AnyCPU&lt;/PlatformTarget&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DebugType&gt;pdbonly&lt;/DebugType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Optimize&gt;true&lt;/Optimize&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 프로젝트·리소스·설정 요소를 지정한다: &lt;OutputPath&gt;bin\Release\&lt;/OutputPath&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DefineConstants&gt;TRACE&lt;/DefineConstants&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ErrorReport&gt;prompt&lt;/ErrorReport&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 프로젝트·리소스·설정 요소를 지정한다: &lt;WarningLevel&gt;4&lt;/WarningLevel&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Core" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.IO.Compression" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Xml.Linq" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Data.DataSetExtensions" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="Microsoft.CSharp" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Data" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Deployment" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Drawing" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Net.Http" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Windows.Forms" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Web.Extensions" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Reference Include="System.Xml" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Constants.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="GlobalSetting.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="InstallerForm.cs"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 프로젝트·리소스·설정 요소를 지정한다: &lt;SubType&gt;Form&lt;/SubType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Compile&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="InstallerForm.Designer.cs"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DependentUpon&gt;InstallerForm.cs&lt;/DependentUpon&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 61 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Compile&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="KakaoPayQrForm.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="LinkedImageButton.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="ModInfo.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Program.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Properties\AssemblyInfo.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="ModData.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="RequirementStatus.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Resource\Resources.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Screen\FinishScreen.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Screen\InstallScreen.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Screen\Screen.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Screen\MainScreen.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Screen\SelectLocationScreen.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Screen\SelectScreen.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Utility.cs" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 프로젝트·리소스·설정 요소를 지정한다: &lt;None Include="packages.config" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 프로젝트·리소스·설정 요소를 지정한다: &lt;None Include="Properties\Settings.settings"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Generator&gt;SettingsSingleFileGenerator&lt;/Generator&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 프로젝트·리소스·설정 요소를 지정한다: &lt;LastGenOutput&gt;Settings.Designer.cs&lt;/LastGenOutput&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/None&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Compile Include="Properties\Settings.Designer.cs"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 프로젝트·리소스·설정 요소를 지정한다: &lt;AutoGen&gt;True&lt;/AutoGen&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DependentUpon&gt;Settings.settings&lt;/DependentUpon&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 프로젝트·리소스·설정 요소를 지정한다: &lt;DesignTimeSharedInput&gt;True&lt;/DesignTimeSharedInput&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Compile&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 프로젝트·리소스·설정 요소를 지정한다: &lt;None Include="App.config" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Content Include="mods.json" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Content Include="Resource\README.md" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\check.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\close.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\mail.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\warning.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\JipperResourceSide.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\JipperProfile.ico" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\KakaoPay_QR.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\kofi_logo.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\payment_icon_yellow_small.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\GitHub_Invertocat_Black.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\Discord-Symbol-Blurple.png" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\UMM\doorstop_config.ini" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EmbeddedResource Include="Resource\UMM\Config.xml" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/ItemGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 프로젝트·리소스·설정 요소를 지정한다: &lt;PropertyGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ErrorText&gt;This project references NuGet package(s) that are missing on this computer. Enable NuGet Package Restore to download them.  For more information, see http://go.microsoft.com/fwlink/?LinkID=322105.The missing file is {0}.&lt;/ErrorText&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/PropertyGroup&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Error Condition="!Exists('..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets')" Text="$([System.String]::Format('$(ErrorText)', '..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets'))" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Target&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Import Project="..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets" Condition="Exists('..\packages\Microsoft.NETFramework.ReferenceAssemblies.net481.1.0.3\build\Microsoft.NETFramework.ReferenceAssemblies.net481.targets')" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Project&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Installer.csproj.DotSettings.user

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,2 +0,0 @@

```diff
@@ -1,2 +0,0 @@
-﻿<wpf:ResourceDictionary xml:space="preserve" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:s="clr-namespace:System;assembly=mscorlib" xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml" xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
-	<s:String x:Key="/Default/CodeEditing/Localization/MoveToResource/LastResourceFile/@EntryValue">B1BD79F4-9B8C-4295-A2EF-5314ED412AC2/d:Properties/f:Resources.resx</s:String></wpf:ResourceDictionary>
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;wpf:ResourceDictionary xml:space="preserve" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:s="clr-namespace:System;assembly=mscorlib" xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml" xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;s:String x:Key="/Default/CodeEditing/Localization/MoveToResource/LastResourceFile/@EntryValue"&gt;B1BD79F4-9B8C-4295-A2EF-5314ED412AC2/d:Properties/f:Resources.resx&lt;/s:String&gt;&lt;/wpf:ResourceDictionary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/InstallerForm.Designer.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,131 +0,0 @@

```diff
@@ -1,131 +0,0 @@
-﻿using JipperResourcePack.Installer.Properties;
-
-namespace JipperResourcePack.Installer {
-    partial class InstallerForm {
-        /// <summary>
-        /// Required designer variable.
-        /// </summary>
-        private System.ComponentModel.IContainer components = null;
-
-        /// <summary>
-        /// Clean up any resources being used.
-        /// </summary>
-        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
-        protected override void Dispose(bool disposing) {
-            if(disposing && (components != null)) {
-                components.Dispose();
-            }
-            base.Dispose(disposing);
-        }
-
-        #region Windows Form Designer generated code
-
-        /// <summary>
-        /// Required method for Designer support - do not modify
-        /// the contents of this method with the code editor.
-        /// </summary>
-        private void InitializeComponent() {
-            this.MainPanel = new System.Windows.Forms.Panel();
-            this.VersionLabel = new System.Windows.Forms.Label();
-            this.UnderPanel = new System.Windows.Forms.Panel();
-            this.Prev = new System.Windows.Forms.Button();
-            this.Next = new System.Windows.Forms.Button();
-            this.Cancel = new System.Windows.Forms.Button();
-            this.TopPanel = new System.Windows.Forms.Panel();
-            this.UnderPanel.SuspendLayout();
-            this.SuspendLayout();
-            // 
-            // MainPanel
-            // 
-            this.MainPanel.Location = new System.Drawing.Point(0, 0);
-            this.MainPanel.Size = new System.Drawing.Size(984, 561);
-            this.MainPanel.TabIndex = 0;
-            this.MainPanel.Name = "MainPanel";
-            // 
-            // VersionLabel
-            // 
-            this.VersionLabel.Location = new System.Drawing.Point(909, 547);
-            this.VersionLabel.Size = new System.Drawing.Size(75, 14);
-            this.VersionLabel.TabIndex = 0;
-            this.VersionLabel.Text = "Initializing...";
-            this.VersionLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
-            this.VersionLabel.ForeColor = System.Drawing.Color.FromArgb(((int) (((byte) (224)))), ((int) (((byte) (224)))), ((int) (((byte) (224)))));
-            this.VersionLabel.Name = "VersionLabel";
-            // 
-            // UnderPanel
-            // 
-            this.UnderPanel.BackColor = System.Drawing.SystemColors.ControlLight;
-            this.UnderPanel.Controls.Add(this.Prev);
-            this.UnderPanel.Controls.Add(this.Next);
-            this.UnderPanel.Controls.Add(this.Cancel);
-            this.UnderPanel.Location = new System.Drawing.Point(0, 561);
-            this.UnderPanel.Size = new System.Drawing.Size(984, 40);
-            this.UnderPanel.TabIndex = 1;
-            this.UnderPanel.Name = "UnderPanel";
-            // 
-            // Prev
-            // 
-            this.Prev.Location = new System.Drawing.Point(710, 5);
-            this.Prev.Size = new System.Drawing.Size(80, 30);
-            this.Prev.TabIndex = 2;
-            this.Prev.Name = "Prev";
-            this.Prev.UseVisualStyleBackColor = true;
-            // 
-            // Next
-            // 
-            this.Next.Location = new System.Drawing.Point(790, 5);
-            this.Next.Size = new System.Drawing.Size(80, 30);
-            this.Next.TabIndex = 1;
-            this.Next.Name = "Next";
-            this.Next.UseVisualStyleBackColor = true;
-            // 
-            // Cancel
-            // 
-            this.Cancel.Location = new System.Drawing.Point(890, 5);
-            this.Cancel.Size = new System.Drawing.Size(80, 30);
-            this.Cancel.TabIndex = 0;
-            this.Cancel.Name = "Cancel";
-            this.Cancel.UseVisualStyleBackColor = true;
-            // 
-            // TopPanel
-            // 
-            this.TopPanel.BackColor = System.Drawing.SystemColors.ControlLight;
-            this.TopPanel.Location = new System.Drawing.Point(0, 0);
-            this.TopPanel.Size = new System.Drawing.Size(984, 40);
-            this.TopPanel.TabIndex = 2;
-            this.TopPanel.Name = "TopPanel";
-            // 
-            // InstallerForm
-            // 
-            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
-            this.ClientSize = new System.Drawing.Size(984, 601);
-            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
-            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
-            this.BackColor = System.Drawing.SystemColors.Control;
-            this.Controls.Add(this.VersionLabel);
-            this.Controls.Add(this.TopPanel);
-            this.Controls.Add(this.UnderPanel);
-            this.Controls.Add(this.MainPanel);
-            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
-            this.MaximizeBox = false;
-            this.Name = "InstallerForm";
-            this.UnderPanel.ResumeLayout(false);
-            this.ResumeLayout(false);
-        }
-        private System.Windows.Forms.Label VersionLabel;
-
-        private System.Windows.Forms.Panel TopPanel;
-
-        private System.Windows.Forms.Button Prev;
-
-        private System.Windows.Forms.Button Next;
-
-        private System.Windows.Forms.Button Cancel;
-
-        private System.Windows.Forms.Panel MainPanel;
-        private System.Windows.Forms.Panel UnderPanel;
-
-        #endregion
-
-    }
-}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Properties; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 연결된 식·선언·설정의 일부다: partial class InstallerForm { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 주석: / &lt;summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 주석: / Required designer variable. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 주석: / &lt;/summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.ComponentModel.IContainer components = null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 주석: / &lt;summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 주석: / Clean up any resources being used. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 주석: / &lt;/summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 주석: / &lt;param name="disposing"&gt;true if managed resources should be disposed; otherwise, false.&lt;/param&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 함수/생성자의 이름·입력·반환 형식을 선언한다: protected override void Dispose(bool disposing) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 조건을 만족할 때 뒤의 작업을 수행한다: if(disposing &amp;&amp; (components != null)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 이 함수 호출/식 처리를 실행한다: components.Dispose(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 이 함수 호출/식 처리를 실행한다: base.Dispose(disposing); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 설정 주석/전처리 지시/스크립트 경계다: #region Windows Form Designer generated code | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 주석: / &lt;summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 주석: / Required method for Designer support - do not modify | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 주석: / the contents of this method with the code editor. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 주석: / &lt;/summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void InitializeComponent() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.MainPanel = new System.Windows.Forms.Panel(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel = new System.Windows.Forms.Label(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.UnderPanel = new System.Windows.Forms.Panel(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Prev = new System.Windows.Forms.Button(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Next = new System.Windows.Forms.Button(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Cancel = new System.Windows.Forms.Button(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.TopPanel = new System.Windows.Forms.Panel(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 이 함수 호출/식 처리를 실행한다: this.UnderPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 이 함수 호출/식 처리를 실행한다: this.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 주석: MainPanel | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.MainPanel.Location = new System.Drawing.Point(0, 0); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.MainPanel.Size = new System.Drawing.Size(984, 561); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.MainPanel.TabIndex = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.MainPanel.Name = "MainPanel"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 주석: VersionLabel | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.Location = new System.Drawing.Point(909, 547); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.Size = new System.Drawing.Size(75, 14); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.TabIndex = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.Text = "Initializing..."; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.TextAlign = System.Drawing.ContentAlignment.TopRight; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.ForeColor = System.Drawing.Color.FromArgb(((int) (((byte) (224)))), ((int) (((byte) (224)))), ((int) (((byte) (224))))); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.VersionLabel.Name = "VersionLabel"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 주석: UnderPanel | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.UnderPanel.BackColor = System.Drawing.SystemColors.ControlLight; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 이 함수 호출/식 처리를 실행한다: this.UnderPanel.Controls.Add(this.Prev); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 이 함수 호출/식 처리를 실행한다: this.UnderPanel.Controls.Add(this.Next); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 이 함수 호출/식 처리를 실행한다: this.UnderPanel.Controls.Add(this.Cancel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.UnderPanel.Location = new System.Drawing.Point(0, 561); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.UnderPanel.Size = new System.Drawing.Size(984, 40); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.UnderPanel.TabIndex = 1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.UnderPanel.Name = "UnderPanel"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 주석: Prev | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Prev.Location = new System.Drawing.Point(710, 5); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Prev.Size = new System.Drawing.Size(80, 30); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Prev.TabIndex = 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Prev.Name = "Prev"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Prev.UseVisualStyleBackColor = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 주석: Next | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Next.Location = new System.Drawing.Point(790, 5); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Next.Size = new System.Drawing.Size(80, 30); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Next.TabIndex = 1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Next.Name = "Next"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Next.UseVisualStyleBackColor = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 주석: Cancel | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Cancel.Location = new System.Drawing.Point(890, 5); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Cancel.Size = new System.Drawing.Size(80, 30); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Cancel.TabIndex = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Cancel.Name = "Cancel"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Cancel.UseVisualStyleBackColor = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 주석: TopPanel | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.TopPanel.BackColor = System.Drawing.SystemColors.ControlLight; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.TopPanel.Location = new System.Drawing.Point(0, 0); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.TopPanel.Size = new System.Drawing.Size(984, 40); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.TopPanel.TabIndex = 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.TopPanel.Name = "TopPanel"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 주석: InstallerForm | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.ClientSize = new System.Drawing.Size(984, 601); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.BackColor = System.Drawing.SystemColors.Control; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 이 함수 호출/식 처리를 실행한다: this.Controls.Add(this.VersionLabel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 이 함수 호출/식 처리를 실행한다: this.Controls.Add(this.TopPanel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 이 함수 호출/식 처리를 실행한다: this.Controls.Add(this.UnderPanel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 이 함수 호출/식 처리를 실행한다: this.Controls.Add(this.MainPanel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.MaximizeBox = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 오른쪽 결과를 왼쪽 상태에 저장한다: this.Name = "InstallerForm"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 이 함수 호출/식 처리를 실행한다: this.UnderPanel.ResumeLayout(false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 이 함수 호출/식 처리를 실행한다: this.ResumeLayout(false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Label VersionLabel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Panel TopPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Button Prev; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 121 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Button Next; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Button Cancel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Panel MainPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private System.Windows.Forms.Panel UnderPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 설정 주석/전처리 지시/스크립트 경계다: #endregion | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/InstallerForm.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,74 +0,0 @@

```diff
@@ -1,74 +0,0 @@
-﻿using System;
-using System.Drawing;
-using System.Net.Http;
-using System.Reflection;
-using System.Threading.Tasks;
-using System.Windows.Forms;
-using System.Web.Script.Serialization;
-using JipperResourcePack.Installer.Resource;
-using JipperResourcePack.Installer.Screen;
-
-namespace JipperResourcePack.Installer;
-
-public partial class InstallerForm : Form {
-    public static HttpClient HttpClient;
-
-    public InstallerForm() {
-        InitializeComponent();
-        Load += InstallerForm_Load;
-    }
-
-    private void InstallerForm_Load(object obj, EventArgs args) {
-        ResetText();
-        SetupScreenData();
-        new MainScreen().Enter();
-        HttpClient = new HttpClient();
-        GlobalSetting.Instance.AdditionMods = GetMods();
-    }
-
-
-    public override void ResetText() {
-        Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.JipperProfile.ico")!);
-        Text = Resources.Current.Title;
-        Cancel.Text = Resources.Current.Cancel;
-        Prev.Text = Resources.Current.Previous;
-        Next.Text = Resources.Current.Next;
-        VersionLabel.Text = Application.ProductVersion;
-        TopPanel.Controls.Clear();
-        Label oldLabel = null;
-        Label[] labels = new Label[4];
-        for(int i = 0; i < 7; i++) {
-            Label label = new() {
-                Text = i switch {
-                    0 => Resources.Current.Title1,
-                    2 => Resources.Current.Title2,
-                    4 => Resources.Current.Title3,
-                    6 => Resources.Current.Title4,
-                    _ => "→"
-                },
-                AutoSize = true,
-                Font = Constants.Arial16,
-                Location = oldLabel == null ? new Point(32, 9) : new Point(oldLabel.Location.X + oldLabel.Size.Width, 9)
-            };
-            if((i & 1) == 0) labels[i >> 1] = label;
-            TopPanel.Controls.Add(label);
-            oldLabel = label;
-        }
-        Screen.Screen.TopPanelLabels = labels;
-    }
-
-    public void SetupScreenData() {
-        Screen.Screen.MainPanel = MainPanel;
-        Screen.Screen.TopPanel = TopPanel;
-        Screen.Screen.UnderPanel = UnderPanel;
-        Screen.Screen.PrevButton = Prev;
-        Screen.Screen.NextButton = Next;
-        Screen.Screen.CancelButton = Cancel;
-        Screen.Screen.SetupButton();
-    }
-
-    public async Task<ModData[]> GetMods() {
-        string responseData = await HttpClient.GetStringAsync("https://github.com/Jongye0l/JipperResourcePack/raw/main/Installer/mods.json");
-        return Utility.JsonSerializer.Deserialize<ModData[]>(responseData);
-    }
-}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Net.Http; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: System.Threading.Tasks; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | namespace 또는 타입 별칭을 가져온다: System.Web.Script.Serialization; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 타입을 선언한다: public partial class InstallerForm : Form { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static HttpClient HttpClient; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public InstallerForm() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 이 함수 호출/식 처리를 실행한다: InitializeComponent(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 이 함수 호출/식 처리를 실행한다: Load += InstallerForm_Load; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void InstallerForm_Load(object obj, EventArgs args) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 이 함수 호출/식 처리를 실행한다: ResetText(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 이 함수 호출/식 처리를 실행한다: SetupScreenData(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 이 함수 호출/식 처리를 실행한다: new MainScreen().Enter(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpClient = new HttpClient(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.AdditionMods = GetMods(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void ResetText() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.JipperProfile.ico")!); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.Title; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cancel.Text = Resources.Current.Cancel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: Prev.Text = Resources.Current.Previous; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 오른쪽 결과를 왼쪽 상태에 저장한다: Next.Text = Resources.Current.Next; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 오른쪽 결과를 왼쪽 상태에 저장한다: VersionLabel.Text = Application.ProductVersion; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 이 함수 호출/식 처리를 실행한다: TopPanel.Controls.Clear(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label oldLabel = null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label[] labels = new Label[4]; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; 7; i++) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label label = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = i switch { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 연결된 식·선언·설정의 일부다: 0 =&gt; Resources.Current.Title1, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 연결된 식·선언·설정의 일부다: 2 =&gt; Resources.Current.Title2, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 연결된 식·선언·설정의 일부다: 4 =&gt; Resources.Current.Title3, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 연결된 식·선언·설정의 일부다: 6 =&gt; Resources.Current.Title4, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 연결된 식·선언·설정의 일부다: _ =&gt; "→" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial16, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = oldLabel == null ? new Point(32, 9) : new Point(oldLabel.Location.X + oldLabel.Size.Width, 9) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 조건을 만족할 때 뒤의 작업을 수행한다: if((i &amp; 1) == 0) labels[i &gt;&gt; 1] = label; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 이 함수 호출/식 처리를 실행한다: TopPanel.Controls.Add(label); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 오른쪽 결과를 왼쪽 상태에 저장한다: oldLabel = label; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.TopPanelLabels = labels; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void SetupScreenData() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.MainPanel = MainPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.TopPanel = TopPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.UnderPanel = UnderPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.PrevButton = Prev; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.NextButton = Next; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen.Screen.CancelButton = Cancel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 이 함수 호출/식 처리를 실행한다: Screen.Screen.SetupButton(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async Task&lt;ModData[]&gt; GetMods() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 오른쪽 결과를 왼쪽 상태에 저장한다: string responseData = await HttpClient.GetStringAsync("https://github.com/Jongye0l/JipperResourcePack/raw/main/Installer/mods.json"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 함수를 종료하거나 식의 결과를 반환한다: return Utility.JsonSerializer.Deserialize&lt;ModData[]&gt;(responseData); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/InstallerForm.resx

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,291 +0,0 @@

```diff
@@ -1,291 +0,0 @@
-﻿<?xml version="1.0" encoding="utf-8"?>
-<root>
-  <!-- 
-    Microsoft ResX Schema 
-    
-    Version 2.0
-    
-    The primary goals of this format is to allow a simple XML format 
-    that is mostly human readable. The generation and parsing of the 
-    various data types are done through the TypeConverter classes 
-    associated with the data types.
-    
-    Example:
-    
-    ... ado.net/XML headers & schema ...
-    <resheader name="resmimetype">text/microsoft-resx</resheader>
-    <resheader name="version">2.0</resheader>
-    <resheader name="reader">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
-    <resheader name="writer">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
-    <data name="Name1"><value>this is my long string</value><comment>this is a comment</comment></data>
-    <data name="Color1" type="System.Drawing.Color, System.Drawing">Blue</data>
-    <data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64">
-        <value>[base64 mime encoded serialized .NET Framework object]</value>
-    </data>
-    <data name="Icon1" type="System.Drawing.Icon, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64">
-        <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>
-        <comment>This is a comment</comment>
-    </data>
-                
-    There are any number of "resheader" rows that contain simple 
-    name/value pairs.
-    
-    Each data row contains a name, and value. The row also contains a 
-    type or mimetype. Type corresponds to a .NET class that support 
-    text/value conversion through the TypeConverter architecture. 
-    Classes that don't support this are serialized and stored with the 
-    mimetype set.
-    
-    The mimetype is used for serialized objects, and tells the 
-    ResXResourceReader how to depersist the object. This is currently not 
-    extensible. For a given mimetype the value must be set accordingly:
-    
-    Note - application/x-microsoft.net.object.binary.base64 is the format 
-    that the ResXResourceWriter will generate, however the reader can 
-    read any of the formats listed below.
-    
-    mimetype: application/x-microsoft.net.object.binary.base64
-    value   : The object must be serialized with 
-            : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
-            : and then encoded with base64 encoding.
-    
-    mimetype: application/x-microsoft.net.object.soap.base64
-    value   : The object must be serialized with 
-            : System.Runtime.Serialization.Formatters.Soap.SoapFormatter
-            : and then encoded with base64 encoding.
-
-    mimetype: application/x-microsoft.net.object.bytearray.base64
-    value   : The object must be serialized into a byte array 
-            : using a System.ComponentModel.TypeConverter
-            : and then encoded with base64 encoding.
-    -->
-  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
-    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
-    <xsd:element name="root" msdata:IsDataSet="true">
-      <xsd:complexType>
-        <xsd:choice maxOccurs="unbounded">
-          <xsd:element name="metadata">
-            <xsd:complexType>
-              <xsd:sequence>
-                <xsd:element name="value" type="xsd:string" minOccurs="0" />
-              </xsd:sequence>
-              <xsd:attribute name="name" use="required" type="xsd:string" />
-              <xsd:attribute name="type" type="xsd:string" />
-              <xsd:attribute name="mimetype" type="xsd:string" />
-              <xsd:attribute ref="xml:space" />
-            </xsd:complexType>
-          </xsd:element>
-          <xsd:element name="assembly">
-            <xsd:complexType>
-              <xsd:attribute name="alias" type="xsd:string" />
-              <xsd:attribute name="name" type="xsd:string" />
-            </xsd:complexType>
-          </xsd:element>
-          <xsd:element name="data">
-            <xsd:complexType>
-              <xsd:sequence>
-                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
-                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
-              </xsd:sequence>
-              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
-              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
-              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
-              <xsd:attribute ref="xml:space" />
-            </xsd:complexType>
-          </xsd:element>
-          <xsd:element name="resheader">
-            <xsd:complexType>
-              <xsd:sequence>
-                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
-              </xsd:sequence>
-              <xsd:attribute name="name" type="xsd:string" use="required" />
-            </xsd:complexType>
-          </xsd:element>
-        </xsd:choice>
-      </xsd:complexType>
-    </xsd:element>
-  </xsd:schema>
-  <resheader name="resmimetype">
-    <value>text/microsoft-resx</value>
-  </resheader>
-  <resheader name="version">
-    <value>2.0</value>
-  </resheader>
-  <resheader name="reader">
-    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </resheader>
-  <resheader name="writer">
-    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </resheader>
-  <assembly alias="System.Drawing" name="System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
-  <data name="VersionLabel.Location" type="System.Drawing.Point, System.Drawing">
-    <value>909, 547</value>
-  </data>
-  <data name="VersionLabel.Size" type="System.Drawing.Size, System.Drawing">
-    <value>75, 14</value>
-  </data>
-  <assembly alias="mscorlib" name="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" />
-  <data name="VersionLabel.TabIndex" type="System.Int32, mscorlib">
-    <value>0</value>
-  </data>
-  <data name="VersionLabel.Text" xml:space="preserve">
-    <value>Initializing...</value>
-  </data>
-  <data name="VersionLabel.TextAlign" type="System.Drawing.ContentAlignment, System.Drawing">
-    <value>TopRight</value>
-  </data>
-  <data name="&gt;&gt;VersionLabel.Name" xml:space="preserve">
-    <value>VersionLabel</value>
-  </data>
-  <data name="&gt;&gt;VersionLabel.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Label, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;VersionLabel.ZOrder" xml:space="preserve">
-    <value>0</value>
-  </data>
-  <data name="MainPanel.Location" type="System.Drawing.Point, System.Drawing">
-    <value>0, 0</value>
-  </data>
-  <data name="MainPanel.Size" type="System.Drawing.Size, System.Drawing">
-    <value>984, 561</value>
-  </data>
-  <data name="MainPanel.TabIndex" type="System.Int32, mscorlib">
-    <value>0</value>
-  </data>
-  <data name="&gt;&gt;MainPanel.Name" xml:space="preserve">
-    <value>MainPanel</value>
-  </data>
-  <data name="&gt;&gt;MainPanel.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Panel, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;MainPanel.Parent" xml:space="preserve">
-    <value>$this</value>
-  </data>
-  <data name="&gt;&gt;MainPanel.ZOrder" xml:space="preserve">
-    <value>2</value>
-  </data>
-  <data name="Prev.Location" type="System.Drawing.Point, System.Drawing">
-    <value>710, 5</value>
-  </data>
-  <data name="Prev.Size" type="System.Drawing.Size, System.Drawing">
-    <value>80, 30</value>
-  </data>
-  <data name="Prev.TabIndex" type="System.Int32, mscorlib">
-    <value>2</value>
-  </data>
-  <data name="&gt;&gt;Prev.Name" xml:space="preserve">
-    <value>Prev</value>
-  </data>
-  <data name="&gt;&gt;Prev.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Button, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;Prev.Parent" xml:space="preserve">
-    <value>UnderPanel</value>
-  </data>
-  <data name="&gt;&gt;Prev.ZOrder" xml:space="preserve">
-    <value>0</value>
-  </data>
-  <data name="Next.Location" type="System.Drawing.Point, System.Drawing">
-    <value>790, 5</value>
-  </data>
-  <data name="Next.Size" type="System.Drawing.Size, System.Drawing">
-    <value>80, 30</value>
-  </data>
-  <data name="Next.TabIndex" type="System.Int32, mscorlib">
-    <value>1</value>
-  </data>
-  <data name="&gt;&gt;Next.Name" xml:space="preserve">
-    <value>Next</value>
-  </data>
-  <data name="&gt;&gt;Next.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Button, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;Next.Parent" xml:space="preserve">
-    <value>UnderPanel</value>
-  </data>
-  <data name="&gt;&gt;Next.ZOrder" xml:space="preserve">
-    <value>1</value>
-  </data>
-  <data name="Cancel.Location" type="System.Drawing.Point, System.Drawing">
-    <value>890, 5</value>
-  </data>
-  <data name="Cancel.Size" type="System.Drawing.Size, System.Drawing">
-    <value>80, 30</value>
-  </data>
-  <data name="Cancel.TabIndex" type="System.Int32, mscorlib">
-    <value>0</value>
-  </data>
-  <data name="&gt;&gt;Cancel.Name" xml:space="preserve">
-    <value>Cancel</value>
-  </data>
-  <data name="&gt;&gt;Cancel.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Button, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;Cancel.Parent" xml:space="preserve">
-    <value>UnderPanel</value>
-  </data>
-  <data name="&gt;&gt;Cancel.ZOrder" xml:space="preserve">
-    <value>2</value>
-  </data>
-  <data name="UnderPanel.Location" type="System.Drawing.Point, System.Drawing">
-    <value>0, 561</value>
-  </data>
-  <data name="UnderPanel.Size" type="System.Drawing.Size, System.Drawing">
-    <value>984, 40</value>
-  </data>
-  <data name="UnderPanel.TabIndex" type="System.Int32, mscorlib">
-    <value>1</value>
-  </data>
-  <data name="&gt;&gt;UnderPanel.Name" xml:space="preserve">
-    <value>UnderPanel</value>
-  </data>
-  <data name="&gt;&gt;UnderPanel.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Panel, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;UnderPanel.Parent" xml:space="preserve">
-    <value>$this</value>
-  </data>
-  <data name="&gt;&gt;UnderPanel.ZOrder" xml:space="preserve">
-    <value>1</value>
-  </data>
-  <data name="TopPanel.Location" type="System.Drawing.Point, System.Drawing">
-    <value>0, 0</value>
-  </data>
-  <data name="TopPanel.Size" type="System.Drawing.Size, System.Drawing">
-    <value>984, 40</value>
-  </data>
-  <data name="TopPanel.TabIndex" type="System.Int32, mscorlib">
-    <value>2</value>
-  </data>
-  <data name="&gt;&gt;TopPanel.Name" xml:space="preserve">
-    <value>TopPanel</value>
-  </data>
-  <data name="&gt;&gt;TopPanel.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Panel, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-  <data name="&gt;&gt;TopPanel.Parent" xml:space="preserve">
-    <value>$this</value>
-  </data>
-  <data name="&gt;&gt;TopPanel.ZOrder" xml:space="preserve">
-    <value>0</value>
-  </data>
-  <metadata name="$this.Localizable" type="System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">
-    <value>True</value>
-  </metadata>
-  <data name="$this.AutoScaleDimensions" type="System.Drawing.SizeF, System.Drawing">
-    <value>7, 12</value>
-  </data>
-  <data name="$this.ClientSize" type="System.Drawing.Size, System.Drawing">
-    <value>984, 601</value>
-  </data>
-  <assembly alias="System.Windows.Forms" name="System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" />
-  <data name="$this.StartPosition" type="System.Windows.Forms.FormStartPosition, System.Windows.Forms">
-    <value>CenterScreen</value>
-  </data>
-  <data name="&gt;&gt;$this.Name" xml:space="preserve">
-    <value>InstallerForm</value>
-  </data>
-  <data name="&gt;&gt;$this.Type" xml:space="preserve">
-    <value>System.Windows.Forms.Form, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
-  </data>
-</root>
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version="1.0" encoding="utf-8"?&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;root&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;!-- | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 연결된 식·선언·설정의 일부다: Microsoft ResX Schema | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 연결된 식·선언·설정의 일부다: Version 2.0 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 연결된 식·선언·설정의 일부다: The primary goals of this format is to allow a simple XML format | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 연결된 식·선언·설정의 일부다: that is mostly human readable. The generation and parsing of the | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 연결된 식·선언·설정의 일부다: various data types are done through the TypeConverter classes | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 연결된 식·선언·설정의 일부다: associated with the data types. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 연결된 식·선언·설정의 일부다: Example: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 연결된 식·선언·설정의 일부다: ... ado.net/XML headers &amp; schema ... | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="resmimetype"&gt;text/microsoft-resx&lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="version"&gt;2.0&lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="reader"&gt;System.Resources.ResXResourceReader, System.Windows.Forms, ...&lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="writer"&gt;System.Resources.ResXResourceWriter, System.Windows.Forms, ...&lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Name1"&gt;&lt;value&gt;this is my long string&lt;/value&gt;&lt;comment&gt;this is a comment&lt;/comment&gt;&lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Color1" type="System.Drawing.Color, System.Drawing"&gt;Blue&lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;[base64 mime encoded serialized .NET Framework object]&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Icon1" type="System.Drawing.Icon, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;[base64 mime encoded string representing a byte array form of the .NET Framework object]&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 프로젝트·리소스·설정 요소를 지정한다: &lt;comment&gt;This is a comment&lt;/comment&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 연결된 식·선언·설정의 일부다: There are any number of "resheader" rows that contain simple | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 연결된 식·선언·설정의 일부다: name/value pairs. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 연결된 식·선언·설정의 일부다: Each data row contains a name, and value. The row also contains a | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 연결된 식·선언·설정의 일부다: type or mimetype. Type corresponds to a .NET class that support | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 연결된 식·선언·설정의 일부다: text/value conversion through the TypeConverter architecture. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 연결된 식·선언·설정의 일부다: Classes that don't support this are serialized and stored with the | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 연결된 식·선언·설정의 일부다: mimetype set. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 연결된 식·선언·설정의 일부다: The mimetype is used for serialized objects, and tells the | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 연결된 식·선언·설정의 일부다: ResXResourceReader how to depersist the object. This is currently not | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 연결된 식·선언·설정의 일부다: extensible. For a given mimetype the value must be set accordingly: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 연결된 식·선언·설정의 일부다: Note - application/x-microsoft.net.object.binary.base64 is the format | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 연결된 식·선언·설정의 일부다: that the ResXResourceWriter will generate, however the reader can | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 연결된 식·선언·설정의 일부다: read any of the formats listed below. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 연결된 식·선언·설정의 일부다: mimetype: application/x-microsoft.net.object.binary.base64 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 연결된 식·선언·설정의 일부다: value   : The object must be serialized with | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 연결된 식·선언·설정의 일부다: : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 연결된 식·선언·설정의 일부다: : and then encoded with base64 encoding. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 연결된 식·선언·설정의 일부다: mimetype: application/x-microsoft.net.object.soap.base64 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 연결된 식·선언·설정의 일부다: value   : The object must be serialized with | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 연결된 식·선언·설정의 일부다: : System.Runtime.Serialization.Formatters.Soap.SoapFormatter | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 연결된 식·선언·설정의 일부다: : and then encoded with base64 encoding. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 연결된 식·선언·설정의 일부다: mimetype: application/x-microsoft.net.object.bytearray.base64 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 연결된 식·선언·설정의 일부다: value   : The object must be serialized into a byte array | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 연결된 식·선언·설정의 일부다: : using a System.ComponentModel.TypeConverter | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 연결된 식·선언·설정의 일부다: : and then encoded with base64 encoding. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 61 | 연결된 식·선언·설정의 일부다: --&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:import namespace="http://www.w3.org/XML/1998/namespace" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="root" msdata:IsDataSet="true"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:choice maxOccurs="unbounded"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="metadata"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:sequence&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="value" type="xsd:string" minOccurs="0" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:sequence&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="name" use="required" type="xsd:string" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="type" type="xsd:string" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="mimetype" type="xsd:string" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute ref="xml:space" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:element&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="assembly"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="alias" type="xsd:string" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="name" type="xsd:string" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:element&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="data"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:sequence&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:sequence&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute ref="xml:space" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:element&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="resheader"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:sequence&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:sequence&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 프로젝트·리소스·설정 요소를 지정한다: &lt;xsd:attribute name="name" type="xsd:string" use="required" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:element&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:choice&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:complexType&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:element&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/xsd:schema&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="resmimetype"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;text/microsoft-resx&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="version"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;2.0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="reader"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 프로젝트·리소스·설정 요소를 지정한다: &lt;resheader name="writer"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/resheader&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 프로젝트·리소스·설정 요소를 지정한다: &lt;assembly alias="System.Drawing" name="System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 121 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="VersionLabel.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;909, 547&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="VersionLabel.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;75, 14&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 프로젝트·리소스·설정 요소를 지정한다: &lt;assembly alias="mscorlib" name="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="VersionLabel.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="VersionLabel.Text" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 132 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;Initializing...&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 133 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 134 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="VersionLabel.TextAlign" type="System.Drawing.ContentAlignment, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 135 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;TopRight&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 136 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 137 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;VersionLabel.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 138 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;VersionLabel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 139 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 140 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;VersionLabel.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 141 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Label, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 142 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 143 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;VersionLabel.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 144 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 145 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 146 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="MainPanel.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 147 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0, 0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 148 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 149 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="MainPanel.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 150 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;984, 561&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 151 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 152 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="MainPanel.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 153 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 154 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 155 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;MainPanel.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 156 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;MainPanel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 157 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 158 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;MainPanel.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 159 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Panel, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 160 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 161 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;MainPanel.Parent" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 162 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;$this&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 163 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 164 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;MainPanel.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 165 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;2&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 166 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 167 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Prev.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 168 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;710, 5&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 169 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 170 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Prev.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 171 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;80, 30&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 172 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 173 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Prev.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 174 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;2&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 175 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 176 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Prev.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 177 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;Prev&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 178 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 179 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Prev.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 180 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Button, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 181 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 182 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Prev.Parent" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 183 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;UnderPanel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 184 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 185 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Prev.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 186 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 187 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 188 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Next.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 189 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;790, 5&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 190 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 191 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Next.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 192 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;80, 30&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 193 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 194 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Next.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 195 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;1&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 196 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 197 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Next.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 198 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;Next&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 199 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 200 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Next.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 201 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Button, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 202 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 203 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Next.Parent" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 204 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;UnderPanel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 205 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 206 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Next.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 207 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;1&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 208 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 209 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Cancel.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 210 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;890, 5&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 211 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 212 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Cancel.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 213 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;80, 30&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 214 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 215 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="Cancel.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 216 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 217 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 218 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Cancel.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 219 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;Cancel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 220 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 221 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Cancel.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 222 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Button, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 223 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 224 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Cancel.Parent" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 225 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;UnderPanel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 226 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 227 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;Cancel.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 228 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;2&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 229 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 230 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="UnderPanel.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 231 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0, 561&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 232 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 233 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="UnderPanel.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 234 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;984, 40&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 235 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 236 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="UnderPanel.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 237 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;1&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 238 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 239 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;UnderPanel.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 240 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;UnderPanel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 241 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 242 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;UnderPanel.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 243 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Panel, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 244 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 245 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;UnderPanel.Parent" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 246 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;$this&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 247 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 248 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;UnderPanel.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 249 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;1&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 250 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 251 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="TopPanel.Location" type="System.Drawing.Point, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 252 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0, 0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 253 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 254 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="TopPanel.Size" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 255 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;984, 40&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 256 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 257 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="TopPanel.TabIndex" type="System.Int32, mscorlib"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 258 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;2&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 259 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 260 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;TopPanel.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 261 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;TopPanel&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 262 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 263 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;TopPanel.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 264 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Panel, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 265 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 266 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;TopPanel.Parent" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 267 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;$this&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 268 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 269 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;TopPanel.ZOrder" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 270 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;0&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 271 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 272 | 프로젝트·리소스·설정 요소를 지정한다: &lt;metadata name="$this.Localizable" type="System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 273 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;True&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 274 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/metadata&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 275 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="$this.AutoScaleDimensions" type="System.Drawing.SizeF, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 276 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;7, 12&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 277 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 278 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="$this.ClientSize" type="System.Drawing.Size, System.Drawing"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 279 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;984, 601&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 280 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 281 | 프로젝트·리소스·설정 요소를 지정한다: &lt;assembly alias="System.Windows.Forms" name="System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 282 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="$this.StartPosition" type="System.Windows.Forms.FormStartPosition, System.Windows.Forms"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 283 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;CenterScreen&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 284 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 285 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;$this.Name" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 286 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;InstallerForm&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 287 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 288 | 프로젝트·리소스·설정 요소를 지정한다: &lt;data name="&amp;gt;&amp;gt;$this.Type" xml:space="preserve"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 289 | 프로젝트·리소스·설정 요소를 지정한다: &lt;value&gt;System.Windows.Forms.Form, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089&lt;/value&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 290 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/data&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 291 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/root&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/KakaoPayQrForm.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,27 +0,0 @@

```diff
@@ -1,27 +0,0 @@
-﻿using System.Drawing;
-using System.Reflection;
-using System.Windows.Forms;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer;
-
-public class KakaoPayQrForm : Form {
-    private const int ImageWidth = 540;
-    private const int ImageHeight = 693;
-        
-    public KakaoPayQrForm() {
-        int widthOffset = Width - ClientSize.Width;
-        int heightOffset = Height - ClientSize.Height;
-        PictureBox qrPictureBox = new() {
-            Location = new Point(0, 0),
-            Size = new Size(ImageWidth, ImageHeight),
-            Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.KakaoPay_QR.png")!),
-        };
-        Size = new Size(ImageWidth + widthOffset, ImageHeight + heightOffset);
-        Controls.Add(qrPictureBox);
-        FormBorderStyle = FormBorderStyle.FixedToolWindow;
-        MaximizeBox = false;
-        Name = nameof(KakaoPayQrForm);
-        Text = Resources.Current.MainScreen_KakaoPayQrForm;
-    }
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 타입을 선언한다: public class KakaoPayQrForm : Form { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private const int ImageWidth = 540; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private const int ImageHeight = 693; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public KakaoPayQrForm() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: int widthOffset = Width - ClientSize.Width; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 오른쪽 결과를 왼쪽 상태에 저장한다: int heightOffset = Height - ClientSize.Height; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 오른쪽 결과를 왼쪽 상태에 저장한다: PictureBox qrPictureBox = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(ImageWidth, ImageHeight), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 오른쪽 결과를 왼쪽 상태에 저장한다: Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.KakaoPay_QR.png")!), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(ImageWidth + widthOffset, ImageHeight + heightOffset); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 이 함수 호출/식 처리를 실행한다: Controls.Add(qrPictureBox); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 오른쪽 결과를 왼쪽 상태에 저장한다: FormBorderStyle = FormBorderStyle.FixedToolWindow; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: MaximizeBox = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 오른쪽 결과를 왼쪽 상태에 저장한다: Name = nameof(KakaoPayQrForm); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_KakaoPayQrForm; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/LinkedImageButton.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,36 +0,0 @@

```diff
@@ -1,36 +0,0 @@
-﻿using System;
-using System.Drawing;
-using System.Reflection;
-using System.Windows.Forms;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer;
-
-public class LinkedImageButton : PictureBox {
-    public string Url;
-    
-    public LinkedImageButton(string url, string imageUrl) {
-        Url = url;
-        Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(imageUrl)!);
-        Cursor = Cursors.Hand;
-        Click += OnClick;
-    }
-    
-    private void OnClick(object sender, EventArgs e) {
-        if(Url == null) {
-            using KakaoPayQrForm form = new();
-            form.ShowDialog();
-        } else {
-            try {
-                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
-                    FileName = Url,
-                    UseShellExecute = true
-                }); 
-            } catch (Exception) {
-                MessageBox.Show(Resources.Current.MainScreen_OpenBrowserFailed, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-                // copy clipboard url
-                Clipboard.SetText(Url);
-            }
-        }
-    }
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 타입을 선언한다: public class LinkedImageButton : PictureBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Url; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public LinkedImageButton(string url, string imageUrl) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: Url = url; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 오른쪽 결과를 왼쪽 상태에 저장한다: Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(imageUrl)!); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cursor = Cursors.Hand; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 이 함수 호출/식 처리를 실행한다: Click += OnClick; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void OnClick(object sender, EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Url == null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | namespace 또는 타입 별칭을 가져온다: KakaoPayQrForm form = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 이 함수 호출/식 처리를 실행한다: form.ShowDialog(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 연결된 식·선언·설정의 일부다: System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 오른쪽 결과를 왼쪽 상태에 저장한다: FileName = Url, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 오른쪽 결과를 왼쪽 상태에 저장한다: UseShellExecute = true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | callback/호출 본문을 닫고 등록 또는 호출을 마친다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(Resources.Current.MainScreen_OpenBrowserFailed, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 주석: copy clipboard url | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 이 함수 호출/식 처리를 실행한다: Clipboard.SetText(Url); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/ModData.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,7 +0,0 @@

```diff
@@ -1,7 +0,0 @@
-﻿namespace JipperResourcePack.Installer;
-
-public class ModData {
-    public string Name;
-    public string URL;
-    public string DisplayName;
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 타입을 선언한다: public class ModData { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Name; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string URL; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string DisplayName; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/ModInfo.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,6 +0,0 @@

```diff
@@ -1,6 +0,0 @@
-﻿namespace JipperResourcePack.Installer;
-
-public class ModInfo {
-    public string Id;
-    public string Author;
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 타입을 선언한다: public class ModInfo { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Id; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Author; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Program.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,20 +0,0 @@

```diff
@@ -1,20 +0,0 @@
-﻿using System;
-using System.Threading;
-using System.Windows.Forms;
-
-namespace JipperResourcePack.Installer;
-
-static class Program {
-    /// <summary>
-    /// The main entry point for the application.
-    /// </summary>
-    [STAThread]
-    static void Main() {
-        using Mutex instanceMutex = new(false, @"Local\JipperResourcePack.Installer", out bool createdNew);
-        if(!createdNew) return;
-
-        Application.EnableVisualStyles();
-        Application.SetCompatibleTextRenderingDefault(false);
-        Application.Run(new InstallerForm());
-    }
-}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Threading; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 연결된 식·선언·설정의 일부다: static class Program { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 주석: / &lt;summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 주석: / The main entry point for the application. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 주석: / &lt;/summary&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 메타데이터/compiler 지시를 지정한다: [STAThread] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 연결된 식·선언·설정의 일부다: static void Main() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | namespace 또는 타입 별칭을 가져온다: Mutex instanceMutex = new(false, @"Local\JipperResourcePack.Installer", out bool createdNew); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!createdNew) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 이 함수 호출/식 처리를 실행한다: Application.EnableVisualStyles(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 이 함수 호출/식 처리를 실행한다: Application.SetCompatibleTextRenderingDefault(false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 이 함수 호출/식 처리를 실행한다: Application.Run(new InstallerForm()); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Properties/AssemblyInfo.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,36 +0,0 @@

```diff
@@ -1,36 +0,0 @@
-﻿using System.Reflection;
-using System.Runtime.CompilerServices;
-using System.Runtime.InteropServices;
-
-// General Information about an assembly is controlled through the following
-// set of attributes. Change these attribute values to modify the information
-// associated with an assembly.
-[assembly: AssemblyTitle("JipperResourcePack Installer")]
-[assembly: AssemblyDescription("JipperResourcePack Auto Installer")]
-[assembly: AssemblyConfiguration("")]
-[assembly: AssemblyCompany("")]
-[assembly: AssemblyProduct("JipperResourcePack Installer")]
-[assembly: AssemblyCopyright("Copyright ©Jongyeol 2024")]
-[assembly: AssemblyTrademark("")]
-[assembly: AssemblyCulture("")]
-
-// Setting ComVisible to false makes the types in this assembly not visible
-// to COM components.  If you need to access a type in this assembly from
-// COM, set the ComVisible attribute to true on that type.
-[assembly: ComVisible(false)]
-
-// The following GUID is for the ID of the typelib if this project is exposed to COM
-[assembly: Guid("B1BD79F4-9B8C-4295-A2EF-5314ED412AC2")]
-
-// Version information for an assembly consists of the following four values:
-//
-//      Major Version
-//      Minor Version
-//      Build Number
-//      Revision
-//
-// You can specify all the values or you can default the Build and Revision Numbers
-// by using the '*' as shown below:
-// [assembly: AssemblyVersion("1.0.*")]
-[assembly: AssemblyVersion("2.0.2.0")]
-[assembly: AssemblyFileVersion("2.0.2.0")]
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Runtime.CompilerServices; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Runtime.InteropServices; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 주석: General Information about an assembly is controlled through the following | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 주석: set of attributes. Change these attribute values to modify the information | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 주석: associated with an assembly. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyTitle("JipperResourcePack Installer")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyDescription("JipperResourcePack Auto Installer")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyConfiguration("")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyCompany("")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyProduct("JipperResourcePack Installer")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyCopyright("Copyright ©Jongyeol 2024")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyTrademark("")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyCulture("")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 주석: Setting ComVisible to false makes the types in this assembly not visible | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 주석: to COM components.  If you need to access a type in this assembly from | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 주석: COM, set the ComVisible attribute to true on that type. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 메타데이터/compiler 지시를 지정한다: [assembly: ComVisible(false)] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 주석: The following GUID is for the ID of the typelib if this project is exposed to COM | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 메타데이터/compiler 지시를 지정한다: [assembly: Guid("B1BD79F4-9B8C-4295-A2EF-5314ED412AC2")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 주석: Version information for an assembly consists of the following four values: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 주석:      Major Version | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 주석:      Minor Version | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 주석:      Build Number | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 주석:      Revision | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 주석: You can specify all the values or you can default the Build and Revision Numbers | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 주석: by using the '*' as shown below: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 주석: [assembly: AssemblyVersion("1.0.*")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyVersion("2.0.2.0")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 메타데이터/compiler 지시를 지정한다: [assembly: AssemblyFileVersion("2.0.2.0")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Properties/Settings.Designer.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,25 +0,0 @@

```diff
@@ -1,25 +0,0 @@
-﻿//------------------------------------------------------------------------------
-// <auto-generated>
-//     This code was generated by a tool.
-//     Runtime Version:4.0.30319.42000
-//
-//     Changes to this file may cause incorrect behavior and will be lost if
-//     the code is regenerated.
-// </auto-generated>
-//------------------------------------------------------------------------------
-
-namespace JipperResourcePack.Installer.Properties {
-
-
-    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
-    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "11.0.0.0")]
-    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
-
-        private static Settings defaultInstance = ((Settings) (global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
-
-        public static Settings Default
-        {
-            get { return defaultInstance; }
-        }
-    }
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 주석: ------------------------------------------------------------------------------ | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 주석: &lt;auto-generated&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 주석:     This code was generated by a tool. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 주석:     Runtime Version:4.0.30319.42000 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 주석:  | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 주석:     Changes to this file may cause incorrect behavior and will be lost if | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 주석:     the code is regenerated. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 주석: &lt;/auto-generated&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 주석: ------------------------------------------------------------------------------ | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Properties { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 메타데이터/compiler 지시를 지정한다: [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 메타데이터/compiler 지시를 지정한다: [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "11.0.0.0")] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 타입을 선언한다: internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private static Settings defaultInstance = ((Settings) (global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings()))); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 연결된 식·선언·설정의 일부다: public static Settings Default | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 현재 블록/타입의 시작이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 연결된 식·선언·설정의 일부다: get { return defaultInstance; } | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Properties/Settings.settings

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,7 +0,0 @@

```diff
@@ -1,7 +0,0 @@
-﻿<?xml version='1.0' encoding='utf-8'?>
-<SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)">
-  <Profiles>
-    <Profile Name="(Default)" />
-  </Profiles>
-  <Settings />
-</SettingsFile>
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version='1.0' encoding='utf-8'?&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Profiles&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Profile Name="(Default)" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Profiles&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Settings /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/SettingsFile&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/RequirementStatus.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,14 +0,0 @@

```diff
@@ -1,14 +0,0 @@
-﻿namespace JipperResourcePack.Installer;
-
-public class RequirementStatus {
-    public bool IsExistUnityModManager;
-    public bool IsExistJALib;
-    public bool IsExistJipperResourcePack;
-    public bool IsAssemblyInstalled;
-    public bool IsOldDoorStop;
-    public bool ExistMods;
-
-    public void Reset() {
-        IsExistUnityModManager = IsExistJALib = IsExistJipperResourcePack = IsAssemblyInstalled = IsOldDoorStop = ExistMods = false;
-    }
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 타입을 선언한다: public class RequirementStatus { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsExistUnityModManager; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsExistJALib; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsExistJipperResourcePack; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsAssemblyInstalled; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool IsOldDoorStop; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool ExistMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void Reset() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 오른쪽 결과를 왼쪽 상태에 저장한다: IsExistUnityModManager = IsExistJALib = IsExistJipperResourcePack = IsAssemblyInstalled = IsOldDoorStop = ExistMods = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Resource/Discord-Symbol-Blurple.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 816 bytes
- 삭제 전 SHA-256: 720f03290a1b6c61dcb8141730ebc8aaa06c668febd3be69b3a49a07f2a7cac1
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/GitHub_Invertocat_Black.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 575 bytes
- 삭제 전 SHA-256: 3735792381b0ca62aadf943ae3fd29c5787c732da3d96f7ceed5a32a171d27da
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/JipperProfile.ico

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 105662 bytes
- 삭제 전 SHA-256: f71c2b2c89fa64b10d935d7a590a7d0eb2c03f2c9fb04c5932080a08fa787f98
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/JipperResourceSide.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 64527 bytes
- 삭제 전 SHA-256: 7cb39b9968aca60a73d2c8ab7028cdfc519b511d0e2861728296c3f3f8b7e493
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/KakaoPay_QR.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 46147 bytes
- 삭제 전 SHA-256: 16fd25ab20e974ff335d7a41e0efb2ea1d1e666993fb1cff231a5a0bb8309abc
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/README.md

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,14 +0,0 @@

```diff
@@ -1,14 +0,0 @@
-﻿# Source of Resource Files
-* JipperResourceSide.png : Requested by **Jipper** Created by **KJDM**
-* JipperProfile.ico : [Jipper's Youtube Post](https://www.youtube.com/post/UgkxTZJ9kJSaZzqWALQNSMX9Pgd1F8C2n7I5) Created by **precioness**
-* UMM/Config.xml : [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
-* UMM/doorstop_config.ini : [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)
-* Discord-Symbol-Blurple.png : [Discord Branding Guidelines](https://discord.com/branding)
-* GitHub_Invertocat_Black.png : [Github Brand Toolkit Logo](https://brand.github.com/foundations/logo)
-* mail.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:mail)
-* payment_icon_yellow_small.png : [KakaoPay Partner Center](https://partner.kakaopay.com/partner/online/guide/payment-method)
-* kofi_logo.png : [Ko-fi Brand Assets](https://more.ko-fi.com/brand-assets)
-* KakaoPay_QR.png : KakaoPay(KakaoTalk) QR Code
-* check.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:check)
-* close.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:close)
-* warning.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:warning)
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 설정 주석/전처리 지시/스크립트 경계다: # Source of Resource Files | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 연결된 식·선언·설정의 일부다: * JipperResourceSide.png : Requested by **Jipper** Created by **KJDM** | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 연결된 식·선언·설정의 일부다: * JipperProfile.ico : [Jipper's Youtube Post](https://www.youtube.com/post/UgkxTZJ9kJSaZzqWALQNSMX9Pgd1F8C2n7I5) Created by **precioness** | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 연결된 식·선언·설정의 일부다: * UMM/Config.xml : [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 연결된 식·선언·설정의 일부다: * UMM/doorstop_config.ini : [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 연결된 식·선언·설정의 일부다: * Discord-Symbol-Blurple.png : [Discord Branding Guidelines](https://discord.com/branding) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 연결된 식·선언·설정의 일부다: * GitHub_Invertocat_Black.png : [Github Brand Toolkit Logo](https://brand.github.com/foundations/logo) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 연결된 식·선언·설정의 일부다: * mail.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:mail) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 연결된 식·선언·설정의 일부다: * payment_icon_yellow_small.png : [KakaoPay Partner Center](https://partner.kakaopay.com/partner/online/guide/payment-method) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 연결된 식·선언·설정의 일부다: * kofi_logo.png : [Ko-fi Brand Assets](https://more.ko-fi.com/brand-assets) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 연결된 식·선언·설정의 일부다: * KakaoPay_QR.png : KakaoPay(KakaoTalk) QR Code | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 연결된 식·선언·설정의 일부다: * check.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:check) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 연결된 식·선언·설정의 일부다: * close.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:close) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 연결된 식·선언·설정의 일부다: * warning.png : [Google Material Symbols](https://fonts.google.com/icons?selected=Material+Symbols+Outlined:warning) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Resource/Resources.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,228 +0,0 @@

```diff
@@ -1,228 +0,0 @@
-﻿using System;
-using System.Globalization;
-
-namespace JipperResourcePack.Installer.Resource;
-
-public class Resources {
-    public static readonly Resources English = new() {
-        Title = "Jipper ResourcePack Installer",
-        Cancel = "Cancel",
-        Previous = "< Previous",
-        Next = "Next >",
-        MainScreen_Title = "Starting the installation of the Jipper Resource Pack.",
-        MainScreen_Description1 = "Installs the Jipper Resource Pack to A Dance of Fire and Ice.",
-        MainScreen_Description2 = "Internet access to Github is required for installation.",
-        MainScreen_Description3 = "Required system(UMM, JALib) may be automatically installed.",
-        MainScreen_Description4 = "The Jipper Resource Pack only supports the latest older, default, beta, and alpha branches. It may not work on other versions.",
-        MainScreen_Description5 = "Proceeding with the installation will force close A Dance of Fire and Ice.",
-        MainScreen_Description6 = "This program is a community mod, not an official program by 7th Beat Games.",
-        MainScreen_Description7 = "Click the 'Next' button to continue.",
-        MainScreen_BugReport = "Bug Report / Inquiry",
-        MainScreen_Donate = "Donate",
-        MainScreen_KakaoPayQrForm = "Jipper Resource Pack Donation KakaoPay QR",
-        MainScreen_OpenBrowserFailed = "Failed to open the browser. The URL has been copied to the clipboard.",
-        MainScreen_NoInternet = "Not connected to the internet.",
-        Warn = "Warning",
-        Error = "Error",
-        Title1 = "Select Folder",
-        Title2 = "Select Components",
-        Title3 = "Install",
-        Title4 = "Finish",
-        SelectLocation_Title = "Please select the A Dance of Fire and Ice folder.",
-        SelectLocation_Description = "Install the Jipper ResourcePack in the ADOFAI.\nIf you install it in a different ADOFAI folder or if the program has not found the ADOFAI folder,\nclick the 'Select Folder' button to select the ADOFAI folder.",
-        SelectLocation_Select = "Browse",
-        SelectLocation_Location = "Select ADOFAI Folder",
-        // SelectLocation_AdofaiFolderGuide = "A Dance of Fire and Ice Folder Guide",
-        SelectLocation_NoLocation = "Location has not been set.",
-        SelectLocation_NoFolder = "Folder does not exist.",
-        SelectLocation_NoAdofai = "The selected folder is not an A Dance of Fire and Ice folder.",
-        SelectLocation_NotifyTitle = "Path Analysis",
-        SelectLocation_NotifyEmpty = "Folder location is not set",
-        SelectLocation_NotifyFolderNotExist = "Folder does not exist",
-        SelectLocation_NotifyFolderExist = "Folder exists",
-        SelectLocation_NotifyGameNotFound = "Game executable or data could not be found",
-        SelectLocation_NotifyGameFound = "Game found",
-        SelectLocation_NotifyUmmIsAssembly = "UMM is installed in Assembly format",
-        SelectLocation_NotifyUmmIsAssembly2 = "It must be reinstalled every time A Dance of Fire and Ice is updated.",
-        SelectLocation_NotifyDoorstopIsOld = "Doorstop is an outdated version.",
-        SelectLocation_NotifyDoorstopIsOld2 = "Crashes will occur from r141 onwards, making it unusable.",
-        Select_Title = "Please select the components to install.",
-        Select_Work = "Task",
-        Select_Work_Install = "Install",
-        Select_Work_Uninstall = "Uninstall",
-        Select_Requirement = "Install Required Components",
-        Select_Requirement_UnityModManager = "Unity Mod Manager is required to use the Jipper Resource Pack.\nIt cannot be excluded because Unity Mod Manager is not installed.",
-        Select_Requirement_JALib = "JALib is required to use the Jipper Resource Pack.\nIt cannot be excluded because JALib is not installed.",
-        Select_Requirement_Doorstop_Umm = "Doorstop is required to use Unity Mod Manager.\nIt cannot be excluded since Unity Mod Manager is selected.",
-        Select_Requirement_Doorstop_Asm = "Unity Mod Manager is currently installed via the Assembly method.\nThe Assembly method requires a reinstallation every time the game updates. Do you wish to continue?",
-        Select_Requirement_Doorstop_Old = "The Doorstop version currently in use is outdated.\nFrom r141 onwards, crashes will occur if Doorstop is not version 5.4.0 or higher. Do you wish to continue?",
-        Select_Requirement_JipperResourcePack = "The Jipper Resource Pack cannot be excluded because it is not installed.",
-        Select_AdditionMods = "Install Additional Mods",
-        Select_RemoveMods = "Remove Existing Mods",
-        Select_RemoveMods_Confirm = "Are you sure you want to remove the selected mods?\nRemoved mods cannot be recovered, and data may not be preserved even if reinstalled.",
-        Select_UninstallOption = "Uninstall Options",
-        Select_UninstallOption_All = "Full Uninstall",
-        Select_UninstallOption_AllDescription = "Completely removes the mod manager and all mods. You will need to reinstall all mods to use them again.",
-        Select_UninstallOption_OnlyManager = "Remove Mod Manager Only",
-        Select_UninstallOption_OnlyManagerDescription = "Removes only the mod manager while keeping the mod data. You can use the mods again just by reinstalling the mod manager.",
-        Select_UninstallOption_OnlyMod = "Remove Specific Mods Only",
-        Select_UninstallOption_OnlyModDescription = "Removes only specific mods instead of erasing all of them. The remaining mods will continue to work.",
-        FinishScreen_Finish = "Exit",
-        FinishScreen_Title_Install = "Installation Complete",
-        FinishScreen_Title_Uninstall = "Uninstallation Complete",
-        FinishScreen_Description_Install = "The Jipper ResourcePack installation is complete.\nClick the 'Exit' button to close the program.",
-        FinishScreen_Description_Uninstall = "The Jipper Resource Pack has been successfully removed.\nClick the 'Exit' button to close the program.",
-        FinishScreen_RunAdofai = "Launch A Dance of Fire and Ice",
-        FinishScreen_Title_Error = "Failed to install the Jipper Resource Pack.",
-        FinishScreen_Description_Error = "An error occurred during the Jipper Resource Pack installation.\nClick the 'View Log' button to check the error details.",
-        FinishScreen_CheckLog = "View Log"
-    };
-
-    public static readonly Resources Korean = new() {
-        Title = "지퍼리소스팩 딸깍 설치기",
-        Cancel = "취소",
-        Previous = "< 이전",
-        Next = "다음 >",
-        MainScreen_Title = "지퍼 리소스팩 설치를 시작합니다.",
-        MainScreen_Description1 = "불과 얼음의 춤에 지퍼 리소스팩을 설치합니다.",
-        MainScreen_Description2 = "설치를 위해 Github에 접속할 수 있는 환경이 필요합니다.",
-        MainScreen_Description3 = "UnityModManager와 JALib등 필요한 시스템이 자동으로 설치될 수 있습니다.",
-        MainScreen_Description4 = "지퍼 리소스팩은 최신 older, default, beta, alpha 브랜치만 지원하며\n그 외 버전에서는 작동하지 않을 수 있습니다.",
-        MainScreen_Description5 = "설치 작업을 진행하면 불과 얼음의 춤이 강제 종료됩니다.",
-        MainScreen_Description6 = "이 프로그램은 7th Beat Games 공식 프로그램이 아닌 커뮤니티 모드입니다.",
-        MainScreen_Description7 = "계속 하시려면 '다음' 버튼을 눌러주세요.",
-        MainScreen_BugReport = "버그/문의",
-        MainScreen_Donate = "후원",
-        MainScreen_KakaoPayQrForm = "지퍼 리소스팩 후원 카카오페이 QR",
-        MainScreen_OpenBrowserFailed = "브라우저를 여는 데 실패했습니다. 클립보드로 URL이 복사되었습니다.",
-        MainScreen_NoInternet = "인터넷에 연결되어 있지 않습니다.",
-        Warn = "경고",
-        Error = "오류",
-        Title1 = "폴더 선택",
-        Title2 = "설치 요소 선택",
-        Title3 = "설치",
-        Title4 = "완료",
-        SelectLocation_Title = "얼불춤의 폴더를 선택해주세요.",
-        SelectLocation_Description = "지퍼 리소스팩을 얼불춤에 설치합니다.\n다른 얼불춤 폴더에 설치하거나 얼불춤 폴더를 프로그램이 찾지 못했다면 '찾아보기' 버튼을 눌러 얼불춤 폴더를 선택해주세요.",
-        SelectLocation_Select = "찾아보기",
-        SelectLocation_Location = "얼불춤 폴더 선택",
-        // SelectLocation_AdofaiFolderGuide = "얼불춤 폴더 가이드",
-        SelectLocation_NoLocation = "위치가 설정되지 않았습니다.",
-        SelectLocation_NoFolder = "폴더가 존재하지 않습니다.",
-        SelectLocation_NoAdofai = "선택된 폴더는 얼불춤 폴더가 아닙니다.",
-        SelectLocation_NotifyTitle = "경로 분석",
-        SelectLocation_NotifyEmpty = "폴더 위치가 설정되있지 않습니다",
-        SelectLocation_NotifyFolderNotExist = "폴더가 존재하지 않습니다",
-        SelectLocation_NotifyFolderExist = "폴더가 존재합니다",
-        SelectLocation_NotifyGameNotFound = "게임 프로그램이나 데이터를 찾을 수 없습니다",
-        SelectLocation_NotifyGameFound = "게임을 찾았습니다",
-        SelectLocation_NotifyUmmIsAssembly = "UMM이 Assembly 형식으로 설치되었습니다",
-        SelectLocation_NotifyUmmIsAssembly2 = "얼불춤을 업데이트 할 때 마다 재설치 해야 합니다.",
-        SelectLocation_NotifyDoorstopIsOld = "Doorstop이 구버전입니다.",
-        SelectLocation_NotifyDoorstopIsOld2 = "r141부터 크래시가 발생하며 사용할 수 없습니다.",
-        Select_Title = "설치 요소를 선택해주세요.",
-        Select_Work = "작업",
-        Select_Work_Install = "설치",
-        Select_Work_Uninstall = "제거",
-        Select_Requirement = "필수 요소 설치",
-        Select_Requirement_UnityModManager = "지퍼 리소스팩을 사용하기 위해서는 유니티 모드 매니저가 필요합니다.\n유니티 모드 매니저가 설치되어 있지 않아 제외할 수 없습니다.",
-        Select_Requirement_JALib = "지퍼 리소스팩을 사용하기 위해서는 JALib이 필요합니다.\nJALib이 설치되어 있지 않아 제외할 수 없습니다.",
-        Select_Requirement_Doorstop_Umm = "유니티 모드 매니저를 사용하기 위해서는 Doorstop이 필요합니다.\n유니티 모드 매니저가 선택되어 제외할 수 없습니다.",
-        Select_Requirement_Doorstop_Asm = "현재 유니티 모드 매니저가 Assembly 방식으로 설치되어 있습니다.\nAssembly 방식은 게임을 업데이트 할 때 마다 새로 설치해야 합니다. 계속 하시겠습니까?",
-        Select_Requirement_Doorstop_Old = "현재 사용중인 Doorstop이 구버전 입니다.\nr141부터는 5.4.0이상에 Doorstop이 아닐 경우 크래시가 발생합니다. 계속 하시겠습니까?",
-        Select_Requirement_JipperResourcePack = "지퍼 리소스팩이 설치되어 있지 않아 제외할 수 없습니다.",
-        Select_AdditionMods = "추가 모드 설치",
-        Select_RemoveMods = "기존 모드 제거",
-        Select_RemoveMods_Confirm = "선택한 모드를 정말 제거하시겠습니까?\n제거된 모드는 복구할 수 없으며 다시 설치해도 데이터가 유지되지 않을 수 있습니다.",
-        Select_UninstallOption = "제거 옵션",
-        Select_UninstallOption_All = "전체 제거",
-        Select_UninstallOption_AllDescription = "모드 매니저와 모든 모드를 완전히 제거합니다. 모드를 다시 사용하기 위해서는 모든 모드를 다시 설치해야 합니다.",
-        Select_UninstallOption_OnlyManager = "모드 매니저만 제거",
-        Select_UninstallOption_OnlyManagerDescription = "모드 데이터는 남겨둔 채로 모드 매니저만 제거합니다. 모드 매니저를 다시 설치하면 그대로 모드를 사용할 수 있습니다.",
-        Select_UninstallOption_OnlyMod = "특정 모드만 제거",
-        Select_UninstallOption_OnlyModDescription = "모든 모드를 지우지 않고 특정 모드만 제거합니다. 나머지 모드는 계속해서 사용할 수 있습니다.",
-        FinishScreen_Finish = "마침",
-        FinishScreen_Title_Install = "지퍼 리소스팩 설치 완료",
-        FinishScreen_Title_Uninstall = "지퍼 리소스팩 제거 완료",
-        FinishScreen_Description_Install = "지퍼 리소스팩 설치가 완료되었습니다.\n프로그램을 마치려면 '마침' 버튼을 눌러주세요.",
-        FinishScreen_Description_Uninstall = "지퍼 리소스팩 제거가 완료되었습니다.\n프로그램을 마치려면 '마침' 버튼을 눌러주세요.",
-        FinishScreen_RunAdofai = "얼불춤 실행",
-        FinishScreen_Title_Error = "지퍼 리소스팩을 설치하지 못했습니다.",
-        FinishScreen_Description_Error = "지퍼 리소스팩 설치중 오류가 발생했습니다.\n자세한 오류를 확인하기 위해서는 '로그 보기' 버튼을 눌러주세요.",
-        FinishScreen_CheckLog = "로그 보기"
-    };
-
-    public static Resources Current = CultureInfo.CurrentCulture.Name == "ko-KR" ? Korean : English;
-
-    public string Title;
-    public string Cancel;
-    public string Previous;
-    public string Next;
-    public string MainScreen_Title;
-    public string MainScreen_Description1;
-    public string MainScreen_Description2;
-    public string MainScreen_Description3;
-    public string MainScreen_Description4;
-    public string MainScreen_Description5;
-    public string MainScreen_Description6;
-    public string MainScreen_Description7;
-    public string MainScreen_BugReport;
-    public string MainScreen_Donate;
-    public string MainScreen_OpenBrowserFailed;
-    public string MainScreen_KakaoPayQrForm;
-    public string MainScreen_NoInternet;
-    public string Warn;
-    public string Error;
-    public string Title1;
-    public string Title2;
-    public string Title3;
-    public string Title4;
-    public string SelectLocation_Title;
-    public string SelectLocation_Description;
-    public string SelectLocation_Select;
-    public string SelectLocation_Location;
-    // public string SelectLocation_AdofaiFolderGuide;
-    public string SelectLocation_NoLocation;
-    public string SelectLocation_NoFolder;
-    public string SelectLocation_NoAdofai;
-    public string SelectLocation_NotifyTitle;
-    public string SelectLocation_NotifyEmpty;
-    public string SelectLocation_NotifyFolderNotExist;
-    public string SelectLocation_NotifyFolderExist;
-    public string SelectLocation_NotifyGameNotFound;
-    public string SelectLocation_NotifyGameFound;
-    public string SelectLocation_NotifyUmmIsAssembly;
-    public string SelectLocation_NotifyUmmIsAssembly2;
-    public string SelectLocation_NotifyDoorstopIsOld;
-    public string SelectLocation_NotifyDoorstopIsOld2;
-    public string Select_Title;
-    public string Select_Work;
-    public string Select_Work_Install;
-    public string Select_Work_Uninstall;
-    public string Select_Requirement;
-    public string Select_Requirement_UnityModManager;
-    public string Select_Requirement_JALib;
-    public string Select_Requirement_Doorstop_Umm;
-    public string Select_Requirement_Doorstop_Asm;
-    public string Select_Requirement_Doorstop_Old;
-    public string Select_Requirement_JipperResourcePack;
-    public string Select_AdditionMods;
-    public string Select_RemoveMods;
-    public string Select_RemoveMods_Confirm;
-    public string Select_UninstallOption;
-    public string Select_UninstallOption_All;
-    public string Select_UninstallOption_AllDescription;
-    public string Select_UninstallOption_OnlyManager;
-    public string Select_UninstallOption_OnlyManagerDescription;
-    public string Select_UninstallOption_OnlyMod;
-    public string Select_UninstallOption_OnlyModDescription;
-    public string FinishScreen_Finish;
-    public string FinishScreen_Title_Install;
-    public string FinishScreen_Title_Uninstall;
-    public string FinishScreen_Description_Install;
-    public string FinishScreen_Description_Uninstall;
-    public string FinishScreen_RunAdofai;
-    public string FinishScreen_Title_Error;
-    public string FinishScreen_Description_Error;
-    public string FinishScreen_CheckLog;
-}
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Globalization; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 타입을 선언한다: public class Resources { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static readonly Resources English = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title = "Jipper ResourcePack Installer", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cancel = "Cancel", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 오른쪽 결과를 왼쪽 상태에 저장한다: Previous = "&lt; Previous", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 오른쪽 결과를 왼쪽 상태에 저장한다: Next = "Next &gt;", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Title = "Starting the installation of the Jipper Resource Pack.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description1 = "Installs the Jipper Resource Pack to A Dance of Fire and Ice.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description2 = "Internet access to Github is required for installation.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description3 = "Required system(UMM, JALib) may be automatically installed.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description4 = "The Jipper Resource Pack only supports the latest older, default, beta, and alpha branches. It may not work on other versions.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description5 = "Proceeding with the installation will force close A Dance of Fire and Ice.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description6 = "This program is a community mod, not an official program by 7th Beat Games.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description7 = "Click the 'Next' button to continue.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_BugReport = "Bug Report / Inquiry", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Donate = "Donate", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_KakaoPayQrForm = "Jipper Resource Pack Donation KakaoPay QR", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_OpenBrowserFailed = "Failed to open the browser. The URL has been copied to the clipboard.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_NoInternet = "Not connected to the internet.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: Warn = "Warning", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 오른쪽 결과를 왼쪽 상태에 저장한다: Error = "Error", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title1 = "Select Folder", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title2 = "Select Components", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title3 = "Install", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title4 = "Finish", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Title = "Please select the A Dance of Fire and Ice folder.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Description = "Install the Jipper ResourcePack in the ADOFAI.\nIf you install it in a different ADOFAI folder or if the program has not found the ADOFAI folder,\nclick the 'Select Folder' button to select the ADOFAI folder.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Select = "Browse", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Location = "Select ADOFAI Folder", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 주석: SelectLocation_AdofaiFolderGuide = "A Dance of Fire and Ice Folder Guide", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NoLocation = "Location has not been set.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NoFolder = "Folder does not exist.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NoAdofai = "The selected folder is not an A Dance of Fire and Ice folder.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyTitle = "Path Analysis", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyEmpty = "Folder location is not set", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyFolderNotExist = "Folder does not exist", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyFolderExist = "Folder exists", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyGameNotFound = "Game executable or data could not be found", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyGameFound = "Game found", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyUmmIsAssembly = "UMM is installed in Assembly format", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyUmmIsAssembly2 = "It must be reinstalled every time A Dance of Fire and Ice is updated.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyDoorstopIsOld = "Doorstop is an outdated version.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyDoorstopIsOld2 = "Crashes will occur from r141 onwards, making it unusable.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Title = "Please select the components to install.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Work = "Task", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Work_Install = "Install", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Work_Uninstall = "Uninstall", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement = "Install Required Components", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_UnityModManager = "Unity Mod Manager is required to use the Jipper Resource Pack.\nIt cannot be excluded because Unity Mod Manager is not installed.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_JALib = "JALib is required to use the Jipper Resource Pack.\nIt cannot be excluded because JALib is not installed.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_Doorstop_Umm = "Doorstop is required to use Unity Mod Manager.\nIt cannot be excluded since Unity Mod Manager is selected.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_Doorstop_Asm = "Unity Mod Manager is currently installed via the Assembly method.\nThe Assembly method requires a reinstallation every time the game updates. Do you wish to continue?", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_Doorstop_Old = "The Doorstop version currently in use is outdated.\nFrom r141 onwards, crashes will occur if Doorstop is not version 5.4.0 or higher. Do you wish to continue?", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_JipperResourcePack = "The Jipper Resource Pack cannot be excluded because it is not installed.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_AdditionMods = "Install Additional Mods", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_RemoveMods = "Remove Existing Mods", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_RemoveMods_Confirm = "Are you sure you want to remove the selected mods?\nRemoved mods cannot be recovered, and data may not be preserved even if reinstalled.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption = "Uninstall Options", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_All = "Full Uninstall", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_AllDescription = "Completely removes the mod manager and all mods. You will need to reinstall all mods to use them again.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyManager = "Remove Mod Manager Only", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyManagerDescription = "Removes only the mod manager while keeping the mod data. You can use the mods again just by reinstalling the mod manager.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyMod = "Remove Specific Mods Only", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyModDescription = "Removes only specific mods instead of erasing all of them. The remaining mods will continue to work.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Finish = "Exit", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Title_Install = "Installation Complete", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Title_Uninstall = "Uninstallation Complete", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Description_Install = "The Jipper ResourcePack installation is complete.\nClick the 'Exit' button to close the program.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Description_Uninstall = "The Jipper Resource Pack has been successfully removed.\nClick the 'Exit' button to close the program.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_RunAdofai = "Launch A Dance of Fire and Ice", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Title_Error = "Failed to install the Jipper Resource Pack.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Description_Error = "An error occurred during the Jipper Resource Pack installation.\nClick the 'View Log' button to check the error details.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_CheckLog = "View Log" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static readonly Resources Korean = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title = "지퍼리소스팩 딸깍 설치기", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cancel = "취소", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 오른쪽 결과를 왼쪽 상태에 저장한다: Previous = "&lt; 이전", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 오른쪽 결과를 왼쪽 상태에 저장한다: Next = "다음 &gt;", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Title = "지퍼 리소스팩 설치를 시작합니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description1 = "불과 얼음의 춤에 지퍼 리소스팩을 설치합니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description2 = "설치를 위해 Github에 접속할 수 있는 환경이 필요합니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description3 = "UnityModManager와 JALib등 필요한 시스템이 자동으로 설치될 수 있습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description4 = "지퍼 리소스팩은 최신 older, default, beta, alpha 브랜치만 지원하며\n그 외 버전에서는 작동하지 않을 수 있습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description5 = "설치 작업을 진행하면 불과 얼음의 춤이 강제 종료됩니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description6 = "이 프로그램은 7th Beat Games 공식 프로그램이 아닌 커뮤니티 모드입니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Description7 = "계속 하시려면 '다음' 버튼을 눌러주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_BugReport = "버그/문의", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_Donate = "후원", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_KakaoPayQrForm = "지퍼 리소스팩 후원 카카오페이 QR", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_OpenBrowserFailed = "브라우저를 여는 데 실패했습니다. 클립보드로 URL이 복사되었습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 오른쪽 결과를 왼쪽 상태에 저장한다: MainScreen_NoInternet = "인터넷에 연결되어 있지 않습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 오른쪽 결과를 왼쪽 상태에 저장한다: Warn = "경고", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 오른쪽 결과를 왼쪽 상태에 저장한다: Error = "오류", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title1 = "폴더 선택", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title2 = "설치 요소 선택", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title3 = "설치", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 오른쪽 결과를 왼쪽 상태에 저장한다: Title4 = "완료", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Title = "얼불춤의 폴더를 선택해주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Description = "지퍼 리소스팩을 얼불춤에 설치합니다.\n다른 얼불춤 폴더에 설치하거나 얼불춤 폴더를 프로그램이 찾지 못했다면 '찾아보기' 버튼을 눌러 얼불춤 폴더를 선택해주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Select = "찾아보기", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_Location = "얼불춤 폴더 선택", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 주석: SelectLocation_AdofaiFolderGuide = "얼불춤 폴더 가이드", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NoLocation = "위치가 설정되지 않았습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NoFolder = "폴더가 존재하지 않습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NoAdofai = "선택된 폴더는 얼불춤 폴더가 아닙니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyTitle = "경로 분석", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyEmpty = "폴더 위치가 설정되있지 않습니다", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyFolderNotExist = "폴더가 존재하지 않습니다", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyFolderExist = "폴더가 존재합니다", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyGameNotFound = "게임 프로그램이나 데이터를 찾을 수 없습니다", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyGameFound = "게임을 찾았습니다", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyUmmIsAssembly = "UMM이 Assembly 형식으로 설치되었습니다", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyUmmIsAssembly2 = "얼불춤을 업데이트 할 때 마다 재설치 해야 합니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 121 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyDoorstopIsOld = "Doorstop이 구버전입니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 오른쪽 결과를 왼쪽 상태에 저장한다: SelectLocation_NotifyDoorstopIsOld2 = "r141부터 크래시가 발생하며 사용할 수 없습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Title = "설치 요소를 선택해주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Work = "작업", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Work_Install = "설치", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Work_Uninstall = "제거", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement = "필수 요소 설치", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_UnityModManager = "지퍼 리소스팩을 사용하기 위해서는 유니티 모드 매니저가 필요합니다.\n유니티 모드 매니저가 설치되어 있지 않아 제외할 수 없습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_JALib = "지퍼 리소스팩을 사용하기 위해서는 JALib이 필요합니다.\nJALib이 설치되어 있지 않아 제외할 수 없습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_Doorstop_Umm = "유니티 모드 매니저를 사용하기 위해서는 Doorstop이 필요합니다.\n유니티 모드 매니저가 선택되어 제외할 수 없습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_Doorstop_Asm = "현재 유니티 모드 매니저가 Assembly 방식으로 설치되어 있습니다.\nAssembly 방식은 게임을 업데이트 할 때 마다 새로 설치해야 합니다. 계속 하시겠습니까?", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 132 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_Doorstop_Old = "현재 사용중인 Doorstop이 구버전 입니다.\nr141부터는 5.4.0이상에 Doorstop이 아닐 경우 크래시가 발생합니다. 계속 하시겠습니까?", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 133 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_Requirement_JipperResourcePack = "지퍼 리소스팩이 설치되어 있지 않아 제외할 수 없습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 134 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_AdditionMods = "추가 모드 설치", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 135 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_RemoveMods = "기존 모드 제거", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 136 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_RemoveMods_Confirm = "선택한 모드를 정말 제거하시겠습니까?\n제거된 모드는 복구할 수 없으며 다시 설치해도 데이터가 유지되지 않을 수 있습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 137 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption = "제거 옵션", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 138 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_All = "전체 제거", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 139 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_AllDescription = "모드 매니저와 모든 모드를 완전히 제거합니다. 모드를 다시 사용하기 위해서는 모든 모드를 다시 설치해야 합니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 140 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyManager = "모드 매니저만 제거", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 141 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyManagerDescription = "모드 데이터는 남겨둔 채로 모드 매니저만 제거합니다. 모드 매니저를 다시 설치하면 그대로 모드를 사용할 수 있습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 142 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyMod = "특정 모드만 제거", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 143 | 오른쪽 결과를 왼쪽 상태에 저장한다: Select_UninstallOption_OnlyModDescription = "모든 모드를 지우지 않고 특정 모드만 제거합니다. 나머지 모드는 계속해서 사용할 수 있습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 144 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Finish = "마침", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 145 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Title_Install = "지퍼 리소스팩 설치 완료", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 146 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Title_Uninstall = "지퍼 리소스팩 제거 완료", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 147 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Description_Install = "지퍼 리소스팩 설치가 완료되었습니다.\n프로그램을 마치려면 '마침' 버튼을 눌러주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 148 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Description_Uninstall = "지퍼 리소스팩 제거가 완료되었습니다.\n프로그램을 마치려면 '마침' 버튼을 눌러주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 149 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_RunAdofai = "얼불춤 실행", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 150 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Title_Error = "지퍼 리소스팩을 설치하지 못했습니다.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 151 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_Description_Error = "지퍼 리소스팩 설치중 오류가 발생했습니다.\n자세한 오류를 확인하기 위해서는 '로그 보기' 버튼을 눌러주세요.", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 152 | 오른쪽 결과를 왼쪽 상태에 저장한다: FinishScreen_CheckLog = "로그 보기" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 153 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 154 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 155 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Resources Current = CultureInfo.CurrentCulture.Name == "ko-KR" ? Korean : English; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 156 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 157 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Title; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 158 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Cancel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 159 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Previous; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 160 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Next; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 161 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Title; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 162 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 163 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 164 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description3; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 165 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description4; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 166 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description5; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 167 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description6; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 168 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Description7; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 169 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_BugReport; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 170 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_Donate; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 171 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_OpenBrowserFailed; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 172 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_KakaoPayQrForm; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 173 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string MainScreen_NoInternet; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 174 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Warn; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 175 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Error; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 176 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Title1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 177 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Title2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 178 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Title3; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 179 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Title4; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 180 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_Title; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 181 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_Description; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 182 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_Select; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 183 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_Location; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 184 | 주석: public string SelectLocation_AdofaiFolderGuide; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 185 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NoLocation; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 186 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NoFolder; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 187 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NoAdofai; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 188 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyTitle; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 189 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyEmpty; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 190 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyFolderNotExist; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 191 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyFolderExist; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 192 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyGameNotFound; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 193 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyGameFound; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 194 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyUmmIsAssembly; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 195 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyUmmIsAssembly2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 196 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyDoorstopIsOld; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 197 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string SelectLocation_NotifyDoorstopIsOld2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 198 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Title; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 199 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Work; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 200 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Work_Install; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 201 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Work_Uninstall; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 202 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 203 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement_UnityModManager; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 204 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement_JALib; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 205 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement_Doorstop_Umm; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 206 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement_Doorstop_Asm; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 207 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement_Doorstop_Old; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 208 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_Requirement_JipperResourcePack; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 209 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_AdditionMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 210 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_RemoveMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 211 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_RemoveMods_Confirm; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 212 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 213 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption_All; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 214 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption_AllDescription; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 215 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption_OnlyManager; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 216 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption_OnlyManagerDescription; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 217 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption_OnlyMod; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 218 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Select_UninstallOption_OnlyModDescription; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 219 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Finish; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 220 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Title_Install; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 221 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Title_Uninstall; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 222 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Description_Install; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 223 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Description_Uninstall; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 224 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_RunAdofai; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 225 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Title_Error; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 226 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_Description_Error; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 227 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string FinishScreen_CheckLog; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 228 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Resource/UMM/Config.xml

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,12 +0,0 @@

```diff
@@ -1,12 +0,0 @@
-<?xml version="1.0" encoding="utf-8"?>
-<Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="A Dance of Fire and Ice">
-  <Folder>ADOFAI</Folder>
-  <ModsDirectory>Mods</ModsDirectory>
-  <ModInfo>Info.json</ModInfo>
-  <GameExe>A Dance of Fire and Ice.exe</GameExe>
-  <EntryPoint>[UnityEngine.CoreModule.dll]UnityEngine.MonoBehaviour.cctor:Before</EntryPoint>
-  <StartingPoint>[Assembly-CSharp.dll]ADOStartup.Startup:Before</StartingPoint>
-  <UIStartingPoint>[Assembly-CSharp.dll]ADOStartup.Startup:After</UIStartingPoint>
-  <MinimalManagerVersion>0.22.14</MinimalManagerVersion>
-  <Comment>Required minimum game version 2.7.0</Comment>
-</Config>
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version="1.0" encoding="utf-8"?&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="A Dance of Fire and Ice"&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Folder&gt;ADOFAI&lt;/Folder&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ModsDirectory&gt;Mods&lt;/ModsDirectory&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 프로젝트·리소스·설정 요소를 지정한다: &lt;ModInfo&gt;Info.json&lt;/ModInfo&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 프로젝트·리소스·설정 요소를 지정한다: &lt;GameExe&gt;A Dance of Fire and Ice.exe&lt;/GameExe&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 프로젝트·리소스·설정 요소를 지정한다: &lt;EntryPoint&gt;[UnityEngine.CoreModule.dll]UnityEngine.MonoBehaviour.cctor:Before&lt;/EntryPoint&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 프로젝트·리소스·설정 요소를 지정한다: &lt;StartingPoint&gt;[Assembly-CSharp.dll]ADOStartup.Startup:Before&lt;/StartingPoint&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 프로젝트·리소스·설정 요소를 지정한다: &lt;UIStartingPoint&gt;[Assembly-CSharp.dll]ADOStartup.Startup:After&lt;/UIStartingPoint&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 프로젝트·리소스·설정 요소를 지정한다: &lt;MinimalManagerVersion&gt;0.22.14&lt;/MinimalManagerVersion&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 프로젝트·리소스·설정 요소를 지정한다: &lt;Comment&gt;Required minimum game version 2.7.0&lt;/Comment&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/Config&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Resource/UMM/doorstop_config.ini

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,3 +0,0 @@

```diff
@@ -1,3 +0,0 @@
-[General]
-enabled = true
-target_assembly = A Dance of Fire and Ice_Data\Managed\UnityModManager\UnityModManager.dll
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 메타데이터/compiler 지시를 지정한다: [General] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 오른쪽 결과를 왼쪽 상태에 저장한다: enabled = true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 오른쪽 결과를 왼쪽 상태에 저장한다: target_assembly = A Dance of Fire and Ice_Data\Managed\UnityModManager\UnityModManager.dll | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Resource/check.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 340 bytes
- 삭제 전 SHA-256: ba647697eacdb98b6ecd39bbbeb7097c971dcf887b143646fd45bcfdcefb02f0
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/close.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 357 bytes
- 삭제 전 SHA-256: bbdd00e4e84a140121add7bd85c8d107be61af2a728ead95694cdf4427332fd1
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/kofi_logo.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 2109 bytes
- 삭제 전 SHA-256: f842977281d3f27566a2c1fa69738109e44899faadf6a88710bd796e8a4b1216
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/mail.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 419 bytes
- 삭제 전 SHA-256: 04cf9c0897e553b4976626cbc6dc17abd629540ae8ae47f4934520b82612a8bb
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/payment_icon_yellow_small.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 2163 bytes
- 삭제 전 SHA-256: 2686103ff528f78f9c1eb4f4895a972af9b2be5ef80222321c0f52d95a18d0dd
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Resource/warning.png

기존 Installer 화면·아이콘·후원/링크 안내용 바이너리 리소스가 삭제된다. SDK 연동에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다.

- 삭제 전 크기: 517 bytes
- 삭제 전 SHA-256: 75cbe1b4ead5e96affe4750a6a73a6bfa17eae1f4354934d1c2a4a20331130f1
- 바이너리이므로 줄 단위 의사코드가 없다.

## Installer/Screen/FinishScreen.cs

기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다.

### @@ -1,146 +0,0 @@

```diff
@@ -1,146 +0,0 @@
-﻿using System;
-using System.Diagnostics;
-using System.Drawing;
-using System.IO;
-using System.Reflection;
-using System.Text;
-using System.Windows.Forms;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer.Screen;
-
-public class FinishScreen : Screen {
-    public CheckBox CheckBox;
-    public string Folder;
-
-    public FinishScreen(Screen screen) {
-        PrevScreen = screen;
-        Cancelable = false;
-        Prevable = false;
-        UpPanelVisible = false;
-    }
-
-    public override void OnEnter() {
-        NextButton.Text = Resources.Current.FinishScreen_Finish;
-        Exception exception = ((InstallScreen) PrevScreen)?.Exception;
-        MainPanel.SuspendLayout();
-        MainPanel.Controls.AddRange([
-            // JipperResourceSide
-            new PictureBox {
-                Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.JipperResourceSide.png")!),
-                Size = new Size(403, 561)
-            },
-            
-            // Title
-            new Label {
-                Text = exception != null ? Resources.Current.FinishScreen_Title_Error :
-                       GlobalSetting.Instance.IsUninstall ? Resources.Current.FinishScreen_Title_Uninstall : Resources.Current.FinishScreen_Title_Install,
-                Font = Constants.Arial24B,
-                AutoSize = true,
-                Location = new Point(430, 48)
-            },
-            
-            // Description
-            new Label {
-                Text = exception != null ? Resources.Current.FinishScreen_Description_Error :
-                           GlobalSetting.Instance.IsUninstall ? Resources.Current.FinishScreen_Description_Uninstall : Resources.Current.FinishScreen_Description_Install,
-                Font = Constants.Arial12,
-                AutoSize = true,
-                Location = new Point(435, 128)
-            },
-            
-            // Bug Report
-            new Label {
-                Text = Resources.Current.MainScreen_BugReport,
-                Font = Constants.Arial12,
-                AutoSize = true,
-                Location = new Point(440, 444)
-            },
-            new LinkedImageButton("https://discord.gg/qTbnPhY7YA", "JipperResourcePack.Installer.Resource.Discord-Symbol-Blurple.png") {
-                Size = new Size(32, 24),
-                Location = new Point(445, 474)
-            },
-            new LinkedImageButton("https://github.com/Jongye0l/JipperResourcePack/issues", "JipperResourcePack.Installer.Resource.GitHub_Invertocat_Black.png") {
-                Size = new Size(32, 31),
-                Location = new Point(485, 470)
-            },
-            new LinkedImageButton("mailto:bcpjy1233@gmail.com", "JipperResourcePack.Installer.Resource.mail.png") {
-                Size = new Size(32, 32),
-                Location = new Point(525, 470)
-            },
-            
-            // Donate
-            new Label {
-                Text = Resources.Current.MainScreen_Donate,
-                Font = Constants.Arial12,
-                AutoSize = true,
-                Location = new Point(605, 444)
-            },
-            new LinkedImageButton(null, "JipperResourcePack.Installer.Resource.payment_icon_yellow_small.png") {
-                Size = new Size(64, 27),
-                Location = new Point(610, 470)
-            },
-            new LinkedImageButton("https://ko-fi.com/jongyeol", "JipperResourcePack.Installer.Resource.kofi_logo.png") {
-                Size = new Size(87, 24),
-                Location = new Point(682, 470)
-            }
-        ]);
-        if(exception == null) {
-            CheckBox = new CheckBox {
-                Text = Resources.Current.FinishScreen_RunAdofai,
-                Font = Constants.Arial12,
-                AutoSize = true,
-                Location = new Point(440, 216),
-                Checked = true
-            };
-            MainPanel.Controls.Add(CheckBox);
-        } else {
-            Button checkLog = new() {
-                Text = Resources.Current.FinishScreen_CheckLog,
-                Font = Constants.Arial12,
-                AutoSize = true,
-                Location = new Point(440, 216)
-            };
-            checkLog.Click += CheckLog_Click;
-            MainPanel.Controls.Add(checkLog);
-        }
-        MainPanel.ResumeLayout();
-    }
-
-    private void CheckLog_Click(object sender, EventArgs e) {
-        string logPath;
-        if(Folder == null) {
-            Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "JipperResourcePack");
-            Directory.CreateDirectory(Folder);
-            try {
-                Application.ApplicationExit += DeleteFolder;
-                logPath = Path.Combine(Folder, "log.txt");
-                StringBuilder builder = new();
-                InstallScreen installScreen = (InstallScreen) PrevScreen;
-                foreach(Control control in installScreen.LogPanel.Controls) builder.AppendLine(control.Text);
-                builder.Append(installScreen.Exception);
-                File.WriteAllText(logPath, builder.ToString());
-            } catch {
-                DeleteFolder(null, null);
-                throw;
-            }
-        } else logPath = Path.Combine(Folder, "log.txt");
-        Process.Start(logPath);
-    }
-
-    private void DeleteFolder(object obj, EventArgs args) {
-        Directory.Delete(Folder, true);
-        Application.ApplicationExit -= DeleteFolder;
-    }
-
-    public override void OnLeave() {
-        if(CheckBox is { Checked: true }) {
-            if(GlobalSetting.Instance.InstallPath == Utility.GetAdofaiPath()) Process.Start("steam://rungameid/977950");
-            else Process.Start(new ProcessStartInfo {
-                FileName = Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice.exe"),
-                WorkingDirectory = GlobalSetting.Instance.InstallPath
-            });
-        }
-        if(Folder != null) DeleteFolder(null, null);
-    }
-}
\ No newline at end of file
```

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Diagnostics; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.IO; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | namespace 또는 타입 별칭을 가져온다: System.Text; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 타입을 선언한다: public class FinishScreen : Screen { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CheckBox CheckBox; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string Folder; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public FinishScreen(Screen screen) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 오른쪽 결과를 왼쪽 상태에 저장한다: PrevScreen = screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cancelable = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 오른쪽 결과를 왼쪽 상태에 저장한다: Prevable = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 오른쪽 결과를 왼쪽 상태에 저장한다: UpPanelVisible = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnEnter() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 오른쪽 결과를 왼쪽 상태에 저장한다: NextButton.Text = Resources.Current.FinishScreen_Finish; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 오른쪽 결과를 왼쪽 상태에 저장한다: Exception exception = ((InstallScreen) PrevScreen)?.Exception; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 이 함수 호출/식 처리를 실행한다: MainPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 연결된 식·선언·설정의 일부다: MainPanel.Controls.AddRange([ | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 주석: JipperResourceSide | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 연결된 식·선언·설정의 일부다: new PictureBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 오른쪽 결과를 왼쪽 상태에 저장한다: Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.JipperResourceSide.png")!), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(403, 561) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 주석: Title | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = exception != null ? Resources.Current.FinishScreen_Title_Error : | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 연결된 식·선언·설정의 일부다: GlobalSetting.Instance.IsUninstall ? Resources.Current.FinishScreen_Title_Uninstall : Resources.Current.FinishScreen_Title_Install, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial24B, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(430, 48) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 주석: Description | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = exception != null ? Resources.Current.FinishScreen_Description_Error : | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 연결된 식·선언·설정의 일부다: GlobalSetting.Instance.IsUninstall ? Resources.Current.FinishScreen_Description_Uninstall : Resources.Current.FinishScreen_Description_Install, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial12, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 128) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 주석: Bug Report | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_BugReport, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial12, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(440, 444) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("https://discord.gg/qTbnPhY7YA", "JipperResourcePack.Installer.Resource.Discord-Symbol-Blurple.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(32, 24), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(445, 474) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("https://github.com/Jongye0l/JipperResourcePack/issues", "JipperResourcePack.Installer.Resource.GitHub_Invertocat_Black.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(32, 31), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(485, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("mailto:bcpjy1233@gmail.com", "JipperResourcePack.Installer.Resource.mail.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(32, 32), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(525, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 주석: Donate | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Donate, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial12, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(605, 444) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 연결된 식·선언·설정의 일부다: new LinkedImageButton(null, "JipperResourcePack.Installer.Resource.payment_icon_yellow_small.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(64, 27), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(610, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("https://ko-fi.com/jongyeol", "JipperResourcePack.Installer.Resource.kofi_logo.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(87, 24), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(682, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 이 함수 호출/식 처리를 실행한다: ]); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 조건을 만족할 때 뒤의 작업을 수행한다: if(exception == null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 오른쪽 결과를 왼쪽 상태에 저장한다: CheckBox = new CheckBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.FinishScreen_RunAdofai, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial12, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(440, 216), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(CheckBox); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 오른쪽 결과를 왼쪽 상태에 저장한다: Button checkLog = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.FinishScreen_CheckLog, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial12, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(440, 216) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 이 함수 호출/식 처리를 실행한다: checkLog.Click += CheckLog_Click; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(checkLog); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 이 함수 호출/식 처리를 실행한다: MainPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void CheckLog_Click(object sender, EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 이 함수 호출/식 처리를 실행한다: string logPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Folder == null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 오른쪽 결과를 왼쪽 상태에 저장한다: Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "JipperResourcePack"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 이 함수 호출/식 처리를 실행한다: Directory.CreateDirectory(Folder); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 이 함수 호출/식 처리를 실행한다: Application.ApplicationExit += DeleteFolder; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 오른쪽 결과를 왼쪽 상태에 저장한다: logPath = Path.Combine(Folder, "log.txt"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 오른쪽 결과를 왼쪽 상태에 저장한다: StringBuilder builder = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 오른쪽 결과를 왼쪽 상태에 저장한다: InstallScreen installScreen = (InstallScreen) PrevScreen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 각 컬렉션 항목을 순회한다: foreach(Control control in installScreen.LogPanel.Controls) builder.AppendLine(control.Text); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 121 | 이 함수 호출/식 처리를 실행한다: builder.Append(installScreen.Exception); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 이 함수 호출/식 처리를 실행한다: File.WriteAllText(logPath, builder.ToString()); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 이 함수 호출/식 처리를 실행한다: DeleteFolder(null, null); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 이 함수 호출/식 처리를 실행한다: throw; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 이 함수 호출/식 처리를 실행한다: Process.Start(logPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void DeleteFolder(object obj, EventArgs args) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 132 | 이 함수 호출/식 처리를 실행한다: Directory.Delete(Folder, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 133 | 이 함수 호출/식 처리를 실행한다: Application.ApplicationExit -= DeleteFolder; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 134 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 135 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 136 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnLeave() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 137 | 조건을 만족할 때 뒤의 작업을 수행한다: if(CheckBox is { Checked: true }) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 138 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallPath == Utility.GetAdofaiPath()) Process.Start("steam://rungameid/977950"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 139 | 앞 조건이 거짓일 때의 처리다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 140 | 오른쪽 결과를 왼쪽 상태에 저장한다: FileName = Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice.exe"), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 141 | 오른쪽 결과를 왼쪽 상태에 저장한다: WorkingDirectory = GlobalSetting.Instance.InstallPath | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 142 | callback/호출 본문을 닫고 등록 또는 호출을 마친다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 143 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 144 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Folder != null) DeleteFolder(null, null); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 145 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 146 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 메타 | — | 마지막 줄 뒤에 개행이 없다. | 파일 끝 형식 표시다. |

## Installer/Screen/InstallScreen.cs

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/Screen/InstallScreen.cs b/Installer/Screen/InstallScreen.cs
deleted file mode 100644
index 8b4b123..0000000
--- a/Installer/Screen/InstallScreen.cs
+++ /dev/null
@@ -1,527 +0,0 @@
-using System;
-using System.Collections.Concurrent;
-using System.Drawing;
-using System.IO;
-using System.IO.Compression;
-using System.Net;
-using System.Net.Http;
-using System.Reflection;
-using System.Text;
-using System.Threading;
-using System.Threading.Tasks;
-using System.Windows.Forms;
-using System.Web.Script.Serialization;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer.Screen;
-
-public class InstallScreen : Screen {
-    public ProgressBar ProgressBar;
-    public Panel LogPanel;
-    public Label LogLabel;
-    public Label LastLogLabel = new() { Location = new Point(0, -10) };
-    public ConcurrentQueue<string> LogQueue;
-
-    public int Progress;
-    public string DownloadName;
-    public int DownloadProgressStart;
-    public int DownloadProgressEnd;
-    public bool Complete;
-    public Exception Exception;
-
-    public string TempPath;
-
-    public InstallScreen(Screen screen) {
-        PrevScreen = screen;
-        NextScreen = new FinishScreen(this);
-        Cancelable = false;
-        Prevable = false;
-        Nextable = false;
-    }
-
-    public override void OnEnter() {
-        TopPanelLabels[2].Font = Constants.Arial16B;
-
-        int maxValue = 0;
-        if(GlobalSetting.Instance.IsUninstall) {
-            maxValue = GlobalSetting.Instance.UninstallOption switch {
-                0 => 4,
-                1 => 3,
-                _ => GlobalSetting.Instance.RemoveRequestMods.Count
-            };
-        } else {
-            if(GlobalSetting.Instance.InstallUnityModManager) maxValue += 60;
-            if(GlobalSetting.Instance.InstallDoorstop) maxValue += 80;
-            if(GlobalSetting.Instance.InstallJalib) maxValue += 70;
-            if(GlobalSetting.Instance.InstallJipperResourcePack) maxValue += 70;
-            maxValue += 50 * GlobalSetting.Instance.SelectedMods.Count;
-            maxValue += 10 * GlobalSetting.Instance.RemoveRequestMods.Count;
-        }
-
-        ProgressBar = new ProgressBar {
-            Minimum = 0,
-            Maximum = maxValue,
-            Size = new Size(800, 20),
-            Location = new Point(92, 120)
-        };
-        LogLabel = new Label {
-            Font = Constants.Arial12,
-            AutoSize = true,
-            Location = new Point(92, 95)
-        };
-        LogPanel = new Panel {
-            Location = new Point(92, 150),
-            Size = new Size(800, 400),
-            AutoScroll = true,
-            BackColor = Color.Silver
-        };
-
-        MainPanel.SuspendLayout();
-        MainPanel.Controls.Add(ProgressBar);
-        MainPanel.Controls.Add(LogLabel);
-        MainPanel.Controls.Add(LogPanel);
-        MainPanel.ResumeLayout();
-
-        LogQueue = new ConcurrentQueue<string>([
-            "JipperResourcePack Installer v" + Application.ProductVersion,
-            "Adofai Version: " + (GlobalSetting.Instance.AdofaiRevision == -1 ? "Unknown" : "r" + GlobalSetting.Instance.AdofaiRevision),
-            "UnityModManager Installed: " + GlobalSetting.Instance.InstallUnityModManager,
-            "Doorstop Installed: " + GlobalSetting.Instance.InstallDoorstop,
-            "JALib Installed: " + GlobalSetting.Instance.InstallJalib,
-            "JipperResourcePack Installed: " + GlobalSetting.Instance.InstallJipperResourcePack,
-            "IsInstall: " + !GlobalSetting.Instance.IsUninstall,
-            "Starting Work..."
-        ]);
-        TempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "JipperResourcePack");
-
-        Task.Run(StartWork);
-        LogListener();
-    }
-
-    public async void StartWork() {
-        try {
-            if(GlobalSetting.Instance.IsUninstall) {
-                string targetPath;
-                switch(GlobalSetting.Instance.UninstallOption) {
-                    case 0:
-                        targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "Mods");
-                        Log($"Deleting {targetPath}...");
-                        Directory.Delete(targetPath, true);
-                        Progress = 1;
-                        goto case 1;
-                    case 1:
-                        targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager");
-                        Log($"Deleting {targetPath}...");
-                        Directory.Delete(targetPath, true);
-                        Progress++;
-                        targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "winhttp.dll");
-                        Log($"Deleting {targetPath}...");
-                        File.Delete(targetPath);
-                        Progress++;
-                        targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "doorstop_config.ini");
-                        Log($"Deleting {targetPath}...");
-                        File.Delete(targetPath);
-                        Progress++;
-                        break;
-                    default:
-                        foreach(string path in GlobalSetting.Instance.RemoveRequestMods) {
-                            Directory.Delete(path, true);
-                            Log($"Deleting {path}...");
-                            Progress++;
-                        }
-                        break;
-                }
-            } else {
-                if(GlobalSetting.Instance.InstallUnityModManager) await InstallUnityModManager();
-                if(GlobalSetting.Instance.InstallDoorstop) await InstallDoorstop();
-
-                // Remove Before Install
-                foreach(string path in GlobalSetting.Instance.RemoveRequestMods) {
-                    Log($"Deleting {path}...");
-                    Directory.Delete(path, true);
-                    Progress += 10;
-                }
-
-                if(GlobalSetting.Instance.InstallJalib) await DownloadJALib();
-                if(GlobalSetting.Instance.InstallJipperResourcePack) await DownloadJipperResourcePack();
-                foreach(ModData selectedMod in GlobalSetting.Instance.SelectedMods) {
-                    Log("Downloading " + selectedMod.Name + "...");
-                    DownloadName = selectedMod.Name;
-                    DownloadProgressStart = Progress;
-                    DownloadProgressEnd = Progress + 50;
-                    await Download(selectedMod.URL, Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", selectedMod.Name), true);
-                    Progress = DownloadProgressEnd;
-                    Log("Download Complete " + selectedMod.Name);
-                }
-            }
-            Complete = true;
-        } catch (Exception e) {
-            Exception = e;
-            Log("Error: " + e.Message);
-        }
-    }
-
-    public async Task InstallUnityModManager() {
-        string managerPath = Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager");
-        if(Directory.Exists(TempPath)) Directory.Delete(TempPath, true);
-        Directory.CreateDirectory(TempPath);
-        try {
-            Application.ApplicationExit += RemoveDirectory;
-            Log("Downloading UnityModManager...");
-            DownloadName = "UnityModManager";
-            DownloadProgressStart = 0;
-            DownloadProgressEnd = 50;
-            await Download("https://www.dropbox.com/s/wz8x8e4onjdfdbm/UnityModManager.zip?dl=1", TempPath, false);
-            Log("Download Complete UnityModManager");
-            Progress = 50;
-
-            if(!Directory.Exists(managerPath))
-                Directory.CreateDirectory(managerPath);
-            foreach(string destFileName in (string[]) ["0Harmony.dll", "dnlib.dll", "UnityModManager.dll", "UnityModManager.xml"]) {
-                Log($"Coping {destFileName}...");
-                File.Copy(Path.Combine(TempPath, "UnityModManagerInstaller", destFileName), Path.Combine(managerPath, destFileName), true);
-                Progress += 2;
-            }
-
-            Log("Coping Confix.xml...");
-            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.UMM.Config.xml");
-            using Stream file = File.Create(Path.Combine(managerPath, "Config.xml"));
-            await stream!.CopyToAsync(file);
-            Progress = 60;
-            Log("Apply Complete UnityModManager");
-        } finally {
-            RemoveDirectory(null, null);
-        }
-    }
-
-    public void RemoveDirectory(object obj, EventArgs args) {
-        Directory.Delete(TempPath, true);
-        Application.ApplicationExit -= RemoveDirectory;
-    }
-
-    public async Task InstallDoorstop() {
-        string doorstopPath = Path.Combine(GlobalSetting.Instance.InstallPath, "winhttp.dll");
-        string doorstopConfigPath = Path.Combine(GlobalSetting.Instance.InstallPath, "doorstop_config.ini");
-        int startProgress = Progress;
-
-        Log("Check Doorstop Latest Version...");
-        string latestVersion;
-        using HttpClient httpClient = new();
-        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JipperResourcePack");
-        try {
-            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/NeighTools/UnityDoorstop/releases/latest");
-            Progress = startProgress + 7;
-            response.EnsureSuccessStatusCode();
-            Progress = startProgress + 14;
-            string json = await response.Content.ReadAsStringAsync();
-            latestVersion = GetReleaseTag(json);
-        } catch (Exception) {
-            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/NeighTools/UnityDoorstop/releases");
-            Progress = startProgress + 10;
-            response.EnsureSuccessStatusCode();
-            Progress = startProgress + 16;
-            string json = await response.Content.ReadAsStringAsync();
-            latestVersion = GetFirstReleaseTag(json);
-        }
-        Progress = startProgress + 20;
-        Log("Latest Version is " + latestVersion);
-
-        if(Directory.Exists(TempPath)) Directory.Delete(TempPath, true);
-        Directory.CreateDirectory(TempPath);
-        try {
-            Application.ApplicationExit += RemoveDirectory;
-            DownloadName = "Doorstop";
-            DownloadProgressStart = Progress;
-            DownloadProgressEnd = Progress + 50;
-            Log("Downloading Doorstop...");
-            await Download($"https://github.com/NeighTools/UnityDoorstop/releases/download/{latestVersion}/doorstop_win_release_{latestVersion.TrimStart('v')}.zip", TempPath, false);
-            Log("Download Complete Doorstop");
-
-            Log("Checking program bits...");
-            bool is64Bit = UnmanagedDllIs64Bit(Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice.exe"));
-            Progress += 2;
-            Log($"Program is {(is64Bit ? "64" : "32")} bit");
-
-            if(File.Exists(doorstopPath)) {
-                Log($"Deleting {doorstopPath}...");
-                File.Delete(doorstopPath);
-            }
-            Progress += 2;
-
-            if(File.Exists(doorstopConfigPath)) {
-                Log($"Deleting {doorstopConfigPath}...");
-                File.Delete(doorstopConfigPath);
-            }
-            Progress += 2;
-
-            Log("Coping winhttp.dll...");
-            File.Copy(Path.Combine(TempPath, is64Bit ? "x64" : "x86", "winhttp.dll"), doorstopPath);
-            Progress += 2;
-
-            using(Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.UMM.doorstop_config.ini")) {
-                using Stream file = File.Create(doorstopConfigPath);
-                await stream!.CopyToAsync(file);
-            }
-            Progress = startProgress + 80;
-            Log("Apply Complete Doorstop");
-        } finally {
-            RemoveDirectory(null, null);
-        }
-    }
-
-    public static ushort GetDllMachineType(string dllPath) {
-        using FileStream input = new(dllPath, FileMode.Open, FileAccess.Read);
-        using BinaryReader binaryReader = new(input);
-        input.Seek(60L, SeekOrigin.Begin);
-        input.Seek(binaryReader.ReadInt32(), SeekOrigin.Begin);
-        return binaryReader.ReadUInt32() == 17744U ? binaryReader.ReadUInt16() : throw new Exception("Can't find PE header");
-    }
-
-    public bool UnmanagedDllIs64Bit(string dllPath) {
-        try {
-            switch(GetDllMachineType(dllPath)) {
-                case 512: // IMAGE_FILE_MACHINE_IA64
-                case 34404: // IMAGE_FILE_MACHINE_AMD64
-                    return true;
-                default:
-                    return false;
-            }
-        } catch (Exception ex) {
-            Log("Unable to determine the bitness of " + dllPath);
-            Log(ex.ToString());
-            return false;
-        }
-    }
-
-    public void Log(string text) => LogQueue.Enqueue(text);
-
-    public async void LogListener() {
-        try {
-            while(!Complete && Exception == null) {
-                WorkLog();
-                ProgressBar.Value = Progress;
-                await Task.Delay(1);
-            }
-            WorkLog();
-            Next();
-        } catch (Exception e) {
-            MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        }
-    }
-
-    private void WorkLog() {
-        if(InstallStream.Instance?.NeedUpdate(out double value) == true) {
-            value = 0.2 + value * 0.8;
-            int realStart = DownloadProgressStart + 10;
-            Progress = realStart + (int)((DownloadProgressEnd - realStart) * value);
-            Log($"Downloading {DownloadName}... {(int)(value * 100)}%");
-        } else if(LogQueue.IsEmpty) return;
-
-        LogPanel.SuspendLayout();
-        Label label = LastLogLabel;
-        while(LogQueue.TryDequeue(out string log)) {
-            LogLabel.Text = log;
-            LogPanel.Controls.Add(label = new Label { Text = log, AutoSize = true, Location = new Point(0, label.Location.Y + 20) });
-            Console.WriteLine(log);
-        }
-
-        LastLogLabel = label;
-        LogPanel.VerticalScroll.Value = LogPanel.VerticalScroll.Maximum;
-        LogPanel.ResumeLayout();
-    }
-
-    public override void OnLeave() {
-        TopPanelLabels[2].Font = Constants.Arial16;
-    }
-
-    public async Task Download(string url, string path, bool checkInfo) {
-        if(!Directory.Exists(path)) Directory.CreateDirectory(path);
-        HttpResponseMessage message = await InstallerForm.HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
-        Log($"Downloading {DownloadName}... 16%");
-        Progress = DownloadProgressStart + 8;
-        long contentLength = message.Content.Headers.ContentLength ?? -1;
-        using Stream stream = new InstallStream(await message.Content.ReadAsStreamAsync(), contentLength);
-        Progress = DownloadProgressStart + 2;
-        Log($"Downloading {DownloadName}... 20%");
-        Unzip(stream, path, checkInfo);
-    }
-
-    public static void Unzip(Stream stream, string path, bool checkInfo) {
-        using ZipArchive archive = new(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
-        foreach(ZipArchiveEntry entry in archive.Entries) {
-            string entryPath = Path.Combine(path, entry.FullName);
-            if(entryPath.EndsWith("/")) Directory.CreateDirectory(entryPath);
-            else {
-                string directory = Path.GetDirectoryName(entryPath)!;
-                if(!Directory.Exists(directory)) Directory.CreateDirectory(directory);
-                using FileStream fileStream = new(entryPath, FileMode.Create);
-                entry.Open().CopyTo(fileStream);
-            }
-        }
-        if(!checkInfo || File.Exists(Path.Combine(path, "Info.json"))) return;
-        string folder = Directory.GetDirectories(path)[0];
-        foreach(string file in Directory.GetFiles(folder)) {
-            string fileName = Path.GetFileName(file);
-            File.Move(file, Path.Combine(path, fileName));
-        }
-        foreach(string folder2 in Directory.GetDirectories(folder)) {
-            string folderName = Path.GetFileName(folder2);
-            Directory.Move(folder2, Path.Combine(path, folderName));
-        }
-    }
-
-    public async Task DownloadJALib() {
-        int startProgress = Progress;
-        Log("Check JALib Latest Version...");
-        string latestVersion;
-        using HttpClient httpClient = new();
-        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JipperResourcePack");
-        try {
-            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JALib/releases/latest");
-            Progress = startProgress + 7;
-            response.EnsureSuccessStatusCode();
-            Progress = startProgress + 14;
-            string json = await response.Content.ReadAsStringAsync();
-            latestVersion = GetReleaseTag(json);
-        } catch (Exception) {
-            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JALib/releases");
-            Progress = startProgress + 10;
-            response.EnsureSuccessStatusCode();
-            Progress = startProgress + 16;
-            string json = await response.Content.ReadAsStringAsync();
-            latestVersion = GetFirstReleaseTag(json);
-        }
-        Progress = startProgress + 20;
-        Log("Latest Version is " + latestVersion);
-
-        DownloadName = "JALib";
-        DownloadProgressStart = Progress;
-        DownloadProgressEnd = Progress + 50;
-        Log("Downloading JALib...");
-        await Download($"https://github.com/Jongye0l/JALib/releases/download/{latestVersion}/JALib.zip", Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JALib"), true);
-        Log("Download Complete JALib");
-    }
-
-    public async Task DownloadJipperResourcePack() {
-        int startProgress = Progress;
-        Log("Check JipperResourcePack Latest Version...");
-        string latestVersion;
-        using HttpClient httpClient = new();
-        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JipperResourcePack");
-        try {
-            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JipperResourcePack/releases/latest");
-            Progress = startProgress + 7;
-            response.EnsureSuccessStatusCode();
-            Progress = startProgress + 14;
-            string json = await response.Content.ReadAsStringAsync();
-            latestVersion = GetReleaseTag(json);
-        } catch (Exception) {
-            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JipperResourcePack/releases");
-            Progress = startProgress + 10;
-            response.EnsureSuccessStatusCode();
-            Progress = startProgress + 16;
-            string json = await response.Content.ReadAsStringAsync();
-            latestVersion = GetFirstReleaseTag(json);
-        }
-        Progress = startProgress + 20;
-        Log("Latest Version is " + latestVersion);
-
-        DownloadName = "JipperResourcePack";
-        DownloadProgressStart = Progress;
-        DownloadProgressEnd = Progress + 50;
-        Log("Downloading JipperResourcePack...");
-        await Download($"https://github.com/Jongye0l/JipperResourcePack/releases/download/{latestVersion}/JipperResourcePack.zip", Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JipperResourcePack"), true);
-        Log("Download Complete JipperResourcePack");
-    }
-
-    private static string GetReleaseTag(string json) {
-        ReleaseData release = Utility.JsonSerializer.Deserialize<ReleaseData>(json);
-        return SanitizeReleaseTag(release?.tag_name);
-    }
-
-    private static string GetFirstReleaseTag(string json) {
-        ReleaseData[] releases = Utility.JsonSerializer.Deserialize<ReleaseData[]>(json);
-        if(releases == null || releases.Length == 0) throw new InvalidDataException("No releases were found.");
-        return SanitizeReleaseTag(releases[0]?.tag_name);
-    }
-
-    private static string SanitizeReleaseTag(string tag) {
-        if(string.IsNullOrWhiteSpace(tag) || tag.Length > 64)
-            throw new InvalidDataException("The release tag is invalid.");
-        foreach(char character in tag)
-            if(!char.IsLetterOrDigit(character) && character != '.' && character != '-' && character != '_')
-                throw new InvalidDataException("The release tag contains invalid characters.");
-        return tag;
-    }
-
-    private sealed class ReleaseData {
-        public string tag_name { get; set; }
-    }
-
-    private class InstallStream : Stream {
-        public static InstallStream Instance;
-        private readonly Stream _baseStream;
-        private long _position;
-        private long _lastCheckedPosition;
-        
-        public InstallStream(Stream baseStream, long length) {
-            _baseStream = baseStream;
-            Length = length;
-            Instance = this;
-        }
-
-        public bool NeedUpdate(out double value) {
-            if(Length == -1) {
-                value = 0;
-                return false;
-            }
-            long currentPosition = _position;
-            if(currentPosition == _lastCheckedPosition) {
-                value = 0;
-                return false;
-            }
-            value = (double) currentPosition / Length;
-            _lastCheckedPosition = currentPosition;
-            return true;
-        }
-
-        public override void Flush() => _baseStream.Flush();
-        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
-        public override void SetLength(long value) => throw new NotSupportedException();
-
-        public override int Read(byte[] buffer, int offset, int count) {
-            int read = _baseStream.Read(buffer, offset, count);
-            _position += read;
-            return read;
-        }
-
-        public override int ReadByte() {
-            int read = _baseStream.ReadByte();
-            if(read != -1) _position++;
-            return read;
-        }
-
-        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
-            int read = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
-            _position += read;
-            return read;
-        }
-
-        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
-        public override bool CanRead => true;
-        public override bool CanSeek => false;
-        public override bool CanWrite => false;
-        public override long Length { get; }
-
-        public override long Position {
-            get => _position;
-            set => throw new NotSupportedException();
-        }
-
-        protected override void Dispose(bool disposing) {
-            if(!disposing) return;
-            _baseStream.Dispose();
-            Instance = null;
-        }
-    }
-}
```

### 삭제 줄 1부터 60까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Collections.Concurrent; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.IO; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: System.IO.Compression; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | namespace 또는 타입 별칭을 가져온다: System.Net; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | namespace 또는 타입 별칭을 가져온다: System.Net.Http; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | namespace 또는 타입 별칭을 가져온다: System.Text; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | namespace 또는 타입 별칭을 가져온다: System.Threading; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | namespace 또는 타입 별칭을 가져온다: System.Threading.Tasks; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | namespace 또는 타입 별칭을 가져온다: System.Web.Script.Serialization; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 타입을 선언한다: public class InstallScreen : Screen { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public ProgressBar ProgressBar; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Panel LogPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Label LogLabel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public Label LastLogLabel = new() { Location = new Point(0, -10) }; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public ConcurrentQueue&lt;string&gt; LogQueue; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string DownloadName; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int DownloadProgressStart; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int DownloadProgressEnd; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool Complete; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Exception Exception; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string TempPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public InstallScreen(Screen screen) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 오른쪽 결과를 왼쪽 상태에 저장한다: PrevScreen = screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 오른쪽 결과를 왼쪽 상태에 저장한다: NextScreen = new FinishScreen(this); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cancelable = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 오른쪽 결과를 왼쪽 상태에 저장한다: Prevable = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: Nextable = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnEnter() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanelLabels[2].Font = Constants.Arial16B; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: int maxValue = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.IsUninstall) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 오른쪽 결과를 왼쪽 상태에 저장한다: maxValue = GlobalSetting.Instance.UninstallOption switch { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 연결된 식·선언·설정의 일부다: 0 =&gt; 4, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 연결된 식·선언·설정의 일부다: 1 =&gt; 3, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 연결된 식·선언·설정의 일부다: _ =&gt; GlobalSetting.Instance.RemoveRequestMods.Count | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallUnityModManager) maxValue += 60; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallDoorstop) maxValue += 80; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallJalib) maxValue += 70; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallJipperResourcePack) maxValue += 70; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 이 함수 호출/식 처리를 실행한다: maxValue += 50 * GlobalSetting.Instance.SelectedMods.Count; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 이 함수 호출/식 처리를 실행한다: maxValue += 10 * GlobalSetting.Instance.RemoveRequestMods.Count; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 61부터 120까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: ProgressBar = new ProgressBar { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: Minimum = 0, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 오른쪽 결과를 왼쪽 상태에 저장한다: Maximum = maxValue, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(800, 20), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(92, 120) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 오른쪽 결과를 왼쪽 상태에 저장한다: LogLabel = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial12, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(92, 95) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 오른쪽 결과를 왼쪽 상태에 저장한다: LogPanel = new Panel { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(92, 150), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(800, 400), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoScroll = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 오른쪽 결과를 왼쪽 상태에 저장한다: BackColor = Color.Silver | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 이 함수 호출/식 처리를 실행한다: MainPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(ProgressBar); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(LogLabel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(LogPanel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 이 함수 호출/식 처리를 실행한다: MainPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 오른쪽 결과를 왼쪽 상태에 저장한다: LogQueue = new ConcurrentQueue&lt;string&gt;([ | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 연결된 식·선언·설정의 일부다: "JipperResourcePack Installer v" + Application.ProductVersion, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 연결된 식·선언·설정의 일부다: "Adofai Version: " + (GlobalSetting.Instance.AdofaiRevision == -1 ? "Unknown" : "r" + GlobalSetting.Instance.AdofaiRevision), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 연결된 식·선언·설정의 일부다: "UnityModManager Installed: " + GlobalSetting.Instance.InstallUnityModManager, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 연결된 식·선언·설정의 일부다: "Doorstop Installed: " + GlobalSetting.Instance.InstallDoorstop, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 연결된 식·선언·설정의 일부다: "JALib Installed: " + GlobalSetting.Instance.InstallJalib, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 연결된 식·선언·설정의 일부다: "JipperResourcePack Installed: " + GlobalSetting.Instance.InstallJipperResourcePack, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 연결된 식·선언·설정의 일부다: "IsInstall: " + !GlobalSetting.Instance.IsUninstall, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 연결된 식·선언·설정의 일부다: "Starting Work..." | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 이 함수 호출/식 처리를 실행한다: ]); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 오른쪽 결과를 왼쪽 상태에 저장한다: TempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "JipperResourcePack"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 이 함수 호출/식 처리를 실행한다: Task.Run(StartWork); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 이 함수 호출/식 처리를 실행한다: LogListener(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async void StartWork() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.IsUninstall) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 이 함수 호출/식 처리를 실행한다: string targetPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 연결된 식·선언·설정의 일부다: switch(GlobalSetting.Instance.UninstallOption) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 연결된 식·선언·설정의 일부다: case 0: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 오른쪽 결과를 왼쪽 상태에 저장한다: targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "Mods"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {targetPath}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 이 함수 호출/식 처리를 실행한다: Directory.Delete(targetPath, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = 1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 이 함수 호출/식 처리를 실행한다: goto case 1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 연결된 식·선언·설정의 일부다: case 1: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 오른쪽 결과를 왼쪽 상태에 저장한다: targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {targetPath}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 이 함수 호출/식 처리를 실행한다: Directory.Delete(targetPath, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 이 함수 호출/식 처리를 실행한다: Progress++; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 오른쪽 결과를 왼쪽 상태에 저장한다: targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "winhttp.dll"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {targetPath}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 이 함수 호출/식 처리를 실행한다: File.Delete(targetPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 이 함수 호출/식 처리를 실행한다: Progress++; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 121부터 180까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 121 | 오른쪽 결과를 왼쪽 상태에 저장한다: targetPath = Path.Combine(GlobalSetting.Instance.InstallPath, "doorstop_config.ini"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {targetPath}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 이 함수 호출/식 처리를 실행한다: File.Delete(targetPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 이 함수 호출/식 처리를 실행한다: Progress++; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 이 함수 호출/식 처리를 실행한다: break; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 연결된 식·선언·설정의 일부다: default: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 각 컬렉션 항목을 순회한다: foreach(string path in GlobalSetting.Instance.RemoveRequestMods) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 이 함수 호출/식 처리를 실행한다: Directory.Delete(path, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {path}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 이 함수 호출/식 처리를 실행한다: Progress++; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 132 | 이 함수 호출/식 처리를 실행한다: break; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 133 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 134 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 135 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallUnityModManager) await InstallUnityModManager(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 136 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallDoorstop) await InstallDoorstop(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 137 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 138 | 주석: Remove Before Install | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 139 | 각 컬렉션 항목을 순회한다: foreach(string path in GlobalSetting.Instance.RemoveRequestMods) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 140 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {path}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 141 | 이 함수 호출/식 처리를 실행한다: Directory.Delete(path, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 142 | 이 함수 호출/식 처리를 실행한다: Progress += 10; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 143 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 144 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 145 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallJalib) await DownloadJALib(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 146 | 조건을 만족할 때 뒤의 작업을 수행한다: if(GlobalSetting.Instance.InstallJipperResourcePack) await DownloadJipperResourcePack(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 147 | 각 컬렉션 항목을 순회한다: foreach(ModData selectedMod in GlobalSetting.Instance.SelectedMods) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 148 | 이 함수 호출/식 처리를 실행한다: Log("Downloading " + selectedMod.Name + "..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 149 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadName = selectedMod.Name; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 150 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressStart = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 151 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressEnd = Progress + 50; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 152 | 이 함수 호출/식 처리를 실행한다: await Download(selectedMod.URL, Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", selectedMod.Name), true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 153 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = DownloadProgressEnd; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 154 | 이 함수 호출/식 처리를 실행한다: Log("Download Complete " + selectedMod.Name); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 155 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 156 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 157 | 오른쪽 결과를 왼쪽 상태에 저장한다: Complete = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 158 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 159 | 오른쪽 결과를 왼쪽 상태에 저장한다: Exception = e; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 160 | 이 함수 호출/식 처리를 실행한다: Log("Error: " + e.Message); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 161 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 162 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 163 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 164 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async Task InstallUnityModManager() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 165 | 오른쪽 결과를 왼쪽 상태에 저장한다: string managerPath = Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 166 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Directory.Exists(TempPath)) Directory.Delete(TempPath, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 167 | 이 함수 호출/식 처리를 실행한다: Directory.CreateDirectory(TempPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 168 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 169 | 이 함수 호출/식 처리를 실행한다: Application.ApplicationExit += RemoveDirectory; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 170 | 이 함수 호출/식 처리를 실행한다: Log("Downloading UnityModManager..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 171 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadName = "UnityModManager"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 172 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressStart = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 173 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressEnd = 50; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 174 | 이 함수 호출/식 처리를 실행한다: await Download("https://www.dropbox.com/s/wz8x8e4onjdfdbm/UnityModManager.zip?dl=1", TempPath, false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 175 | 이 함수 호출/식 처리를 실행한다: Log("Download Complete UnityModManager"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 176 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = 50; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 177 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 178 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!Directory.Exists(managerPath)) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 179 | 이 함수 호출/식 처리를 실행한다: Directory.CreateDirectory(managerPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 180 | 각 컬렉션 항목을 순회한다: foreach(string destFileName in (string[]) ["0Harmony.dll", "dnlib.dll", "UnityModManager.dll", "UnityModManager.xml"]) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 181부터 240까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 181 | 이 함수 호출/식 처리를 실행한다: Log($"Coping {destFileName}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 182 | 이 함수 호출/식 처리를 실행한다: File.Copy(Path.Combine(TempPath, "UnityModManagerInstaller", destFileName), Path.Combine(managerPath, destFileName), true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 183 | 이 함수 호출/식 처리를 실행한다: Progress += 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 184 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 185 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 186 | 이 함수 호출/식 처리를 실행한다: Log("Coping Confix.xml..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 187 | namespace 또는 타입 별칭을 가져온다: Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.UMM.Config.xml"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 188 | namespace 또는 타입 별칭을 가져온다: Stream file = File.Create(Path.Combine(managerPath, "Config.xml")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 189 | 이 함수 호출/식 처리를 실행한다: await stream!.CopyToAsync(file); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 190 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = 60; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 191 | 이 함수 호출/식 처리를 실행한다: Log("Apply Complete UnityModManager"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 192 | 연결된 식·선언·설정의 일부다: } finally { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 193 | 이 함수 호출/식 처리를 실행한다: RemoveDirectory(null, null); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 194 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 195 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 196 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 197 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void RemoveDirectory(object obj, EventArgs args) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 198 | 이 함수 호출/식 처리를 실행한다: Directory.Delete(TempPath, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 199 | 이 함수 호출/식 처리를 실행한다: Application.ApplicationExit -= RemoveDirectory; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 200 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 201 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 202 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async Task InstallDoorstop() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 203 | 오른쪽 결과를 왼쪽 상태에 저장한다: string doorstopPath = Path.Combine(GlobalSetting.Instance.InstallPath, "winhttp.dll"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 204 | 오른쪽 결과를 왼쪽 상태에 저장한다: string doorstopConfigPath = Path.Combine(GlobalSetting.Instance.InstallPath, "doorstop_config.ini"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 205 | 오른쪽 결과를 왼쪽 상태에 저장한다: int startProgress = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 206 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 207 | 이 함수 호출/식 처리를 실행한다: Log("Check Doorstop Latest Version..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 208 | 이 함수 호출/식 처리를 실행한다: string latestVersion; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 209 | namespace 또는 타입 별칭을 가져온다: HttpClient httpClient = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 210 | 이 함수 호출/식 처리를 실행한다: httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JipperResourcePack"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 211 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 212 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/NeighTools/UnityDoorstop/releases/latest"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 213 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 7; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 214 | 이 함수 호출/식 처리를 실행한다: response.EnsureSuccessStatusCode(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 215 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 14; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 216 | 오른쪽 결과를 왼쪽 상태에 저장한다: string json = await response.Content.ReadAsStringAsync(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 217 | 오른쪽 결과를 왼쪽 상태에 저장한다: latestVersion = GetReleaseTag(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 218 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 219 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/NeighTools/UnityDoorstop/releases"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 220 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 10; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 221 | 이 함수 호출/식 처리를 실행한다: response.EnsureSuccessStatusCode(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 222 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 16; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 223 | 오른쪽 결과를 왼쪽 상태에 저장한다: string json = await response.Content.ReadAsStringAsync(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 224 | 오른쪽 결과를 왼쪽 상태에 저장한다: latestVersion = GetFirstReleaseTag(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 225 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 226 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 20; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 227 | 이 함수 호출/식 처리를 실행한다: Log("Latest Version is " + latestVersion); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 228 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 229 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Directory.Exists(TempPath)) Directory.Delete(TempPath, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 230 | 이 함수 호출/식 처리를 실행한다: Directory.CreateDirectory(TempPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 231 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 232 | 이 함수 호출/식 처리를 실행한다: Application.ApplicationExit += RemoveDirectory; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 233 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadName = "Doorstop"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 234 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressStart = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 235 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressEnd = Progress + 50; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 236 | 이 함수 호출/식 처리를 실행한다: Log("Downloading Doorstop..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 237 | 이 함수 호출/식 처리를 실행한다: await Download($"https://github.com/NeighTools/UnityDoorstop/releases/download/{latestVersion}/doorstop_win_release_{latestVersion.TrimStart('v')}.zip", TempPath, false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 238 | 이 함수 호출/식 처리를 실행한다: Log("Download Complete Doorstop"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 239 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 240 | 이 함수 호출/식 처리를 실행한다: Log("Checking program bits..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 241부터 300까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 241 | 오른쪽 결과를 왼쪽 상태에 저장한다: bool is64Bit = UnmanagedDllIs64Bit(Path.Combine(GlobalSetting.Instance.InstallPath, "A Dance of Fire and Ice.exe")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 242 | 이 함수 호출/식 처리를 실행한다: Progress += 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 243 | 이 함수 호출/식 처리를 실행한다: Log($"Program is {(is64Bit ? "64" : "32")} bit"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 244 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 245 | 조건을 만족할 때 뒤의 작업을 수행한다: if(File.Exists(doorstopPath)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 246 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {doorstopPath}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 247 | 이 함수 호출/식 처리를 실행한다: File.Delete(doorstopPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 248 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 249 | 이 함수 호출/식 처리를 실행한다: Progress += 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 250 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 251 | 조건을 만족할 때 뒤의 작업을 수행한다: if(File.Exists(doorstopConfigPath)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 252 | 이 함수 호출/식 처리를 실행한다: Log($"Deleting {doorstopConfigPath}..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 253 | 이 함수 호출/식 처리를 실행한다: File.Delete(doorstopConfigPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 254 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 255 | 이 함수 호출/식 처리를 실행한다: Progress += 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 256 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 257 | 이 함수 호출/식 처리를 실행한다: Log("Coping winhttp.dll..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 258 | 이 함수 호출/식 처리를 실행한다: File.Copy(Path.Combine(TempPath, is64Bit ? "x64" : "x86", "winhttp.dll"), doorstopPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 259 | 이 함수 호출/식 처리를 실행한다: Progress += 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 260 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 261 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.UMM.doorstop_config.ini")) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 262 | namespace 또는 타입 별칭을 가져온다: Stream file = File.Create(doorstopConfigPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 263 | 이 함수 호출/식 처리를 실행한다: await stream!.CopyToAsync(file); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 264 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 265 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 80; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 266 | 이 함수 호출/식 처리를 실행한다: Log("Apply Complete Doorstop"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 267 | 연결된 식·선언·설정의 일부다: } finally { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 268 | 이 함수 호출/식 처리를 실행한다: RemoveDirectory(null, null); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 269 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 270 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 271 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 272 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static ushort GetDllMachineType(string dllPath) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 273 | namespace 또는 타입 별칭을 가져온다: FileStream input = new(dllPath, FileMode.Open, FileAccess.Read); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 274 | namespace 또는 타입 별칭을 가져온다: BinaryReader binaryReader = new(input); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 275 | 이 함수 호출/식 처리를 실행한다: input.Seek(60L, SeekOrigin.Begin); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 276 | 이 함수 호출/식 처리를 실행한다: input.Seek(binaryReader.ReadInt32(), SeekOrigin.Begin); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 277 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 278 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 279 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 280 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public bool UnmanagedDllIs64Bit(string dllPath) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 281 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 282 | 연결된 식·선언·설정의 일부다: switch(GetDllMachineType(dllPath)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 283 | 연결된 식·선언·설정의 일부다: case 512: // IMAGE_FILE_MACHINE_IA64 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 284 | 연결된 식·선언·설정의 일부다: case 34404: // IMAGE_FILE_MACHINE_AMD64 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 285 | 함수를 종료하거나 식의 결과를 반환한다: return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 286 | 연결된 식·선언·설정의 일부다: default: | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 287 | 함수를 종료하거나 식의 결과를 반환한다: return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 288 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 289 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 290 | 이 함수 호출/식 처리를 실행한다: Log("Unable to determine the bitness of " + dllPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 291 | 이 함수 호출/식 처리를 실행한다: Log(ex.ToString()); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 292 | 함수를 종료하거나 식의 결과를 반환한다: return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 293 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 294 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 295 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 296 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void Log(string text) =&gt; LogQueue.Enqueue(text); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 297 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 298 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async void LogListener() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 299 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 300 | 조건이 참인 동안 반복한다: while(!Complete &amp;&amp; Exception == null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 301부터 360까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 301 | 이 함수 호출/식 처리를 실행한다: WorkLog(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 302 | 오른쪽 결과를 왼쪽 상태에 저장한다: ProgressBar.Value = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 303 | 이 함수 호출/식 처리를 실행한다: await Task.Delay(1); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 304 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 305 | 이 함수 호출/식 처리를 실행한다: WorkLog(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 306 | 이 함수 호출/식 처리를 실행한다: Next(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 307 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 308 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 309 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 310 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 311 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 312 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void WorkLog() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 313 | 조건을 만족할 때 뒤의 작업을 수행한다: if(InstallStream.Instance?.NeedUpdate(out double value) == true) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 314 | 오른쪽 결과를 왼쪽 상태에 저장한다: value = 0.2 + value * 0.8; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 315 | 오른쪽 결과를 왼쪽 상태에 저장한다: int realStart = DownloadProgressStart + 10; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 316 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = realStart + (int)((DownloadProgressEnd - realStart) * value); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 317 | 이 함수 호출/식 처리를 실행한다: Log($"Downloading {DownloadName}... {(int)(value * 100)}%"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 318 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 319 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 320 | 이 함수 호출/식 처리를 실행한다: LogPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 321 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label label = LastLogLabel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 322 | 조건이 참인 동안 반복한다: while(LogQueue.TryDequeue(out string log)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 323 | 오른쪽 결과를 왼쪽 상태에 저장한다: LogLabel.Text = log; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 324 | 오른쪽 결과를 왼쪽 상태에 저장한다: LogPanel.Controls.Add(label = new Label { Text = log, AutoSize = true, Location = new Point(0, label.Location.Y + 20) }); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 325 | 작업/검사 결과를 출력한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 326 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 327 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 328 | 오른쪽 결과를 왼쪽 상태에 저장한다: LastLogLabel = label; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 329 | 오른쪽 결과를 왼쪽 상태에 저장한다: LogPanel.VerticalScroll.Value = LogPanel.VerticalScroll.Maximum; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 330 | 이 함수 호출/식 처리를 실행한다: LogPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 331 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 332 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 333 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnLeave() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 334 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanelLabels[2].Font = Constants.Arial16; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 335 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 336 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 337 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async Task Download(string url, string path, bool checkInfo) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 338 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!Directory.Exists(path)) Directory.CreateDirectory(path); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 339 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage message = await InstallerForm.HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 340 | 이 함수 호출/식 처리를 실행한다: Log($"Downloading {DownloadName}... 16%"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 341 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = DownloadProgressStart + 8; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 342 | 오른쪽 결과를 왼쪽 상태에 저장한다: long contentLength = message.Content.Headers.ContentLength ?? -1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 343 | namespace 또는 타입 별칭을 가져온다: Stream stream = new InstallStream(await message.Content.ReadAsStreamAsync(), contentLength); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 344 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = DownloadProgressStart + 2; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 345 | 이 함수 호출/식 처리를 실행한다: Log($"Downloading {DownloadName}... 20%"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 346 | 이 함수 호출/식 처리를 실행한다: Unzip(stream, path, checkInfo); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 347 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 348 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 349 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void Unzip(Stream stream, string path, bool checkInfo) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 350 | namespace 또는 타입 별칭을 가져온다: ZipArchive archive = new(stream, ZipArchiveMode.Read, false, Encoding.UTF8); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 351 | 각 컬렉션 항목을 순회한다: foreach(ZipArchiveEntry entry in archive.Entries) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 352 | 오른쪽 결과를 왼쪽 상태에 저장한다: string entryPath = Path.Combine(path, entry.FullName); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 353 | 조건을 만족할 때 뒤의 작업을 수행한다: if(entryPath.EndsWith("/")) Directory.CreateDirectory(entryPath); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 354 | 앞 조건이 거짓일 때의 처리다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 355 | 오른쪽 결과를 왼쪽 상태에 저장한다: string directory = Path.GetDirectoryName(entryPath)!; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 356 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!Directory.Exists(directory)) Directory.CreateDirectory(directory); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 357 | namespace 또는 타입 별칭을 가져온다: FileStream fileStream = new(entryPath, FileMode.Create); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 358 | 이 함수 호출/식 처리를 실행한다: entry.Open().CopyTo(fileStream); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 359 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 360 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 361부터 420까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 361 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!checkInfo \|\| File.Exists(Path.Combine(path, "Info.json"))) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 362 | 오른쪽 결과를 왼쪽 상태에 저장한다: string folder = Directory.GetDirectories(path)[0]; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 363 | 각 컬렉션 항목을 순회한다: foreach(string file in Directory.GetFiles(folder)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 364 | 오른쪽 결과를 왼쪽 상태에 저장한다: string fileName = Path.GetFileName(file); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 365 | 이 함수 호출/식 처리를 실행한다: File.Move(file, Path.Combine(path, fileName)); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 366 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 367 | 각 컬렉션 항목을 순회한다: foreach(string folder2 in Directory.GetDirectories(folder)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 368 | 오른쪽 결과를 왼쪽 상태에 저장한다: string folderName = Path.GetFileName(folder2); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 369 | 이 함수 호출/식 처리를 실행한다: Directory.Move(folder2, Path.Combine(path, folderName)); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 370 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 371 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 372 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 373 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async Task DownloadJALib() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 374 | 오른쪽 결과를 왼쪽 상태에 저장한다: int startProgress = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 375 | 이 함수 호출/식 처리를 실행한다: Log("Check JALib Latest Version..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 376 | 이 함수 호출/식 처리를 실행한다: string latestVersion; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 377 | namespace 또는 타입 별칭을 가져온다: HttpClient httpClient = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 378 | 이 함수 호출/식 처리를 실행한다: httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JipperResourcePack"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 379 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 380 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JALib/releases/latest"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 381 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 7; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 382 | 이 함수 호출/식 처리를 실행한다: response.EnsureSuccessStatusCode(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 383 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 14; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 384 | 오른쪽 결과를 왼쪽 상태에 저장한다: string json = await response.Content.ReadAsStringAsync(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 385 | 오른쪽 결과를 왼쪽 상태에 저장한다: latestVersion = GetReleaseTag(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 386 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 387 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JALib/releases"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 388 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 10; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 389 | 이 함수 호출/식 처리를 실행한다: response.EnsureSuccessStatusCode(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 390 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 16; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 391 | 오른쪽 결과를 왼쪽 상태에 저장한다: string json = await response.Content.ReadAsStringAsync(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 392 | 오른쪽 결과를 왼쪽 상태에 저장한다: latestVersion = GetFirstReleaseTag(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 393 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 394 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 20; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 395 | 이 함수 호출/식 처리를 실행한다: Log("Latest Version is " + latestVersion); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 396 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 397 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadName = "JALib"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 398 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressStart = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 399 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressEnd = Progress + 50; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 400 | 이 함수 호출/식 처리를 실행한다: Log("Downloading JALib..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 401 | 이 함수 호출/식 처리를 실행한다: await Download($"https://github.com/Jongye0l/JALib/releases/download/{latestVersion}/JALib.zip", Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JALib"), true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 402 | 이 함수 호출/식 처리를 실행한다: Log("Download Complete JALib"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 403 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 404 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 405 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public async Task DownloadJipperResourcePack() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 406 | 오른쪽 결과를 왼쪽 상태에 저장한다: int startProgress = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 407 | 이 함수 호출/식 처리를 실행한다: Log("Check JipperResourcePack Latest Version..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 408 | 이 함수 호출/식 처리를 실행한다: string latestVersion; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 409 | namespace 또는 타입 별칭을 가져온다: HttpClient httpClient = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 410 | 이 함수 호출/식 처리를 실행한다: httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JipperResourcePack"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 411 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 412 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JipperResourcePack/releases/latest"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 413 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 7; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 414 | 이 함수 호출/식 처리를 실행한다: response.EnsureSuccessStatusCode(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 415 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 14; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 416 | 오른쪽 결과를 왼쪽 상태에 저장한다: string json = await response.Content.ReadAsStringAsync(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 417 | 오른쪽 결과를 왼쪽 상태에 저장한다: latestVersion = GetReleaseTag(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 418 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 419 | 오른쪽 결과를 왼쪽 상태에 저장한다: HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/Jongye0l/JipperResourcePack/releases"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 420 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 10; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 421부터 480까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 421 | 이 함수 호출/식 처리를 실행한다: response.EnsureSuccessStatusCode(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 422 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 16; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 423 | 오른쪽 결과를 왼쪽 상태에 저장한다: string json = await response.Content.ReadAsStringAsync(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 424 | 오른쪽 결과를 왼쪽 상태에 저장한다: latestVersion = GetFirstReleaseTag(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 425 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 426 | 오른쪽 결과를 왼쪽 상태에 저장한다: Progress = startProgress + 20; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 427 | 이 함수 호출/식 처리를 실행한다: Log("Latest Version is " + latestVersion); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 428 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 429 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadName = "JipperResourcePack"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 430 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressStart = Progress; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 431 | 오른쪽 결과를 왼쪽 상태에 저장한다: DownloadProgressEnd = Progress + 50; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 432 | 이 함수 호출/식 처리를 실행한다: Log("Downloading JipperResourcePack..."); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 433 | 이 함수 호출/식 처리를 실행한다: await Download($"https://github.com/Jongye0l/JipperResourcePack/releases/download/{latestVersion}/JipperResourcePack.zip", Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JipperResourcePack"), true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 434 | 이 함수 호출/식 처리를 실행한다: Log("Download Complete JipperResourcePack"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 435 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 436 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 437 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static string GetReleaseTag(string json) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 438 | 오른쪽 결과를 왼쪽 상태에 저장한다: ReleaseData release = Utility.JsonSerializer.Deserialize&lt;ReleaseData&gt;(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 439 | 함수를 종료하거나 식의 결과를 반환한다: return SanitizeReleaseTag(release?.tag_name); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 440 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 441 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 442 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static string GetFirstReleaseTag(string json) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 443 | 오른쪽 결과를 왼쪽 상태에 저장한다: ReleaseData[] releases = Utility.JsonSerializer.Deserialize&lt;ReleaseData[]&gt;(json); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 444 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 445 | 함수를 종료하거나 식의 결과를 반환한다: return SanitizeReleaseTag(releases[0]?.tag_name); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 446 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 447 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 448 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static string SanitizeReleaseTag(string tag) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 449 | 조건을 만족할 때 뒤의 작업을 수행한다: if(string.IsNullOrWhiteSpace(tag) \|\| tag.Length &gt; 64) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 450 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 451 | 각 컬렉션 항목을 순회한다: foreach(char character in tag) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 452 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!char.IsLetterOrDigit(character) &amp;&amp; character != '.' &amp;&amp; character != '-' &amp;&amp; character != '_') | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 453 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 454 | 함수를 종료하거나 식의 결과를 반환한다: return tag; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 455 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 456 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 457 | 타입을 선언한다: private sealed class ReleaseData { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 458 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string tag_name { get; set; } | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 459 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 460 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 461 | 타입을 선언한다: private class InstallStream : Stream { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 462 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static InstallStream Instance; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 463 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private readonly Stream _baseStream; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 464 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private long _position; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 465 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: private long _lastCheckedPosition; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 466 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 467 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public InstallStream(Stream baseStream, long length) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 468 | 오른쪽 결과를 왼쪽 상태에 저장한다: _baseStream = baseStream; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 469 | 오른쪽 결과를 왼쪽 상태에 저장한다: Length = length; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 470 | 오른쪽 결과를 왼쪽 상태에 저장한다: Instance = this; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 471 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 472 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 473 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public bool NeedUpdate(out double value) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 474 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Length == -1) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 475 | 오른쪽 결과를 왼쪽 상태에 저장한다: value = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 476 | 함수를 종료하거나 식의 결과를 반환한다: return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 477 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 478 | 오른쪽 결과를 왼쪽 상태에 저장한다: long currentPosition = _position; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 479 | 조건을 만족할 때 뒤의 작업을 수행한다: if(currentPosition == _lastCheckedPosition) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 480 | 오른쪽 결과를 왼쪽 상태에 저장한다: value = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 481부터 527까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 481 | 함수를 종료하거나 식의 결과를 반환한다: return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 482 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 483 | 오른쪽 결과를 왼쪽 상태에 저장한다: value = (double) currentPosition / Length; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 484 | 오른쪽 결과를 왼쪽 상태에 저장한다: _lastCheckedPosition = currentPosition; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 485 | 함수를 종료하거나 식의 결과를 반환한다: return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 486 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 487 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 488 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void Flush() =&gt; _baseStream.Flush(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 489 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 490 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 491 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 492 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override int Read(byte[] buffer, int offset, int count) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 493 | 오른쪽 결과를 왼쪽 상태에 저장한다: int read = _baseStream.Read(buffer, offset, count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 494 | 이 함수 호출/식 처리를 실행한다: _position += read; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 495 | 함수를 종료하거나 식의 결과를 반환한다: return read; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 496 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 497 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 498 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override int ReadByte() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 499 | 오른쪽 결과를 왼쪽 상태에 저장한다: int read = _baseStream.ReadByte(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 500 | 조건을 만족할 때 뒤의 작업을 수행한다: if(read != -1) _position++; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 501 | 함수를 종료하거나 식의 결과를 반환한다: return read; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 502 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 503 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 504 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override async Task&lt;int&gt; ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 505 | 오른쪽 결과를 왼쪽 상태에 저장한다: int read = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 506 | 이 함수 호출/식 처리를 실행한다: _position += read; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 507 | 함수를 종료하거나 식의 결과를 반환한다: return read; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 508 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 509 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 510 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 511 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public override bool CanRead =&gt; true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 512 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public override bool CanSeek =&gt; false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 513 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public override bool CanWrite =&gt; false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 514 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public override long Length { get; } | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 515 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 516 | 연결된 식·선언·설정의 일부다: public override long Position { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 517 | 이 함수 호출/식 처리를 실행한다: get =&gt; _position; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 518 | 오류를 발생시켜 현재 작업을 실패시킨다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 519 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 520 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 521 | 함수/생성자의 이름·입력·반환 형식을 선언한다: protected override void Dispose(bool disposing) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 522 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!disposing) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 523 | 이 함수 호출/식 처리를 실행한다: _baseStream.Dispose(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 524 | 오른쪽 결과를 왼쪽 상태에 저장한다: Instance = null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 525 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 526 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 527 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Screen/MainScreen.cs

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/Screen/MainScreen.cs b/Installer/Screen/MainScreen.cs
deleted file mode 100644
index 49f90cc..0000000
--- a/Installer/Screen/MainScreen.cs
+++ /dev/null
@@ -1,130 +0,0 @@
-﻿using System.Drawing;
-using System.Linq;
-using System.Net.NetworkInformation;
-using System.Reflection;
-using System.Windows.Forms;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer.Screen;
-
-public class MainScreen : Screen {
-    public MainScreen() {
-        NextScreen = new SelectLocationScreen(this);
-        UpPanelVisible = false;
-    }
-
-    public override void OnEnter() {
-        Font descriptionFont = Constants.Arial12;
-        MainPanel.SuspendLayout();
-        MainPanel.Controls.AddRange([
-            // JipperResourceSide
-            new PictureBox {
-                Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.JipperResourceSide.png")!),
-                Size = new Size(403, 561)
-            },
-            
-            // Title
-            new Label {
-                Text = Resources.Current.MainScreen_Title,
-                Font = Constants.Arial24B,
-                Size = new Size(392, 40),
-                Location = new Point(430, 48)
-            },
-            
-            // Descriptions
-            new Label {
-                Text = Resources.Current.MainScreen_Description1,
-                Font = descriptionFont,
-                Size = new Size(517, 18),
-                Location = new Point(435, 140)
-            },
-            new Label {
-                Text = Resources.Current.MainScreen_Description2,
-                Font = descriptionFont,
-                Size = new Size(517, 18),
-                Location = new Point(435, 166)
-            },
-            new Label {
-                Text = Resources.Current.MainScreen_Description3,
-                Font = descriptionFont,
-                Size = new Size(517, 18),
-                Location = new Point(435, 192)
-            },
-            new Label {
-                Text = Resources.Current.MainScreen_Description4,
-                Font = descriptionFont,
-                Size = new Size(517, 36),
-                Location = new Point(435, 218)
-            },
-            new Label {
-                Text = Resources.Current.MainScreen_Description5,
-                Font = descriptionFont,
-                Size = new Size(517, 18),
-                Location = new Point(435, 260)
-            },
-            new Label {
-                Text = Resources.Current.MainScreen_Description6,
-                Font = descriptionFont,
-                Size = new Size(517, 18),
-                Location = new Point(435, 286)
-            },
-            new Label {
-                Text = Resources.Current.MainScreen_Description7,
-                Font = descriptionFont,
-                Size = new Size(517, 18),
-                Location = new Point(435, 312)
-            },
-            
-            // Bug Report
-            new Label {
-                Text = Resources.Current.MainScreen_BugReport,
-                Font = descriptionFont,
-                AutoSize = true,
-                Location = new Point(440, 444)
-            },
-            new LinkedImageButton("https://discord.gg/qTbnPhY7YA", "JipperResourcePack.Installer.Resource.Discord-Symbol-Blurple.png") {
-                Size = new Size(32, 24),
-                Location = new Point(445, 474)
-            },
-            new LinkedImageButton("https://github.com/Jongye0l/JipperResourcePack/issues", "JipperResourcePack.Installer.Resource.GitHub_Invertocat_Black.png") {
-                Size = new Size(32, 31),
-                Location = new Point(485, 470)
-            },
-            new LinkedImageButton("mailto:bcpjy1233@gmail.com", "JipperResourcePack.Installer.Resource.mail.png") {
-                Size = new Size(32, 32),
-                Location = new Point(525, 470)
-            },
-            
-            // Donate
-            new Label {
-                Text = Resources.Current.MainScreen_Donate,
-                Font = descriptionFont,
-                AutoSize = true,
-                Location = new Point(605, 444)
-            },
-            new LinkedImageButton(null, "JipperResourcePack.Installer.Resource.payment_icon_yellow_small.png") {
-                Size = new Size(64, 27),
-                Location = new Point(610, 470)
-            },
-            new LinkedImageButton("https://ko-fi.com/jongyeol", "JipperResourcePack.Installer.Resource.kofi_logo.png") {
-                Size = new Size(87, 24),
-                Location = new Point(682, 470)
-            }
-        ]);
-        MainPanel.ResumeLayout();
-    }
-
-    public override void OnLeave() {
-    }
-
-    public override bool OnNext() {
-        if(IsNetworkAvailable()) return true;
-        MessageBox.Show(Resources.Current.MainScreen_NoInternet, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        return false;
-    }
-
-    public static bool IsNetworkAvailable() {
-        return NetworkInterface.GetAllNetworkInterfaces().Any(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
-                                                                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
-    }
-}
\ No newline at end of file
```

### 삭제 줄 1부터 60까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Linq; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Net.NetworkInformation; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 타입을 선언한다: public class MainScreen : Screen { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public MainScreen() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 오른쪽 결과를 왼쪽 상태에 저장한다: NextScreen = new SelectLocationScreen(this); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: UpPanelVisible = false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnEnter() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font descriptionFont = Constants.Arial12; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 이 함수 호출/식 처리를 실행한다: MainPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 연결된 식·선언·설정의 일부다: MainPanel.Controls.AddRange([ | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 주석: JipperResourceSide | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 연결된 식·선언·설정의 일부다: new PictureBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 오른쪽 결과를 왼쪽 상태에 저장한다: Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("JipperResourcePack.Installer.Resource.JipperResourceSide.png")!), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(403, 561) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 주석: Title | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Title, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial24B, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(392, 40), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(430, 48) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 주석: Descriptions | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description1, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 18), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 140) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description2, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 18), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 166) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description3, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 18), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 192) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description4, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 36), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 218) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description5, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 61부터 120까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 18), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 260) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description6, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 18), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 286) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Description7, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(517, 18), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(435, 312) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 주석: Bug Report | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_BugReport, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(440, 444) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("https://discord.gg/qTbnPhY7YA", "JipperResourcePack.Installer.Resource.Discord-Symbol-Blurple.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(32, 24), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(445, 474) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("https://github.com/Jongye0l/JipperResourcePack/issues", "JipperResourcePack.Installer.Resource.GitHub_Invertocat_Black.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(32, 31), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(485, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("mailto:bcpjy1233@gmail.com", "JipperResourcePack.Installer.Resource.mail.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(32, 32), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(525, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 주석: Donate | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 연결된 식·선언·설정의 일부다: new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.MainScreen_Donate, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = descriptionFont, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(605, 444) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 연결된 식·선언·설정의 일부다: new LinkedImageButton(null, "JipperResourcePack.Installer.Resource.payment_icon_yellow_small.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(64, 27), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(610, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 연결된 식·선언·설정의 일부다: new LinkedImageButton("https://ko-fi.com/jongyeol", "JipperResourcePack.Installer.Resource.kofi_logo.png") { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(87, 24), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(682, 470) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 이 함수 호출/식 처리를 실행한다: ]); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 이 함수 호출/식 처리를 실행한다: MainPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnLeave() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override bool OnNext() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 121부터 130까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 121 | 조건을 만족할 때 뒤의 작업을 수행한다: if(IsNetworkAvailable()) return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(Resources.Current.MainScreen_NoInternet, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 함수를 종료하거나 식의 결과를 반환한다: return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static bool IsNetworkAvailable() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 함수를 종료하거나 식의 결과를 반환한다: return NetworkInterface.GetAllNetworkInterfaces().Any(ni =&gt; ni.OperationalStatus == OperationalStatus.Up &amp;&amp; ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &amp;&amp; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 이 함수 호출/식 처리를 실행한다: ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Screen/Screen.cs

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/Screen/Screen.cs b/Installer/Screen/Screen.cs
deleted file mode 100644
index c80ba1f..0000000
--- a/Installer/Screen/Screen.cs
+++ /dev/null
@@ -1,67 +0,0 @@
-﻿using System.Windows.Forms;
-
-namespace JipperResourcePack.Installer.Screen;
-
-public abstract class Screen {
-    public static Screen CurrentScreen;
-    public Screen PrevScreen;
-    public Screen NextScreen;
-    public bool Cancelable = true;
-    public bool Prevable = true;
-    public bool Nextable = true;
-    public bool UpPanelVisible = true;
-    public static Panel MainPanel;
-    public static Panel TopPanel;
-    public static Panel UnderPanel;
-    public static Button PrevButton;
-    public static Button NextButton;
-    public static Button CancelButton;
-    public static Label[] TopPanelLabels;
-
-    public abstract void OnEnter();
-    public abstract void OnLeave();
-    public virtual bool OnNext() => true;
-
-    public void Enter() {
-        CurrentScreen?.OnLeave();
-        MainPanel.Controls.Clear();
-        TopPanel.Visible = UpPanelVisible;
-        PrevButton.Visible = PrevScreen != null;
-        PrevButton.Enabled = Prevable;
-        NextButton.Enabled = Nextable;
-        CancelButton.Enabled = Cancelable;
-        OnEnter();
-        CurrentScreen = this;
-    }
-
-    public static void SetupButton() {
-        NextButton.Click += NextButton_Click;
-        PrevButton.Click += PrevButton_Click;
-        CancelButton.Click += CancelButton_Click;
-    }
-
-    private static void NextButton_Click(object sender, System.EventArgs e) {
-        if(CurrentScreen.OnNext()) Next();
-    }
-
-    private static void PrevButton_Click(object sender, System.EventArgs e) {
-        Prev();
-    }
-
-    private static void CancelButton_Click(object sender, System.EventArgs e) {
-        Application.Exit();
-    }
-
-    public static void Next() {
-        Screen screen = CurrentScreen.NextScreen;
-        if(screen != null) screen.Enter();
-        else {
-            CurrentScreen.OnLeave();
-            Application.Exit();
-        }
-    }
-
-    public static void Prev() {
-        CurrentScreen.PrevScreen.Enter();
-    }
-}
\ No newline at end of file
```

### 삭제 줄 1부터 60까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 타입을 선언한다: public abstract class Screen { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Screen CurrentScreen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Screen PrevScreen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Screen NextScreen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool Cancelable = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool Prevable = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool Nextable = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public bool UpPanelVisible = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Panel MainPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Panel TopPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Panel UnderPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Button PrevButton; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Button NextButton; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Button CancelButton; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static Label[] TopPanelLabels; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public abstract void OnEnter(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public abstract void OnLeave(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public virtual bool OnNext() =&gt; true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void Enter() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 이 함수 호출/식 처리를 실행한다: CurrentScreen?.OnLeave(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Clear(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanel.Visible = UpPanelVisible; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 오른쪽 결과를 왼쪽 상태에 저장한다: PrevButton.Visible = PrevScreen != null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 오른쪽 결과를 왼쪽 상태에 저장한다: PrevButton.Enabled = Prevable; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 오른쪽 결과를 왼쪽 상태에 저장한다: NextButton.Enabled = Nextable; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: CancelButton.Enabled = Cancelable; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 이 함수 호출/식 처리를 실행한다: OnEnter(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: CurrentScreen = this; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void SetupButton() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 이 함수 호출/식 처리를 실행한다: NextButton.Click += NextButton_Click; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 이 함수 호출/식 처리를 실행한다: PrevButton.Click += PrevButton_Click; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 이 함수 호출/식 처리를 실행한다: CancelButton.Click += CancelButton_Click; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static void NextButton_Click(object sender, System.EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 조건을 만족할 때 뒤의 작업을 수행한다: if(CurrentScreen.OnNext()) Next(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static void PrevButton_Click(object sender, System.EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 이 함수 호출/식 처리를 실행한다: Prev(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static void CancelButton_Click(object sender, System.EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 이 함수 호출/식 처리를 실행한다: Application.Exit(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void Next() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: Screen screen = CurrentScreen.NextScreen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 조건을 만족할 때 뒤의 작업을 수행한다: if(screen != null) screen.Enter(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 앞 조건이 거짓일 때의 처리다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 이 함수 호출/식 처리를 실행한다: CurrentScreen.OnLeave(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 이 함수 호출/식 처리를 실행한다: Application.Exit(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 61부터 67까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 61 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static void Prev() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 이 함수 호출/식 처리를 실행한다: CurrentScreen.PrevScreen.Enter(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Screen/SelectLocationScreen.cs

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/Screen/SelectLocationScreen.cs b/Installer/Screen/SelectLocationScreen.cs
deleted file mode 100644
index 5c5f7c6..0000000
--- a/Installer/Screen/SelectLocationScreen.cs
+++ /dev/null
@@ -1,272 +0,0 @@
-﻿using System;
-using System.Diagnostics;
-using System.Drawing;
-using System.IO;
-using System.Reflection;
-using System.Threading;
-using System.Threading.Tasks;
-using System.Windows.Forms;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer.Screen;
-
-public class SelectLocationScreen : Screen {
-    public TextBox LocationTextBox;
-    public GroupBox NotifyBox;
-    public CancellationTokenSource Cts;
-    public string ErrorString;
-    public RequirementStatus RequirementStatus = new();
-    //public Button AdofaiFolderGuide;
-
-    public SelectLocationScreen(Screen screen) {
-        PrevScreen = screen;
-        NextScreen = new SelectScreen(this, RequirementStatus);
-    }
-
-    public override void OnEnter() {
-        GlobalSetting.Instance.InstallPath ??= Utility.GetAdofaiPath();
-        TopPanelLabels[0].Font = Constants.Arial16B;
-        MainPanel.SuspendLayout();
-
-        // Title
-        Control control = new Label {
-            Text = Resources.Current.SelectLocation_Title,
-            Font = Constants.Arial16,
-            AutoSize = true,
-            Location = new Point(30, 62)
-        };
-        MainPanel.Controls.Add(control);
-
-        // Description
-        control = new Label {
-            Text = Resources.Current.SelectLocation_Description,
-            Font = Constants.Arial13,
-            AutoSize = true,
-            Location = new Point(32, 99)
-        };
-        MainPanel.Controls.Add(control);
-
-        // Location
-        control = new Label {
-            Text = Resources.Current.SelectLocation_Location,
-            Font = Constants.Arial13,
-            AutoSize = true,
-            Location = new Point(35, 230)
-        };
-        MainPanel.Controls.Add(control);
-
-        control = LocationTextBox = new TextBox {
-            Text = GlobalSetting.Instance.InstallPath,
-            Location = new Point(38, 254),
-            Font = Constants.Arial13,
-            Size = new Size(770, 50)
-        };
-        LocationTextBox.TextChanged += (_, _) => UpdateInformation(false);
-        MainPanel.Controls.Add(control);
-
-        control = new Button {
-            Text = Resources.Current.SelectLocation_Select,
-            Location = new Point(818, 254),
-            Font = Constants.Arial13,
-            Size = new Size(130, 30)
-        };
-        control.Click += LocationSelectButton_Click;
-        MainPanel.Controls.Add(control);
-
-        control = NotifyBox = new GroupBox();
-        MainPanel.Controls.Add(control);
-        // AdofaiFolderGuide = new Button {
-        //     Text = Resources.Current.SelectLocation_AdofaiFolderGuide,
-        //     Location = new Point(30, 150),
-        //     Font = Constants.Arial13,
-        //     Size = new Size(150, 30)
-        // };
-        // AdofaiFolderGuide.Click += AdofaiFolderGuide_Click;
-        // MainPanel.Controls.Add(AdofaiFolderGuide);
-        MainPanel.ResumeLayout();
-        UpdateInformation(true);
-    }
-
-    public override void OnLeave() {
-        TopPanelLabels[0].Font = Constants.Arial16;
-    }
-
-    public override bool OnNext() {
-        if(ErrorString == null) return true;
-        MessageBox.Show(ErrorString, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        return false;
-    }
-
-    public void UpdateInformation(bool init) {
-        int count = 0;
-        try {
-            RequirementStatus.Reset();
-            NotifyBox.SuspendLayout();
-            if(Cts != null) {
-                Cts.Cancel();
-                NotifyBox.Controls.Clear();
-            }
-            Cts = new CancellationTokenSource();
-            ErrorString = null;
-
-            Resources resources = Resources.Current;
-            NotifyBox.Controls.Add(new Label {
-                Text = resources.SelectLocation_NotifyTitle,
-                Font = Constants.Arial16B,
-                AutoSize = true,
-                Location = new Point(12, 16)
-            });
-
-            string path = LocationTextBox.Text.Replace("\\", "/");
-            if(string.IsNullOrWhiteSpace(path)) {
-                ErrorString = resources.SelectLocation_NoLocation;
-                CreateNotify(1, resources.SelectLocation_NotifyEmpty, ref count);
-                return;
-            }
-
-            if(!Directory.Exists(path)) {
-                ErrorString = resources.SelectLocation_NoFolder;
-                CreateNotify(1, resources.SelectLocation_NotifyFolderNotExist, ref count);
-                return;
-            }
-            CreateNotify(0, resources.SelectLocation_NotifyFolderExist, ref count);
-
-            if(!CheckAdofaiFolder(path)) {
-                ErrorString = resources.SelectLocation_NoAdofai;
-                CreateNotify(1, resources.SelectLocation_NotifyGameNotFound, ref count);
-                return;
-            }
-            ParseAdofaiVersion(CreateNotify(0, resources.SelectLocation_NotifyGameFound, ref count), init);
-
-            if(!File.Exists(Path.Combine(path, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager", "UnityModManager.dll"))) return;
-            RequirementStatus.IsExistUnityModManager = true;
-            if(!File.Exists(Path.Combine(path, "winhttp.dll"))) {
-                RequirementStatus.IsAssemblyInstalled = true;
-                CreateNotify(2, resources.SelectLocation_NotifyUmmIsAssembly, resources.SelectLocation_NotifyUmmIsAssembly2, ref count);
-                return;
-            }
-
-            FileVersionInfo doorstopVersion = FileVersionInfo.GetVersionInfo(Path.Combine(path, "winhttp.dll"));
-            Version version = Version.Parse(doorstopVersion.FileVersion);
-            if(version < new Version(4, 4, 0, 0)) {
-                RequirementStatus.IsOldDoorStop = true;
-                CreateNotify(2, resources.SelectLocation_NotifyDoorstopIsOld, resources.SelectLocation_NotifyDoorstopIsOld2, ref count);
-            }
-        } finally {
-            int addition = 32 * count;
-            NotifyBox.Location = new Point(38, 492 - addition);
-            NotifyBox.Size = new Size(909, 50 + addition);
-            NotifyBox.ResumeLayout();
-        }
-    }
-
-    private async void ParseAdofaiVersion(Label label, bool init) {
-        try {
-            CancellationToken token = Cts.Token;
-            label.AutoSize = true;
-            int adofaiVersion;
-
-            if(!init || GlobalSetting.Instance.AdofaiRevision == -1) {
-                GlobalSetting.Instance.AdofaiRevision = -1;
-                string path = GlobalSetting.Instance.InstallPath;
-                string dllPath = Path.Combine(path, "A Dance of Fire and Ice_Data", "Managed", "Assembly-CSharp.dll");
-                adofaiVersion = await Task.Run(() => GetAdofaiRevision(dllPath), token); // TODO: Memory Leak. add AppDomain System.
-                if(token.IsCancellationRequested) return;
-                GlobalSetting.Instance.AdofaiRevision = adofaiVersion;
-            } else {
-                adofaiVersion = GlobalSetting.Instance.AdofaiRevision;
-                await Task.Yield(); // Waiting Resume to update label location
-            }
-
-            Label versionLabel = new() {
-                Text = $"(r{adofaiVersion})",
-                Font = Constants.Arial10,
-                ForeColor = Constants.SubColor,
-                AutoSize = true,
-                Location = new Point(label.Location.X + label.Size.Width - 4, label.Location.Y + 2)
-            };
-            NotifyBox.Controls.Add(versionLabel);
-        } catch (Exception e) {
-            MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        }
-    }
-
-    public static int GetAdofaiRevision(string dllPath) {
-        Assembly assembly = Assembly.Load(File.ReadAllBytes(dllPath));
-        Type type = assembly.GetType("GCNS");
-        FieldInfo fieldInfo = type.GetField("releaseNumber", BindingFlags.Public | BindingFlags.Static);
-        if((object) fieldInfo == null) {
-            type = assembly.GetType("Releases");
-            fieldInfo = type.GetField("releaseNumber", BindingFlags.Public | BindingFlags.Static);
-        }
-        return (int) fieldInfo!.GetRawConstantValue();
-    }
-
-    private static bool CheckAdofaiFolder(string path) {
-        string installPath;
-        if(!Directory.Exists(Path.Combine(path, "A Dance of Fire and Ice_Data"))) {
-            if(!path.TrimEnd('/', '\\').EndsWith("A Dance of Fire and Ice_Data")) {
-                if(!Directory.Exists(Path.Combine(path, "A Dance of Fire and Ice", "A Dance of Fire and Ice_Data"))) return false;
-                installPath = Path.Combine(path, "A Dance of Fire and Ice");
-            } else installPath = Directory.GetParent(path)!.FullName;
-        } else installPath = path;
-        if(!File.Exists(Path.Combine(installPath, "A Dance of Fire and Ice.exe"))) return false;
-        GlobalSetting.Instance.InstallPath = installPath;
-        return true;
-    }
-
-    private void CreateNotify(int level, string title, string subTitle, ref int count) {
-        Label label = CreateNotify(level, title, ref count);
-        label.AutoSize = true;
-        Task.Yield().GetAwaiter().UnsafeOnCompleted(() => {
-            try {
-                Label label2 = new() {
-                    Text = subTitle,
-                    Font = Constants.Arial10,
-                    ForeColor = Constants.SubColor,
-                    AutoSize = true,
-                    Location = new Point(label.Location.X + label.Size.Width - 4, label.Location.Y + 2)
-                };
-                NotifyBox.Controls.Add(label2);
-            } catch (Exception e) {
-                Console.WriteLine(e);
-            }
-        });
-    }
-
-    private Label CreateNotify(int level, string title, ref int count) {
-        string iconLocation = "JipperResourcePack.Installer.Resource." + level switch {
-            0 => "check",
-            1 => "close",
-            _ => "warning"
-        } + ".png";
-        PictureBox icon = new() {
-            Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(iconLocation)!),
-            Size = new Size(24, 24),
-            Location = new Point(18, 50 + count * 32)
-        };
-        NotifyBox.Controls.Add(icon);
-        Label label = new() {
-            Text = title,
-            Font = Constants.Arial13,
-            Size = new Size(845, 20),
-            Location = new Point(46, 51 + count * 32)
-        };
-        NotifyBox.Controls.Add(label);
-        count++;
-        return label;
-    }
-
-    public void LocationSelectButton_Click(object sender, EventArgs e) {
-        FolderBrowserDialog folderBrowserDialog = new();
-        if(folderBrowserDialog.ShowDialog() == DialogResult.OK)
-            LocationTextBox.Text = GlobalSetting.Instance.InstallPath = folderBrowserDialog.SelectedPath;
-    }
-
-    //public void AdofaiFolderGuide_Click(object sender, EventArgs e) {
-    //    Process.Start(new ProcessStartInfo {
-    //        FileName = "https://jongyeol.kr/guide/adofai-folder",
-    //        UseShellExecute = true
-    //    });
-    //}
-}
\ No newline at end of file
```

### 삭제 줄 1부터 60까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Diagnostics; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.IO; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: System.Reflection; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | namespace 또는 타입 별칭을 가져온다: System.Threading; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | namespace 또는 타입 별칭을 가져온다: System.Threading.Tasks; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 타입을 선언한다: public class SelectLocationScreen : Screen { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public TextBox LocationTextBox; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public GroupBox NotifyBox; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CancellationTokenSource Cts; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public string ErrorString; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RequirementStatus RequirementStatus = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 주석: public Button AdofaiFolderGuide; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public SelectLocationScreen(Screen screen) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 오른쪽 결과를 왼쪽 상태에 저장한다: PrevScreen = screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 오른쪽 결과를 왼쪽 상태에 저장한다: NextScreen = new SelectScreen(this, RequirementStatus); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnEnter() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallPath ??= Utility.GetAdofaiPath(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanelLabels[0].Font = Constants.Arial16B; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 이 함수 호출/식 처리를 실행한다: MainPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 주석: Title | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 오른쪽 결과를 왼쪽 상태에 저장한다: Control control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.SelectLocation_Title, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial16, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(30, 62) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 주석: Description | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.SelectLocation_Description, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(32, 99) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 주석: Location | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.SelectLocation_Location, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(35, 230) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = LocationTextBox = new TextBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = GlobalSetting.Instance.InstallPath, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(38, 254), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 61부터 120까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 61 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(770, 50) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 이 함수 호출/식 처리를 실행한다: LocationTextBox.TextChanged += (_, _) =&gt; UpdateInformation(false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Button { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.SelectLocation_Select, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(818, 254), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(130, 30) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 이 함수 호출/식 처리를 실행한다: control.Click += LocationSelectButton_Click; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = NotifyBox = new GroupBox(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 주석: AdofaiFolderGuide = new Button { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 주석:     Text = Resources.Current.SelectLocation_AdofaiFolderGuide, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 주석:     Location = new Point(30, 150), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 주석:     Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 주석:     Size = new Size(150, 30) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 주석: }; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 주석: AdofaiFolderGuide.Click += AdofaiFolderGuide_Click; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 주석: MainPanel.Controls.Add(AdofaiFolderGuide); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 이 함수 호출/식 처리를 실행한다: MainPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 이 함수 호출/식 처리를 실행한다: UpdateInformation(true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnLeave() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanelLabels[0].Font = Constants.Arial16; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override bool OnNext() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 조건을 만족할 때 뒤의 작업을 수행한다: if(ErrorString == null) return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(ErrorString, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 함수를 종료하거나 식의 결과를 반환한다: return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void UpdateInformation(bool init) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 오른쪽 결과를 왼쪽 상태에 저장한다: int count = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 이 함수 호출/식 처리를 실행한다: RequirementStatus.Reset(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 이 함수 호출/식 처리를 실행한다: NotifyBox.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 조건을 만족할 때 뒤의 작업을 수행한다: if(Cts != null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 이 함수 호출/식 처리를 실행한다: Cts.Cancel(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 이 함수 호출/식 처리를 실행한다: NotifyBox.Controls.Clear(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 오른쪽 결과를 왼쪽 상태에 저장한다: Cts = new CancellationTokenSource(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 오른쪽 결과를 왼쪽 상태에 저장한다: ErrorString = null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 오른쪽 결과를 왼쪽 상태에 저장한다: Resources resources = Resources.Current; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 연결된 식·선언·설정의 일부다: NotifyBox.Controls.Add(new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.SelectLocation_NotifyTitle, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial16B, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(12, 16) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | callback/호출 본문을 닫고 등록 또는 호출을 마친다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 오른쪽 결과를 왼쪽 상태에 저장한다: string path = LocationTextBox.Text.Replace("\\", "/"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 121부터 180까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 121 | 조건을 만족할 때 뒤의 작업을 수행한다: if(string.IsNullOrWhiteSpace(path)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 오른쪽 결과를 왼쪽 상태에 저장한다: ErrorString = resources.SelectLocation_NoLocation; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 이 함수 호출/식 처리를 실행한다: CreateNotify(1, resources.SelectLocation_NotifyEmpty, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 함수를 종료하거나 식의 결과를 반환한다: return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!Directory.Exists(path)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 오른쪽 결과를 왼쪽 상태에 저장한다: ErrorString = resources.SelectLocation_NoFolder; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 이 함수 호출/식 처리를 실행한다: CreateNotify(1, resources.SelectLocation_NotifyFolderNotExist, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 함수를 종료하거나 식의 결과를 반환한다: return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 132 | 이 함수 호출/식 처리를 실행한다: CreateNotify(0, resources.SelectLocation_NotifyFolderExist, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 133 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 134 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!CheckAdofaiFolder(path)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 135 | 오른쪽 결과를 왼쪽 상태에 저장한다: ErrorString = resources.SelectLocation_NoAdofai; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 136 | 이 함수 호출/식 처리를 실행한다: CreateNotify(1, resources.SelectLocation_NotifyGameNotFound, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 137 | 함수를 종료하거나 식의 결과를 반환한다: return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 138 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 139 | 이 함수 호출/식 처리를 실행한다: ParseAdofaiVersion(CreateNotify(0, resources.SelectLocation_NotifyGameFound, ref count), init); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 140 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 141 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!File.Exists(Path.Combine(path, "A Dance of Fire and Ice_Data", "Managed", "UnityModManager", "UnityModManager.dll"))) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 142 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus.IsExistUnityModManager = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 143 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!File.Exists(Path.Combine(path, "winhttp.dll"))) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 144 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus.IsAssemblyInstalled = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 145 | 이 함수 호출/식 처리를 실행한다: CreateNotify(2, resources.SelectLocation_NotifyUmmIsAssembly, resources.SelectLocation_NotifyUmmIsAssembly2, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 146 | 함수를 종료하거나 식의 결과를 반환한다: return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 147 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 148 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 149 | 오른쪽 결과를 왼쪽 상태에 저장한다: FileVersionInfo doorstopVersion = FileVersionInfo.GetVersionInfo(Path.Combine(path, "winhttp.dll")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 150 | 오른쪽 결과를 왼쪽 상태에 저장한다: Version version = Version.Parse(doorstopVersion.FileVersion); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 151 | 조건을 만족할 때 뒤의 작업을 수행한다: if(version &lt; new Version(4, 4, 0, 0)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 152 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus.IsOldDoorStop = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 153 | 이 함수 호출/식 처리를 실행한다: CreateNotify(2, resources.SelectLocation_NotifyDoorstopIsOld, resources.SelectLocation_NotifyDoorstopIsOld2, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 154 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 155 | 연결된 식·선언·설정의 일부다: } finally { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 156 | 오른쪽 결과를 왼쪽 상태에 저장한다: int addition = 32 * count; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 157 | 오른쪽 결과를 왼쪽 상태에 저장한다: NotifyBox.Location = new Point(38, 492 - addition); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 158 | 오른쪽 결과를 왼쪽 상태에 저장한다: NotifyBox.Size = new Size(909, 50 + addition); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 159 | 이 함수 호출/식 처리를 실행한다: NotifyBox.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 160 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 161 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 162 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 163 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private async void ParseAdofaiVersion(Label label, bool init) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 164 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 165 | 오른쪽 결과를 왼쪽 상태에 저장한다: CancellationToken token = Cts.Token; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 166 | 오른쪽 결과를 왼쪽 상태에 저장한다: label.AutoSize = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 167 | 이 함수 호출/식 처리를 실행한다: int adofaiVersion; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 168 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 169 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!init \|\| GlobalSetting.Instance.AdofaiRevision == -1) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 170 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.AdofaiRevision = -1; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 171 | 오른쪽 결과를 왼쪽 상태에 저장한다: string path = GlobalSetting.Instance.InstallPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 172 | 오른쪽 결과를 왼쪽 상태에 저장한다: string dllPath = Path.Combine(path, "A Dance of Fire and Ice_Data", "Managed", "Assembly-CSharp.dll"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 173 | 오른쪽 결과를 왼쪽 상태에 저장한다: adofaiVersion = await Task.Run(() =&gt; GetAdofaiRevision(dllPath), token); // TODO: Memory Leak. add AppDomain System. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 174 | 조건을 만족할 때 뒤의 작업을 수행한다: if(token.IsCancellationRequested) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 175 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.AdofaiRevision = adofaiVersion; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 176 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 177 | 오른쪽 결과를 왼쪽 상태에 저장한다: adofaiVersion = GlobalSetting.Instance.AdofaiRevision; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 178 | 연결된 식·선언·설정의 일부다: await Task.Yield(); // Waiting Resume to update label location | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 179 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 180 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 181부터 240까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 181 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label versionLabel = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 182 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = $"(r{adofaiVersion})", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 183 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial10, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 184 | 오른쪽 결과를 왼쪽 상태에 저장한다: ForeColor = Constants.SubColor, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 185 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 186 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(label.Location.X + label.Size.Width - 4, label.Location.Y + 2) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 187 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 188 | 이 함수 호출/식 처리를 실행한다: NotifyBox.Controls.Add(versionLabel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 189 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 190 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 191 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 192 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 193 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 194 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static int GetAdofaiRevision(string dllPath) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 195 | 오른쪽 결과를 왼쪽 상태에 저장한다: Assembly assembly = Assembly.Load(File.ReadAllBytes(dllPath)); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 196 | 오른쪽 결과를 왼쪽 상태에 저장한다: Type type = assembly.GetType("GCNS"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 197 | 오른쪽 결과를 왼쪽 상태에 저장한다: FieldInfo fieldInfo = type.GetField("releaseNumber", BindingFlags.Public \| BindingFlags.Static); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 198 | 조건을 만족할 때 뒤의 작업을 수행한다: if((object) fieldInfo == null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 199 | 오른쪽 결과를 왼쪽 상태에 저장한다: type = assembly.GetType("Releases"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 200 | 오른쪽 결과를 왼쪽 상태에 저장한다: fieldInfo = type.GetField("releaseNumber", BindingFlags.Public \| BindingFlags.Static); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 201 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 202 | 함수를 종료하거나 식의 결과를 반환한다: return (int) fieldInfo!.GetRawConstantValue(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 203 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 204 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 205 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static bool CheckAdofaiFolder(string path) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 206 | 이 함수 호출/식 처리를 실행한다: string installPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 207 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!Directory.Exists(Path.Combine(path, "A Dance of Fire and Ice_Data"))) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 208 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!path.TrimEnd('/', '\\').EndsWith("A Dance of Fire and Ice_Data")) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 209 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!Directory.Exists(Path.Combine(path, "A Dance of Fire and Ice", "A Dance of Fire and Ice_Data"))) return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 210 | 오른쪽 결과를 왼쪽 상태에 저장한다: installPath = Path.Combine(path, "A Dance of Fire and Ice"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 211 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 212 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 213 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!File.Exists(Path.Combine(installPath, "A Dance of Fire and Ice.exe"))) return false; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 214 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallPath = installPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 215 | 함수를 종료하거나 식의 결과를 반환한다: return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 216 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 217 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 218 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void CreateNotify(int level, string title, string subTitle, ref int count) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 219 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label label = CreateNotify(level, title, ref count); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 220 | 오른쪽 결과를 왼쪽 상태에 저장한다: label.AutoSize = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 221 | 연결된 식·선언·설정의 일부다: Task.Yield().GetAwaiter().UnsafeOnCompleted(() =&gt; { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 222 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 223 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label label2 = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 224 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = subTitle, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 225 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial10, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 226 | 오른쪽 결과를 왼쪽 상태에 저장한다: ForeColor = Constants.SubColor, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 227 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 228 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(label.Location.X + label.Size.Width - 4, label.Location.Y + 2) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 229 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 230 | 이 함수 호출/식 처리를 실행한다: NotifyBox.Controls.Add(label2); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 231 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 232 | 작업/검사 결과를 출력한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 233 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 234 | callback/호출 본문을 닫고 등록 또는 호출을 마친다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 235 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 236 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 237 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private Label CreateNotify(int level, string title, ref int count) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 238 | 오른쪽 결과를 왼쪽 상태에 저장한다: string iconLocation = "JipperResourcePack.Installer.Resource." + level switch { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 239 | 연결된 식·선언·설정의 일부다: 0 =&gt; "check", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 240 | 연결된 식·선언·설정의 일부다: 1 =&gt; "close", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 241부터 272까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 241 | 연결된 식·선언·설정의 일부다: _ =&gt; "warning" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 242 | 이 함수 호출/식 처리를 실행한다: } + ".png"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 243 | 오른쪽 결과를 왼쪽 상태에 저장한다: PictureBox icon = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 244 | 오른쪽 결과를 왼쪽 상태에 저장한다: Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(iconLocation)!), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 245 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(24, 24), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 246 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(18, 50 + count * 32) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 247 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 248 | 이 함수 호출/식 처리를 실행한다: NotifyBox.Controls.Add(icon); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 249 | 오른쪽 결과를 왼쪽 상태에 저장한다: Label label = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 250 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = title, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 251 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 252 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(845, 20), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 253 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(46, 51 + count * 32) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 254 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 255 | 이 함수 호출/식 처리를 실행한다: NotifyBox.Controls.Add(label); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 256 | 이 함수 호출/식 처리를 실행한다: count++; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 257 | 함수를 종료하거나 식의 결과를 반환한다: return label; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 258 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 259 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 260 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public void LocationSelectButton_Click(object sender, EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 261 | 오른쪽 결과를 왼쪽 상태에 저장한다: FolderBrowserDialog folderBrowserDialog = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 262 | 조건을 만족할 때 뒤의 작업을 수행한다: if(folderBrowserDialog.ShowDialog() == DialogResult.OK) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 263 | 오른쪽 결과를 왼쪽 상태에 저장한다: LocationTextBox.Text = GlobalSetting.Instance.InstallPath = folderBrowserDialog.SelectedPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 264 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 265 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 266 | 주석: public void AdofaiFolderGuide_Click(object sender, EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 267 | 주석:    Process.Start(new ProcessStartInfo { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 268 | 주석:        FileName = "https://jongyeol.kr/guide/adofai-folder", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 269 | 주석:        UseShellExecute = true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 270 | 주석:    }); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 271 | 주석: } | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 272 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Screen/SelectScreen.cs

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/Screen/SelectScreen.cs b/Installer/Screen/SelectScreen.cs
deleted file mode 100644
index dcb7f70..0000000
--- a/Installer/Screen/SelectScreen.cs
+++ /dev/null
@@ -1,467 +0,0 @@
-﻿using System;
-using System.Collections.Concurrent;
-using System.Collections.Generic;
-using System.Drawing;
-using System.IO;
-using System.Threading;
-using System.Threading.Tasks;
-using System.Windows.Forms;
-using System.Web.Script.Serialization;
-using JipperResourcePack.Installer.Resource;
-
-namespace JipperResourcePack.Installer.Screen;
-
-public class SelectScreen : Screen {
-    public RadioButton WorkInstallRadio;
-    public RadioButton WorkUninstallRadio;
-
-    public Panel ContentsPanel;
-
-    // Install
-    public CheckBox RequirementUnityModManager;
-    public CheckBox RequirementJalib;
-    public CheckBox RequirementDoorstop;
-    public CheckBox RequirementJipperResourcePack;
-
-    public GroupBox ModListGroup;
-    public Panel ModListPanel;
-
-    public CheckBox[] AdditionMods;
-    
-    // Uninstall
-    public RadioButton UninstallAll;
-    public RadioButton UninstallManager;
-    public RadioButton UninstallMod;
-    
-    // All
-    public List<string> ExistModsPath = [];
-    public List<CheckBox> ExistMods = [];
-    public int ExistModHeight;
-    public CancellationTokenSource ExistModsCancellationTokenSource;
-
-    public RequirementStatus RequirementStatus;
-
-    public SelectScreen(Screen screen, RequirementStatus requirementStatus) {
-        PrevScreen = screen;
-        NextScreen = new InstallScreen(this);
-        RequirementStatus = requirementStatus;
-    }
-
-    public override void OnEnter() {
-        RequirementStatus.IsExistJALib = File.Exists(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JALib", "JALib.dll"));
-        RequirementStatus.IsExistJipperResourcePack = File.Exists(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JipperResourcePack", "JipperResourcePack.dll"));
-        RequirementStatus.ExistMods = Directory.Exists(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods"));
-        TopPanelLabels[1].Font = Constants.Arial16B;
-        if(!GlobalSetting.Instance.SelectSaved) { 
-            GlobalSetting.Instance.InstallDoorstop = !RequirementStatus.IsExistUnityModManager || RequirementStatus.IsAssemblyInstalled || RequirementStatus.IsOldDoorStop;
-            GlobalSetting.Instance.InstallJipperResourcePack = true;
-        }
-        
-        Resources resources = Resources.Current;
-        MainPanel.SuspendLayout();
-        // Title
-        Control control = new Label {
-            Text = resources.Select_Title,
-            Font = Constants.Arial16,
-            AutoSize = true,
-            Location = new Point(30, 62)
-        };
-        MainPanel.Controls.Add(control);
-
-        // Work
-        control = new Label {
-            Text = resources.Select_Work,
-            Font = Constants.Arial13,
-            AutoSize = true,
-            Location = new Point(34, 103)
-        };
-        MainPanel.Controls.Add(control);
-
-        Panel panel = new() {
-            Location = new Point(40, 125),
-            Size = new Size(910, 22)
-        };
-
-        control = WorkInstallRadio = new RadioButton {
-            Text = resources.Select_Work_Install,
-            Font = Constants.Arial13,
-            AutoSize = true,
-            Location = new Point(0, 0),
-            Checked = true
-        };
-        panel.Controls.Add(control);
-
-        control = WorkUninstallRadio = new RadioButton {
-            Text = resources.Select_Work_Uninstall,
-            Font = Constants.Arial13,
-            AutoSize = true,
-            Location = new Point(80, 0),
-            Enabled = RequirementStatus.IsExistUnityModManager || RequirementStatus.ExistMods
-        };
-        WorkUninstallRadio.CheckedChanged += (_, _) => {
-            SaveData(WorkUninstallRadio.Checked);
-            UpdateUI(!WorkUninstallRadio.Checked, false);
-        };
-        panel.Controls.Add(control);
-        
-        MainPanel.Controls.Add(panel);
-
-        panel = ContentsPanel = new Panel {
-            Location = new Point(34, 169),
-            Size = new Size(916, 360)
-        };
-        MainPanel.Controls.Add(panel);
-        
-        MainPanel.ResumeLayout();
-        UpdateUI(!GlobalSetting.Instance.IsUninstall, true);
-    }
-
-    private void UpdateUI(bool install, bool init) {
-        ContentsPanel.SuspendLayout();
-        ContentsPanel.Controls.Clear();
-        Resources resources = Resources.Current;
-
-        if(install) {
-            // Requirement
-            Control control = new Label {
-                Text = resources.Select_Requirement,
-                Font = Constants.Arial13,
-                AutoSize = true,
-                Location = new Point(0, 0)
-            };
-            ContentsPanel.Controls.Add(control);
-
-            Panel panel = new() {
-                Location = new Point(6, 28),
-                Size = new Size(910, 22)
-            };
-
-            panel.Click += RequirementPanelOnClick;
-            control = RequirementUnityModManager = new CheckBox {
-                Text = "UnityModManager",
-                Font = Constants.Arial11,
-                AutoSize = true,
-                Location = new Point(0, 0),
-                Checked = !RequirementStatus.IsExistUnityModManager || GlobalSetting.Instance.InstallUnityModManager,
-                Enabled = RequirementStatus.IsExistUnityModManager
-            };
-            panel.Controls.Add(control);
-
-            control = RequirementDoorstop = new CheckBox {
-                Text = "Doorstop",
-                Font = Constants.Arial11,
-                AutoSize = true,
-                Location = new Point(177, 0),
-                Checked = RequirementUnityModManager.Checked || GlobalSetting.Instance.InstallDoorstop,
-                Enabled = RequirementStatus.IsExistUnityModManager
-            };
-            RequirementDoorstop.CheckedChanged += RequirementDoorstopOnCheckedChanged;
-            panel.Controls.Add(control);
-
-            control = RequirementJalib = new CheckBox {
-                Text = "JALib",
-                Font = Constants.Arial11,
-                AutoSize = true,
-                Location = new Point(293, 0),
-                Checked = !RequirementStatus.IsExistJALib || GlobalSetting.Instance.InstallJalib,
-                Enabled = RequirementStatus.IsExistJALib
-            };
-            panel.Controls.Add(control);
-
-            control = RequirementJipperResourcePack = new CheckBox {
-                Text = "JipperResourcePack",
-                Font = Constants.Arial11,
-                AutoSize = true,
-                Location = new Point(384, 0),
-                Checked = GlobalSetting.Instance.InstallJipperResourcePack || !RequirementStatus.IsExistJipperResourcePack,
-                Enabled = RequirementStatus.IsExistJipperResourcePack
-            };
-            panel.Controls.Add(control);
-
-            ContentsPanel.Controls.Add(panel);
-
-            // Addition Mods
-            control = new Label {
-                Text = resources.Select_AdditionMods,
-                Font = Constants.Arial13,
-                AutoSize = true,
-                Location = new Point(0, 71)
-            };
-            ContentsPanel.Controls.Add(control);
-        } else {
-            // Uninstall Option
-            Control control = new Label {
-                Text = resources.Select_UninstallOption,
-                Font = Constants.Arial13,
-                AutoSize = true,
-                Location = new Point(0, 0)
-            };
-            ContentsPanel.Controls.Add(control);
-
-            Panel panel = new() {
-                Location = new Point(6, 28),
-                Size = new Size(910, 82)
-            };
-            
-            control = UninstallAll = new RadioButton {
-                Text = resources.Select_UninstallOption_All,
-                Font = Constants.Arial13,
-                Size = new Size(85, 22),
-                Location = new Point(0, 0),
-                Checked = GlobalSetting.Instance.UninstallOption == 0
-            };
-            panel.Controls.Add(control);
-
-            control = new Label {
-                Text = resources.Select_UninstallOption_AllDescription,
-                Font = Constants.Arial11,
-                ForeColor = Constants.SubColor,
-                AutoSize = true,
-                Location = new Point(82, 5)
-            };
-            panel.Controls.Add(control);
-            
-            control = UninstallManager = new RadioButton {
-                Text = resources.Select_UninstallOption_OnlyManager,
-                Font = Constants.Arial13,
-                Size = new Size(143, 22),
-                Location = new Point(0, 30),
-                Checked = GlobalSetting.Instance.UninstallOption == 1,
-                Enabled = RequirementStatus.IsExistUnityModManager
-            };
-            panel.Controls.Add(control);
-            
-            control = new Label {
-                Text = resources.Select_UninstallOption_OnlyManagerDescription,
-                Font = Constants.Arial11,
-                ForeColor = Constants.SubColor,
-                AutoSize = true,
-                Location = new Point(140, 35)
-            };
-            panel.Controls.Add(control);
-
-            control = UninstallMod = new RadioButton {
-                Text = resources.Select_UninstallOption_OnlyMod,
-                Font = Constants.Arial13,
-                Size = new Size(129, 22),
-                Location = new Point(0, 60),
-                Checked = GlobalSetting.Instance.UninstallOption == 2,
-                Enabled = RequirementStatus.ExistMods
-            };
-            UninstallMod.CheckedChanged += (_, _) => {
-                ModListGroup.Visible = UninstallMod.Checked || WorkInstallRadio.Checked;
-            };
-            panel.Controls.Add(control);
-
-            control = new Label {
-                Text = resources.Select_UninstallOption_OnlyModDescription,
-                Font = Constants.Arial11,
-                ForeColor = Constants.SubColor,
-                AutoSize = true,
-                Location = new Point(126, 65)
-            };
-            panel.Controls.Add(control);
-
-            ContentsPanel.Controls.Add(panel);
-            ContentsPanel.Size = new Size(916, 134);
-        }
-        EnterAsyncWork(install, init);
-        ContentsPanel.ResumeLayout();
-    }
-
-    private async void EnterAsyncWork(bool install, bool init) {
-        try {
-            ConcurrentQueue<string> modItemQueue;
-            Task task;
-            if(init && RequirementStatus.ExistMods) {
-                ExistModsCancellationTokenSource = new CancellationTokenSource();
-                modItemQueue = new ConcurrentQueue<string>();
-                task = Task.Run(() => {
-                    try {
-                        CancellationToken token = ExistModsCancellationTokenSource.Token;
-                        foreach(string file in Directory.GetDirectories(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods"))) {
-                            string infoLocation = Path.Combine(file, "Info.json");
-                            if(!File.Exists(infoLocation)) infoLocation = Path.Combine(file, "info.json");
-                            if(!File.Exists(infoLocation)) continue;
-
-                            ModInfo modInfo = Utility.JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(infoLocation));
-                            if(token.IsCancellationRequested) return;
-                            ExistModsPath.Add(file);
-                            modItemQueue.Enqueue(modInfo.Id + "(By " + modInfo.Author.Replace(" & ", ", ").Replace("&", ", ") + ")");
-                        }
-                    } finally {
-                        modItemQueue.Enqueue(null);
-                    }
-                }, ExistModsCancellationTokenSource.Token);
-            } else {
-                modItemQueue = null;
-                task = Task.CompletedTask;
-            }
-
-            if(install) {
-                int line = 0;
-                try {
-                    ModData[] mods = await GlobalSetting.Instance.AdditionMods;
-                    line = (mods.Length + 2) / 3;
-                    if(line != 0) {
-                        Panel panel = new() {
-                            Location = new Point(6, 96),
-                            Size = new Size(910, -4 + 25 * line)
-                        };
-
-                        AdditionMods = new CheckBox[mods.Length];
-                        List<ModData> selectedMods = GlobalSetting.Instance.SelectedMods;
-                        for(int i = 0; i < mods.Length; i++) {
-                            AdditionMods[i] = new CheckBox {
-                                Text = mods[i].DisplayName,
-                                Font = Constants.Arial11,
-                                Size = new Size(303, 25),
-                                Location = new Point(i % 3 * 303, i / 3 * 25),
-                                Checked = selectedMods?.Contains(mods[i]) == true
-                            };
-                            panel.Controls.Add(AdditionMods[i]);
-                        }
-
-                        ContentsPanel.Controls.Add(panel);
-                    }
-                } catch (Exception e) {
-                    Console.WriteLine(e);
-                    MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-                }
-                ContentsPanel.Size = new Size(916, 114 + 25 * line);
-            }
-
-            // Remove Mods
-            if(!RequirementStatus.ExistMods) return;
-
-            if(init) {
-                ModListGroup = new GroupBox {
-                    Text = Resources.Current.Select_RemoveMods,
-                    Font = Constants.Arial13,
-                    Location = new Point(32, 169 + ContentsPanel.Size.Height)
-                };
-                ModListGroup.Size = new Size(920, 539 - ModListGroup.Location.Y);
-            
-                ModListPanel = new Panel {
-                    Location = new Point(10, 25),
-                    Size = ModListGroup.Size - new Size(11, 30),
-                    AutoScroll = true
-                };
-
-                List<string> removeMods = GlobalSetting.Instance.RemoveRequestMods;
-
-                while(true) {
-                    if(modItemQueue!.TryDequeue(out string item)) {
-                        if(item == null) break;
-                        CheckBox checkBox = new() {
-                            Text = item,
-                            Font = Constants.Arial11,
-                            Size = new Size(295, 25),
-                            Location = new Point(ExistMods.Count % 3 * 295, ExistMods.Count / 3 * 25),
-                            Checked = removeMods?.Contains(ExistModsPath[ExistMods.Count]) == true
-                        };
-                        ExistMods.Add(checkBox);
-                        ModListPanel.Controls.Add(checkBox);
-                    } else await Task.Yield();
-                }
-
-                await task;
-            
-                int calY = 31 + (ExistMods.Count + 2) / 3 * 25;
-                ExistModHeight = calY;
-                if(calY < ModListGroup.Size.Height) {
-                    ModListGroup.Size = ModListGroup.Size with {
-                        Height = calY
-                    };
-                    ModListPanel.Size = ModListPanel.Size with {
-                        Height = calY - 30
-                    };
-                }
-            
-                ModListGroup.Controls.Add(ModListPanel);
-                MainPanel.Controls.Add(ModListGroup);
-            } else {
-                ModListGroup.Visible = install || UninstallMod.Checked;
-                ModListGroup.Location = new Point(32, 169 + ContentsPanel.Size.Height);
-                int calY = 539 - ModListGroup.Location.Y;
-                ModListGroup.Size = new Size(920, Math.Min(calY, ExistModHeight));
-
-                ModListPanel.Size = ModListGroup.Size - new Size(11, 30);
-            }
-        } catch (Exception e) {
-            Console.WriteLine(e);
-            MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        }
-    }
-
-    private void SaveData(bool isInstall) {
-        if(isInstall) {
-            GlobalSetting.Instance.InstallUnityModManager = RequirementUnityModManager.Checked;
-            GlobalSetting.Instance.InstallDoorstop = RequirementDoorstop.Checked;
-            GlobalSetting.Instance.InstallJalib = RequirementJalib.Checked;
-            GlobalSetting.Instance.InstallJipperResourcePack = RequirementJipperResourcePack.Checked;
-            List<ModData> selectedMods = [];
-            ModData[] mods = GlobalSetting.Instance.AdditionMods.Result;
-            for(int i = 0; i < AdditionMods.Length; i++)
-                if(AdditionMods[i].Checked)
-                    selectedMods.Add(mods[i]);
-            GlobalSetting.Instance.SelectedMods = selectedMods;
-        } else GlobalSetting.Instance.UninstallOption = UninstallAll.Checked ? 0 : UninstallManager.Checked ? 1 : 2;
-    }
-
-    private void RequirementDoorstopOnCheckedChanged(object sender, EventArgs e) {
-        if(RequirementDoorstop.Checked) return;
-        if(RequirementStatus.IsAssemblyInstalled) {
-            DialogResult result = MessageBox.Show(Resources.Current.Select_Requirement_Doorstop_Asm, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
-            if(result == DialogResult.No) RequirementDoorstop.Checked = true;
-        } else if(RequirementStatus.IsOldDoorStop) {
-            DialogResult result = MessageBox.Show(Resources.Current.Select_Requirement_Doorstop_Old, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
-            if(result == DialogResult.No) RequirementDoorstop.Checked = true;
-        }
-    }
-
-    private void RequirementPanelOnClick(object sender, EventArgs e) {
-        if(!RequirementUnityModManager.Enabled && RequirementUnityModManager.Bounds.Contains(((MouseEventArgs) e).Location))
-            MessageBox.Show(Resources.Current.Select_Requirement_UnityModManager, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        else if(!RequirementDoorstop.Enabled && RequirementDoorstop.Bounds.Contains(((MouseEventArgs) e).Location))
-            MessageBox.Show(Resources.Current.Select_Requirement_Doorstop_Umm, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        else if(!RequirementJalib.Enabled && RequirementJalib.Bounds.Contains(((MouseEventArgs) e).Location))
-            MessageBox.Show(Resources.Current.Select_Requirement_JALib, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-        else if(!RequirementJipperResourcePack.Enabled && RequirementJipperResourcePack.Bounds.Contains(((MouseEventArgs) e).Location))
-            MessageBox.Show(Resources.Current.Select_Requirement_JipperResourcePack, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
-    }
-
-    public override bool OnNext() {
-        if(WorkUninstallRadio.Checked) {
-            if(UninstallAll.Checked) {
-                DialogResult result = MessageBox.Show(Resources.Current.Select_RemoveMods_Confirm, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
-                return result != DialogResult.No;
-            }
-            if(UninstallManager.Checked) return true;
-        }
-        foreach(CheckBox checkBox in ExistMods) {
-            if(!checkBox.Checked) continue;
-            DialogResult result = MessageBox.Show(Resources.Current.Select_RemoveMods_Confirm, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
-            return result != DialogResult.No;
-        }
-        return true;
-    }
-
-    public override void OnLeave() {
-        ExistModsCancellationTokenSource?.Cancel();
-        
-        SaveData(WorkInstallRadio.Checked);
-        List<string> removeRequestMods = [];
-        for(int i = 0; i < ExistMods.Count; i++)
-            if(ExistMods[i].Checked)
-                removeRequestMods.Add(ExistModsPath[i]);
-
-        GlobalSetting.Instance.RemoveRequestMods = removeRequestMods;
-        GlobalSetting.Instance.IsUninstall = WorkUninstallRadio.Checked;
-        GlobalSetting.Instance.SelectSaved = true;
-        ExistModsPath.Clear();
-        ExistMods.Clear();
-        
-        TopPanelLabels[1].Font = Constants.Arial16;
-    }
-}
```

### 삭제 줄 1부터 60까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.Collections.Concurrent; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Collections.Generic; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: System.Drawing; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | namespace 또는 타입 별칭을 가져온다: System.IO; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | namespace 또는 타입 별칭을 가져온다: System.Threading; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | namespace 또는 타입 별칭을 가져온다: System.Threading.Tasks; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | namespace 또는 타입 별칭을 가져온다: System.Windows.Forms; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | namespace 또는 타입 별칭을 가져온다: System.Web.Script.Serialization; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | namespace 또는 타입 별칭을 가져온다: JipperResourcePack.Installer.Resource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer.Screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 타입을 선언한다: public class SelectScreen : Screen { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RadioButton WorkInstallRadio; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RadioButton WorkUninstallRadio; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Panel ContentsPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 주석: Install | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CheckBox RequirementUnityModManager; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CheckBox RequirementJalib; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CheckBox RequirementDoorstop; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CheckBox RequirementJipperResourcePack; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public GroupBox ModListGroup; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public Panel ModListPanel; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CheckBox[] AdditionMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 30 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 31 | 주석: Uninstall | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 32 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RadioButton UninstallAll; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 33 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RadioButton UninstallManager; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 34 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RadioButton UninstallMod; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 35 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 36 | 주석: All | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 37 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public List&lt;string&gt; ExistModsPath = []; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 38 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public List&lt;CheckBox&gt; ExistMods = []; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 39 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public int ExistModHeight; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 40 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public CancellationTokenSource ExistModsCancellationTokenSource; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 41 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 42 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public RequirementStatus RequirementStatus; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 43 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 44 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public SelectScreen(Screen screen, RequirementStatus requirementStatus) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 45 | 오른쪽 결과를 왼쪽 상태에 저장한다: PrevScreen = screen; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 46 | 오른쪽 결과를 왼쪽 상태에 저장한다: NextScreen = new InstallScreen(this); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 47 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus = requirementStatus; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 48 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 49 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 50 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnEnter() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 51 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus.IsExistJALib = File.Exists(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JALib", "JALib.dll")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 52 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus.IsExistJipperResourcePack = File.Exists(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods", "JipperResourcePack", "JipperResourcePack.dll")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 53 | 오른쪽 결과를 왼쪽 상태에 저장한다: RequirementStatus.ExistMods = Directory.Exists(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 54 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanelLabels[1].Font = Constants.Arial16B; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 55 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!GlobalSetting.Instance.SelectSaved) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 56 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallDoorstop = !RequirementStatus.IsExistUnityModManager \|\| RequirementStatus.IsAssemblyInstalled \|\| RequirementStatus.IsOldDoorStop; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 57 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallJipperResourcePack = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 58 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 59 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 60 | 오른쪽 결과를 왼쪽 상태에 저장한다: Resources resources = Resources.Current; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 61부터 120까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 61 | 이 함수 호출/식 처리를 실행한다: MainPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 62 | 주석: Title | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 63 | 오른쪽 결과를 왼쪽 상태에 저장한다: Control control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 64 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_Title, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 65 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial16, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 66 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 67 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(30, 62) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 68 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 69 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 70 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 71 | 주석: Work | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 72 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 73 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_Work, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 74 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 75 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 76 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(34, 103) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 77 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 78 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 79 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 80 | 오른쪽 결과를 왼쪽 상태에 저장한다: Panel panel = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 81 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(40, 125), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 82 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(910, 22) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 83 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 84 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 85 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = WorkInstallRadio = new RadioButton { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 86 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_Work_Install, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 87 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 88 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 89 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 90 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 91 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 92 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 93 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 94 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = WorkUninstallRadio = new RadioButton { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 95 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_Work_Uninstall, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 96 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 97 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 98 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(80, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 99 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.IsExistUnityModManager \|\| RequirementStatus.ExistMods | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 100 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 101 | 연결된 식·선언·설정의 일부다: WorkUninstallRadio.CheckedChanged += (_, _) =&gt; { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 102 | 이 함수 호출/식 처리를 실행한다: SaveData(WorkUninstallRadio.Checked); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 103 | 이 함수 호출/식 처리를 실행한다: UpdateUI(!WorkUninstallRadio.Checked, false); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 104 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 105 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 106 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 107 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(panel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 108 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 109 | 오른쪽 결과를 왼쪽 상태에 저장한다: panel = ContentsPanel = new Panel { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 110 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(34, 169), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 111 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(916, 360) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 112 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 113 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(panel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 114 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 115 | 이 함수 호출/식 처리를 실행한다: MainPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 116 | 이 함수 호출/식 처리를 실행한다: UpdateUI(!GlobalSetting.Instance.IsUninstall, true); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 117 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 118 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 119 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void UpdateUI(bool install, bool init) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 120 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.SuspendLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 121부터 180까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 121 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Clear(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 122 | 오른쪽 결과를 왼쪽 상태에 저장한다: Resources resources = Resources.Current; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 123 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 124 | 조건을 만족할 때 뒤의 작업을 수행한다: if(install) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 125 | 주석: Requirement | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 126 | 오른쪽 결과를 왼쪽 상태에 저장한다: Control control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 127 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_Requirement, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 128 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 129 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 130 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 0) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 131 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 132 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 133 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 134 | 오른쪽 결과를 왼쪽 상태에 저장한다: Panel panel = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 135 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(6, 28), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 136 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(910, 22) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 137 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 138 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 139 | 이 함수 호출/식 처리를 실행한다: panel.Click += RequirementPanelOnClick; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 140 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = RequirementUnityModManager = new CheckBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 141 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = "UnityModManager", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 142 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 143 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 144 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 145 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = !RequirementStatus.IsExistUnityModManager \|\| GlobalSetting.Instance.InstallUnityModManager, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 146 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.IsExistUnityModManager | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 147 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 148 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 149 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 150 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = RequirementDoorstop = new CheckBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 151 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = "Doorstop", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 152 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 153 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 154 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(177, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 155 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = RequirementUnityModManager.Checked \|\| GlobalSetting.Instance.InstallDoorstop, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 156 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.IsExistUnityModManager | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 157 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 158 | 이 함수 호출/식 처리를 실행한다: RequirementDoorstop.CheckedChanged += RequirementDoorstopOnCheckedChanged; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 159 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 160 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 161 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = RequirementJalib = new CheckBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 162 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = "JALib", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 163 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 164 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 165 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(293, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 166 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = !RequirementStatus.IsExistJALib \|\| GlobalSetting.Instance.InstallJalib, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 167 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.IsExistJALib | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 168 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 169 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 170 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 171 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = RequirementJipperResourcePack = new CheckBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 172 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = "JipperResourcePack", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 173 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 174 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 175 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(384, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 176 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = GlobalSetting.Instance.InstallJipperResourcePack \|\| !RequirementStatus.IsExistJipperResourcePack, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 177 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.IsExistJipperResourcePack | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 178 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 179 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 180 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 181부터 240까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 181 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Add(panel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 182 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 183 | 주석: Addition Mods | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 184 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 185 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_AdditionMods, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 186 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 187 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 188 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 71) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 189 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 190 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 191 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 192 | 주석: Uninstall Option | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 193 | 오른쪽 결과를 왼쪽 상태에 저장한다: Control control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 194 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 195 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 196 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 197 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 0) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 198 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 199 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 200 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 201 | 오른쪽 결과를 왼쪽 상태에 저장한다: Panel panel = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 202 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(6, 28), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 203 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(910, 82) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 204 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 205 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 206 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = UninstallAll = new RadioButton { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 207 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption_All, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 208 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 209 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(85, 22), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 210 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 0), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 211 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = GlobalSetting.Instance.UninstallOption == 0 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 212 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 213 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 214 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 215 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 216 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption_AllDescription, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 217 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 218 | 오른쪽 결과를 왼쪽 상태에 저장한다: ForeColor = Constants.SubColor, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 219 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 220 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(82, 5) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 221 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 222 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 223 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 224 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = UninstallManager = new RadioButton { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 225 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption_OnlyManager, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 226 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 227 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(143, 22), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 228 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 30), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 229 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = GlobalSetting.Instance.UninstallOption == 1, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 230 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.IsExistUnityModManager | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 231 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 232 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 233 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 234 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 235 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption_OnlyManagerDescription, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 236 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 237 | 오른쪽 결과를 왼쪽 상태에 저장한다: ForeColor = Constants.SubColor, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 238 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 239 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(140, 35) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 240 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 241부터 300까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 241 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 242 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 243 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = UninstallMod = new RadioButton { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 244 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption_OnlyMod, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 245 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 246 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(129, 22), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 247 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(0, 60), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 248 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = GlobalSetting.Instance.UninstallOption == 2, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 249 | 오른쪽 결과를 왼쪽 상태에 저장한다: Enabled = RequirementStatus.ExistMods | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 250 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 251 | 연결된 식·선언·설정의 일부다: UninstallMod.CheckedChanged += (_, _) =&gt; { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 252 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup.Visible = UninstallMod.Checked \|\| WorkInstallRadio.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 253 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 254 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 255 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 256 | 오른쪽 결과를 왼쪽 상태에 저장한다: control = new Label { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 257 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = resources.Select_UninstallOption_OnlyModDescription, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 258 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 259 | 오른쪽 결과를 왼쪽 상태에 저장한다: ForeColor = Constants.SubColor, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 260 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoSize = true, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 261 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(126, 65) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 262 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 263 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(control); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 264 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 265 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Add(panel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 266 | 오른쪽 결과를 왼쪽 상태에 저장한다: ContentsPanel.Size = new Size(916, 134); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 267 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 268 | 이 함수 호출/식 처리를 실행한다: EnterAsyncWork(install, init); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 269 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.ResumeLayout(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 270 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 271 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 272 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private async void EnterAsyncWork(bool install, bool init) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 273 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 274 | 이 함수 호출/식 처리를 실행한다: ConcurrentQueue&lt;string&gt; modItemQueue; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 275 | 이 함수 호출/식 처리를 실행한다: Task task; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 276 | 조건을 만족할 때 뒤의 작업을 수행한다: if(init &amp;&amp; RequirementStatus.ExistMods) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 277 | 오른쪽 결과를 왼쪽 상태에 저장한다: ExistModsCancellationTokenSource = new CancellationTokenSource(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 278 | 오른쪽 결과를 왼쪽 상태에 저장한다: modItemQueue = new ConcurrentQueue&lt;string&gt;(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 279 | 오른쪽 결과를 왼쪽 상태에 저장한다: task = Task.Run(() =&gt; { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 280 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 281 | 오른쪽 결과를 왼쪽 상태에 저장한다: CancellationToken token = ExistModsCancellationTokenSource.Token; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 282 | 각 컬렉션 항목을 순회한다: foreach(string file in Directory.GetDirectories(Path.Combine(GlobalSetting.Instance.InstallPath, "Mods"))) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 283 | 오른쪽 결과를 왼쪽 상태에 저장한다: string infoLocation = Path.Combine(file, "Info.json"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 284 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!File.Exists(infoLocation)) infoLocation = Path.Combine(file, "info.json"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 285 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!File.Exists(infoLocation)) continue; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 286 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 287 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModInfo modInfo = Utility.JsonSerializer.Deserialize&lt;ModInfo&gt;(File.ReadAllText(infoLocation)); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 288 | 조건을 만족할 때 뒤의 작업을 수행한다: if(token.IsCancellationRequested) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 289 | 이 함수 호출/식 처리를 실행한다: ExistModsPath.Add(file); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 290 | 이 함수 호출/식 처리를 실행한다: modItemQueue.Enqueue(modInfo.Id + "(By " + modInfo.Author.Replace(" &amp; ", ", ").Replace("&amp;", ", ") + ")"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 291 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 292 | 연결된 식·선언·설정의 일부다: } finally { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 293 | 이 함수 호출/식 처리를 실행한다: modItemQueue.Enqueue(null); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 294 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 295 | 이 함수 호출/식 처리를 실행한다: }, ExistModsCancellationTokenSource.Token); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 296 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 297 | 오른쪽 결과를 왼쪽 상태에 저장한다: modItemQueue = null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 298 | 오른쪽 결과를 왼쪽 상태에 저장한다: task = Task.CompletedTask; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 299 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 300 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 301부터 360까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 301 | 조건을 만족할 때 뒤의 작업을 수행한다: if(install) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 302 | 오른쪽 결과를 왼쪽 상태에 저장한다: int line = 0; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 303 | 작업을 실행하고 오류를 catch에 전달한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 304 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModData[] mods = await GlobalSetting.Instance.AdditionMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 305 | 오른쪽 결과를 왼쪽 상태에 저장한다: line = (mods.Length + 2) / 3; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 306 | 조건을 만족할 때 뒤의 작업을 수행한다: if(line != 0) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 307 | 오른쪽 결과를 왼쪽 상태에 저장한다: Panel panel = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 308 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(6, 96), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 309 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(910, -4 + 25 * line) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 310 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 311 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 312 | 오른쪽 결과를 왼쪽 상태에 저장한다: AdditionMods = new CheckBox[mods.Length]; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 313 | 오른쪽 결과를 왼쪽 상태에 저장한다: List&lt;ModData&gt; selectedMods = GlobalSetting.Instance.SelectedMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 314 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; mods.Length; i++) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 315 | 오른쪽 결과를 왼쪽 상태에 저장한다: AdditionMods[i] = new CheckBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 316 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = mods[i].DisplayName, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 317 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 318 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(303, 25), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 319 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(i % 3 * 303, i / 3 * 25), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 320 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = selectedMods?.Contains(mods[i]) == true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 321 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 322 | 이 함수 호출/식 처리를 실행한다: panel.Controls.Add(AdditionMods[i]); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 323 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 324 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 325 | 이 함수 호출/식 처리를 실행한다: ContentsPanel.Controls.Add(panel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 326 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 327 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 328 | 작업/검사 결과를 출력한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 329 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 330 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 331 | 오른쪽 결과를 왼쪽 상태에 저장한다: ContentsPanel.Size = new Size(916, 114 + 25 * line); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 332 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 333 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 334 | 주석: Remove Mods | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 335 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!RequirementStatus.ExistMods) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 336 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 337 | 조건을 만족할 때 뒤의 작업을 수행한다: if(init) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 338 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup = new GroupBox { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 339 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = Resources.Current.Select_RemoveMods, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 340 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial13, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 341 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(32, 169 + ContentsPanel.Size.Height) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 342 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 343 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup.Size = new Size(920, 539 - ModListGroup.Location.Y); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 344 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 345 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListPanel = new Panel { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 346 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(10, 25), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 347 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = ModListGroup.Size - new Size(11, 30), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 348 | 오른쪽 결과를 왼쪽 상태에 저장한다: AutoScroll = true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 349 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 350 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 351 | 오른쪽 결과를 왼쪽 상태에 저장한다: List&lt;string&gt; removeMods = GlobalSetting.Instance.RemoveRequestMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 352 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 353 | 조건이 참인 동안 반복한다: while(true) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 354 | 조건을 만족할 때 뒤의 작업을 수행한다: if(modItemQueue!.TryDequeue(out string item)) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 355 | 조건을 만족할 때 뒤의 작업을 수행한다: if(item == null) break; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 356 | 오른쪽 결과를 왼쪽 상태에 저장한다: CheckBox checkBox = new() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 357 | 오른쪽 결과를 왼쪽 상태에 저장한다: Text = item, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 358 | 오른쪽 결과를 왼쪽 상태에 저장한다: Font = Constants.Arial11, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 359 | 오른쪽 결과를 왼쪽 상태에 저장한다: Size = new Size(295, 25), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 360 | 오른쪽 결과를 왼쪽 상태에 저장한다: Location = new Point(ExistMods.Count % 3 * 295, ExistMods.Count / 3 * 25), | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 361부터 420까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 361 | 오른쪽 결과를 왼쪽 상태에 저장한다: Checked = removeMods?.Contains(ExistModsPath[ExistMods.Count]) == true | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 362 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 363 | 이 함수 호출/식 처리를 실행한다: ExistMods.Add(checkBox); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 364 | 이 함수 호출/식 처리를 실행한다: ModListPanel.Controls.Add(checkBox); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 365 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 366 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 367 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 368 | 이 함수 호출/식 처리를 실행한다: await task; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 369 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 370 | 오른쪽 결과를 왼쪽 상태에 저장한다: int calY = 31 + (ExistMods.Count + 2) / 3 * 25; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 371 | 오른쪽 결과를 왼쪽 상태에 저장한다: ExistModHeight = calY; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 372 | 조건을 만족할 때 뒤의 작업을 수행한다: if(calY &lt; ModListGroup.Size.Height) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 373 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup.Size = ModListGroup.Size with { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 374 | 오른쪽 결과를 왼쪽 상태에 저장한다: Height = calY | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 375 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 376 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListPanel.Size = ModListPanel.Size with { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 377 | 오른쪽 결과를 왼쪽 상태에 저장한다: Height = calY - 30 | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 378 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 379 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 380 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 381 | 이 함수 호출/식 처리를 실행한다: ModListGroup.Controls.Add(ModListPanel); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 382 | 이 함수 호출/식 처리를 실행한다: MainPanel.Controls.Add(ModListGroup); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 383 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 384 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup.Visible = install \|\| UninstallMod.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 385 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup.Location = new Point(32, 169 + ContentsPanel.Size.Height); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 386 | 오른쪽 결과를 왼쪽 상태에 저장한다: int calY = 539 - ModListGroup.Location.Y; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 387 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListGroup.Size = new Size(920, Math.Min(calY, ExistModHeight)); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 388 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 389 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModListPanel.Size = ModListGroup.Size - new Size(11, 30); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 390 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 391 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 392 | 작업/검사 결과를 출력한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 393 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(e.ToString(), Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 394 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 395 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 396 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 397 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void SaveData(bool isInstall) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 398 | 조건을 만족할 때 뒤의 작업을 수행한다: if(isInstall) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 399 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallUnityModManager = RequirementUnityModManager.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 400 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallDoorstop = RequirementDoorstop.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 401 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallJalib = RequirementJalib.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 402 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.InstallJipperResourcePack = RequirementJipperResourcePack.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 403 | 오른쪽 결과를 왼쪽 상태에 저장한다: List&lt;ModData&gt; selectedMods = []; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 404 | 오른쪽 결과를 왼쪽 상태에 저장한다: ModData[] mods = GlobalSetting.Instance.AdditionMods.Result; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 405 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; AdditionMods.Length; i++) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 406 | 조건을 만족할 때 뒤의 작업을 수행한다: if(AdditionMods[i].Checked) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 407 | 이 함수 호출/식 처리를 실행한다: selectedMods.Add(mods[i]); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 408 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.SelectedMods = selectedMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 409 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 410 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 411 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 412 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void RequirementDoorstopOnCheckedChanged(object sender, EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 413 | 조건을 만족할 때 뒤의 작업을 수행한다: if(RequirementDoorstop.Checked) return; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 414 | 조건을 만족할 때 뒤의 작업을 수행한다: if(RequirementStatus.IsAssemblyInstalled) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 415 | 오른쪽 결과를 왼쪽 상태에 저장한다: DialogResult result = MessageBox.Show(Resources.Current.Select_Requirement_Doorstop_Asm, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 416 | 조건을 만족할 때 뒤의 작업을 수행한다: if(result == DialogResult.No) RequirementDoorstop.Checked = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 417 | 이전 블록을 닫고 분기/예외 처리로 이동한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 418 | 오른쪽 결과를 왼쪽 상태에 저장한다: DialogResult result = MessageBox.Show(Resources.Current.Select_Requirement_Doorstop_Old, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 419 | 조건을 만족할 때 뒤의 작업을 수행한다: if(result == DialogResult.No) RequirementDoorstop.Checked = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 420 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

### 삭제 줄 421부터 467까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 421 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 422 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 423 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private void RequirementPanelOnClick(object sender, EventArgs e) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 424 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!RequirementUnityModManager.Enabled &amp;&amp; RequirementUnityModManager.Bounds.Contains(((MouseEventArgs) e).Location)) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 425 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(Resources.Current.Select_Requirement_UnityModManager, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 426 | 앞 조건이 아니면 대체 조건을 검사한다: else if(!RequirementDoorstop.Enabled &amp;&amp; RequirementDoorstop.Bounds.Contains(((MouseEventArgs) e).Location)) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 427 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(Resources.Current.Select_Requirement_Doorstop_Umm, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 428 | 앞 조건이 아니면 대체 조건을 검사한다: else if(!RequirementJalib.Enabled &amp;&amp; RequirementJalib.Bounds.Contains(((MouseEventArgs) e).Location)) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 429 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(Resources.Current.Select_Requirement_JALib, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 430 | 앞 조건이 아니면 대체 조건을 검사한다: else if(!RequirementJipperResourcePack.Enabled &amp;&amp; RequirementJipperResourcePack.Bounds.Contains(((MouseEventArgs) e).Location)) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 431 | 이 함수 호출/식 처리를 실행한다: MessageBox.Show(Resources.Current.Select_Requirement_JipperResourcePack, Resources.Current.Error, MessageBoxButtons.OK, MessageBoxIcon.Error); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 432 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 433 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 434 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override bool OnNext() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 435 | 조건을 만족할 때 뒤의 작업을 수행한다: if(WorkUninstallRadio.Checked) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 436 | 조건을 만족할 때 뒤의 작업을 수행한다: if(UninstallAll.Checked) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 437 | 오른쪽 결과를 왼쪽 상태에 저장한다: DialogResult result = MessageBox.Show(Resources.Current.Select_RemoveMods_Confirm, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 438 | 함수를 종료하거나 식의 결과를 반환한다: return result != DialogResult.No; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 439 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 440 | 조건을 만족할 때 뒤의 작업을 수행한다: if(UninstallManager.Checked) return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 441 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 442 | 각 컬렉션 항목을 순회한다: foreach(CheckBox checkBox in ExistMods) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 443 | 조건을 만족할 때 뒤의 작업을 수행한다: if(!checkBox.Checked) continue; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 444 | 오른쪽 결과를 왼쪽 상태에 저장한다: DialogResult result = MessageBox.Show(Resources.Current.Select_RemoveMods_Confirm, Resources.Current.Warn, MessageBoxButtons.YesNo, MessageBoxIcon.Warning); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 445 | 함수를 종료하거나 식의 결과를 반환한다: return result != DialogResult.No; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 446 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 447 | 함수를 종료하거나 식의 결과를 반환한다: return true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 448 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 449 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 450 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public override void OnLeave() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 451 | 이 함수 호출/식 처리를 실행한다: ExistModsCancellationTokenSource?.Cancel(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 452 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 453 | 이 함수 호출/식 처리를 실행한다: SaveData(WorkInstallRadio.Checked); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 454 | 오른쪽 결과를 왼쪽 상태에 저장한다: List&lt;string&gt; removeRequestMods = []; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 455 | 초기값·조건·증가식에 따라 반복한다: for(int i = 0; i &lt; ExistMods.Count; i++) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 456 | 조건을 만족할 때 뒤의 작업을 수행한다: if(ExistMods[i].Checked) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 457 | 이 함수 호출/식 처리를 실행한다: removeRequestMods.Add(ExistModsPath[i]); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 458 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 459 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.RemoveRequestMods = removeRequestMods; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 460 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.IsUninstall = WorkUninstallRadio.Checked; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 461 | 오른쪽 결과를 왼쪽 상태에 저장한다: GlobalSetting.Instance.SelectSaved = true; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 462 | 이 함수 호출/식 처리를 실행한다: ExistModsPath.Clear(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 463 | 이 함수 호출/식 처리를 실행한다: ExistMods.Clear(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 464 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 465 | 오른쪽 결과를 왼쪽 상태에 저장한다: TopPanelLabels[1].Font = Constants.Arial16; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 466 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 467 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/Utility.cs

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/Utility.cs b/Installer/Utility.cs
deleted file mode 100644
index eb9d42d..0000000
--- a/Installer/Utility.cs
+++ /dev/null
@@ -1,29 +0,0 @@
-﻿using System;
-using System.IO;
-using System.Web.Script.Serialization;
-using Microsoft.Win32;
-
-namespace JipperResourcePack.Installer;
-
-public static class Utility {
-    public static readonly JavaScriptSerializer JsonSerializer = new();
-    
-    public static string GetAdofaiPath() {
-        string steamPath;
-        using(RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\" + (Environment.Is64BitOperatingSystem ? "Wow6432Node\\" : "") + "Valve\\Steam"))
-            steamPath = key?.GetValue("InstallPath") as string;
-        if(steamPath != null) {
-            string path = Path.Combine(steamPath, "steamapps", "common", "A Dance of Fire and Ice");
-            if(CheckAdofaiDirectory(path)) return path;
-        }
-        for(char c = 'A'; c <= 'Z'; c++) {
-            string path = c + ":/SteamLibrary/steamapps/common/A Dance of Fire and Ice";
-            if(CheckAdofaiDirectory(path)) return path;
-        }
-        return null;
-    }
-
-    private static bool CheckAdofaiDirectory(string path) {
-        return Directory.Exists(Path.Combine(path, "A Dance of Fire and Ice_Data")) && File.Exists(Path.Combine(path, "A Dance of Fire and Ice.exe"));
-    }
-}
\ No newline at end of file
```

### 삭제 줄 1부터 29까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | namespace 또는 타입 별칭을 가져온다: System; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | namespace 또는 타입 별칭을 가져온다: System.IO; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | namespace 또는 타입 별칭을 가져온다: System.Web.Script.Serialization; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | namespace 또는 타입 별칭을 가져온다: Microsoft.Win32; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 이 타입의 소속 namespace를 지정한다: namespace JipperResourcePack.Installer; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 타입을 선언한다: public static class Utility { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 상태 필드/속성과 초기값 또는 조회식을 지정한다: public static readonly JavaScriptSerializer JsonSerializer = new(); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 함수/생성자의 이름·입력·반환 형식을 선언한다: public static string GetAdofaiPath() { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 이 함수 호출/식 처리를 실행한다: string steamPath; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 오른쪽 결과를 왼쪽 상태에 저장한다: using(RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\" + (Environment.Is64BitOperatingSystem ? "Wow6432Node\\" : "") + "Valve\\Steam")) | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 오른쪽 결과를 왼쪽 상태에 저장한다: steamPath = key?.GetValue("InstallPath") as string; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 조건을 만족할 때 뒤의 작업을 수행한다: if(steamPath != null) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 오른쪽 결과를 왼쪽 상태에 저장한다: string path = Path.Combine(steamPath, "steamapps", "common", "A Dance of Fire and Ice"); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 조건을 만족할 때 뒤의 작업을 수행한다: if(CheckAdofaiDirectory(path)) return path; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 초기값·조건·증가식에 따라 반복한다: for(char c = 'A'; c &lt;= 'Z'; c++) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 오른쪽 결과를 왼쪽 상태에 저장한다: string path = c + ":/SteamLibrary/steamapps/common/A Dance of Fire and Ice"; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 조건을 만족할 때 뒤의 작업을 수행한다: if(CheckAdofaiDirectory(path)) return path; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 23 | 함수를 종료하거나 식의 결과를 반환한다: return null; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 24 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 25 | 빈 줄을 제거한다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 26 | 함수/생성자의 이름·입력·반환 형식을 선언한다: private static bool CheckAdofaiDirectory(string path) { | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 27 | 함수를 종료하거나 식의 결과를 반환한다: return Directory.Exists(Path.Combine(path, "A Dance of Fire and Ice_Data")) &amp;&amp; File.Exists(Path.Combine(path, "A Dance of Fire and Ice.exe")); | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 28 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 29 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/mods.json

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/mods.json b/Installer/mods.json
deleted file mode 100644
index 3f518a8..0000000
--- a/Installer/mods.json
+++ /dev/null
@@ -1,22 +0,0 @@
-[
-  {
-    "Name": "AdofaiTweaks",
-    "URL": "https://github.com/PizzaLovers007/AdofaiTweaks/releases/download/v2.9.2/AdofaiTweaks-2.9.2.zip",
-    "DisplayName": "AdofaiTweaks(By PizzaLovers007)"
-  },
-  {
-    "Name": "YouTubeStream",
-    "URL": "https://fixcdn.hyonsu.com/attachments/886661471533162526/1509057919369347193/YouTubeStream-1.0.4.zip",
-    "DisplayName": "YouTubeStream(By 정현수)"
-  },
-  {
-    "Name": "KeyboardChatterBlocker",
-    "URL": "https://github.com/fangshenghan/KeyboardChatterBlocker/releases/download/0.1.2/KeyboardChatterBlocker.v0.1.2.zip",
-    "DisplayName": "KeyboardChatterBlocker(By Sharky)"
-  },
-  {
-    "Name": "PACL2",
-    "URL": "https://jalib.jongyeol.kr/downloadMod/PACL2/latest",
-    "DisplayName": "PACL2(By Asansis, Jongyeol)"
-  }
-]
```

### 삭제 줄 1부터 22까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 연결된 식·선언·설정의 일부다: [ | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 현재 블록/타입의 시작이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 연결된 식·선언·설정의 일부다: "Name": "AdofaiTweaks", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 연결된 식·선언·설정의 일부다: "URL": "https://github.com/PizzaLovers007/AdofaiTweaks/releases/download/v2.9.2/AdofaiTweaks-2.9.2.zip", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 5 | 연결된 식·선언·설정의 일부다: "DisplayName": "AdofaiTweaks(By PizzaLovers007)" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 6 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 7 | 현재 블록/타입의 시작이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 8 | 연결된 식·선언·설정의 일부다: "Name": "YouTubeStream", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 9 | 연결된 식·선언·설정의 일부다: "URL": "https://fixcdn.hyonsu.com/attachments/886661471533162526/1509057919369347193/YouTubeStream-1.0.4.zip", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 10 | 연결된 식·선언·설정의 일부다: "DisplayName": "YouTubeStream(By 정현수)" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 11 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 12 | 현재 블록/타입의 시작이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 13 | 연결된 식·선언·설정의 일부다: "Name": "KeyboardChatterBlocker", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 14 | 연결된 식·선언·설정의 일부다: "URL": "https://github.com/fangshenghan/KeyboardChatterBlocker/releases/download/0.1.2/KeyboardChatterBlocker.v0.1.2.zip", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 15 | 연결된 식·선언·설정의 일부다: "DisplayName": "KeyboardChatterBlocker(By Sharky)" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 16 | 연결된 식·선언·설정의 일부다: }, | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 17 | 현재 블록/타입의 시작이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 18 | 연결된 식·선언·설정의 일부다: "Name": "PACL2", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 19 | 연결된 식·선언·설정의 일부다: "URL": "https://jalib.jongyeol.kr/downloadMod/PACL2/latest", | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 20 | 연결된 식·선언·설정의 일부다: "DisplayName": "PACL2(By Asansis, Jongyeol)" | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 21 | 현재 블록/타입의 끝이다. | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 22 | 연결된 식·선언·설정의 일부다: ] | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |

## Installer/packages.config

기존 Installer 삭제다. SDK에 필요한 삭제가 아니며 삭제 목적은 확인되지 않았다. 실제 삭제 diff 전체를 먼저 표시하고, 설명 표만 60줄씩 나눈다.

```diff
diff --git a/Installer/packages.config b/Installer/packages.config
deleted file mode 100644
index 033cb3a..0000000
--- a/Installer/packages.config
+++ /dev/null
@@ -1,4 +0,0 @@
-﻿<?xml version="1.0" encoding="utf-8"?>
-<packages>
-  <package id="Microsoft.NETFramework.ReferenceAssemblies.net481" version="1.0.3" targetFramework="net481" developmentDependency="true" />
-</packages>
```

### 삭제 줄 1부터 4까지

| 변경 | 줄 번호 | 의사코드 | 이유 |
|---|---:|---|---|
| 삭제 | 이전 1 | 프로젝트·리소스·설정 요소를 지정한다: &lt;?xml version="1.0" encoding="utf-8"?&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 2 | 프로젝트·리소스·설정 요소를 지정한다: &lt;packages&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 3 | 프로젝트·리소스·설정 요소를 지정한다: &lt;package id="Microsoft.NETFramework.ReferenceAssemblies.net481" version="1.0.3" targetFramework="net481" developmentDependency="true" /&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |
| 삭제 | 이전 4 | 프로젝트·리소스·설정 요소를 지정한다: &lt;/packages&gt; | 기존 Installer 삭제에 포함된 줄이다. SDK 필수 변경이 아니며 삭제 이유는 확인되지 않았다. |


