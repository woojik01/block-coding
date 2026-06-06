<<<<<<< HEAD
# Block Game Engine

Windows용 Scratch 스타일 블록 코딩 2D 게임 엔진 MVP입니다.

엔진 자체는 Windows WPF 프로그램이며, 사용자가 만든 게임은 Shared Runtime을 재사용해 Windows exe 또는 Android APK용 프로젝트로 export하는 구조입니다.

## 구조

```text
Windows Editor (WPF)
  -> Project Serializer / Compiler
  -> Shared Runtime Engine
  -> Export Pipeline
      -> Windows Export (.exe)
      -> Android Export (.apk project)
```

## 프로젝트 구성

- `src/BlockGameEngine.Editor`: WPF 기반 Windows Editor
- `src/BlockGameEngine.Runtime`: 플랫폼 독립 Runtime, Scene/Sprite/Collision/Variables/Lists/Block Interpreter
- `src/BlockGameEngine.ProjectIO`: `.blockgame` 저장/불러오기, 에셋 폴더 관리
- `src/BlockGameEngine.Compiler`: 프로젝트를 export용 `CompiledGamePackage`로 변환
- `src/BlockGameEngine.Export`: Windows/Android export 파이프라인
- `src/BlockGameEngine.WindowsPlayer`: export된 Windows 게임 실행기
- `src/BlockGameEngine.AndroidPlayer.Template`: Android export 시 복사되는 `.NET Android` 템플릿 (소스 전용, Android workload 필요)
- `tests/BlockGameEngine.Tests`: 외부 NuGet 없는 콘솔 테스트 러너

## 현재 기능

### WPF Editor

- Canvas 기반 **자유 배치형 블록 노드 그래프** (드래그 이동, 소켓 연결)
- 스크립트 탭: Game Start / Key Pressed / Click / Collision
- Scene 전환, Sprite 목록, 이미지 import
- 프로젝트 설정: 이름, 버전, Application ID, 아이콘
- Undo / Redo, 줌
- Canvas 게임 미리보기 (키보드 + 클릭 입력)
- `.blockgame` 저장 / 불러오기 (`assets/` 폴더 동기화)
- Windows exe / Android APK export (진행 상태 및 성공/실패 표시)

### Shared Runtime

- Sprite / Scene / Camera 모델
- 변수 및 **리스트** 모델
- AABB 충돌, 프레임 기반 **Wait** 블록
- 키 / 클릭 / 충돌 이벤트
- 블록 인터프리터

### 지원 블록

- 이벤트: 게임 시작, 키 입력, 클릭, 충돌
- 동작: 이동, 회전, 좌표 변경, 크기 변경
- 제어: 반복, 조건, 대기
- 데이터: 변수 설정/변경, 리스트 추가/삭제/길이

### Export

- **Windows**: Release publish + `game.package.json` + assets + 게임 이름 기반 exe
- **Android**: 템플릿 복사 + Runtime 참조 + `Assets/game.package.json` + 터치 입력 어댑터 + 브랜딩

## NuGet 패키지

현재 MVP는 제한된 빌드 환경에서도 동작하도록 외부 NuGet 패키지를 사용하지 않습니다.

## 빌드

```powershell
dotnet restore BlockGameEngine.slnx
dotnet build BlockGameEngine.slnx
```

NuGet 홈 접근이 막힌 환경:

```powershell
$root = (Get-Location).Path
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$env:NUGET_PACKAGES = Join-Path $root ".nuget_packages"
$env:APPDATA = Join-Path $root ".appdata"
$env:LOCALAPPDATA = Join-Path $root ".localappdata"
dotnet restore BlockGameEngine.slnx
dotnet build BlockGameEngine.slnx --no-restore
```

> `BlockGameEngine.AndroidPlayer.Template`은 Android workload가 없으면 솔루션 빌드에 포함되지 않습니다. Export 시 소스 파일이 복사됩니다.

## 실행

```powershell
dotnet run --project src/BlockGameEngine.Editor
```

1. 팔레트에서 블록을 작업 Canvas에 드래그
2. 블록 하단 소켓 → 다른 블록 상단 소켓으로 연결
3. `Run` 후 미리보기에서 `Right` 키 또는 클릭으로 테스트

## 테스트

```powershell
dotnet run --project tests/BlockGameEngine.Tests
```

## Release 빌드

```powershell
dotnet publish src/BlockGameEngine.Editor -c Release --self-contained false -o outputs/editor-release
dotnet publish src/BlockGameEngine.WindowsPlayer -c Release --self-contained false -o outputs/windows-player-release
```

## Export

### Windows exe

Editor `Export EXE` → `outputs/windows-export`

### Android APK

Editor `Export APK` → `outputs/android-export`

APK 빌드 (Android workload + SDK + JDK 필요):

```powershell
cd outputs/android-export
dotnet publish -c Release -f net10.0-android
```

## 프로젝트 파일 형식

- `.blockgame`: JSON 프로젝트 파일
- `assets/`: 스프라이트 이미지 및 아이콘 (프로젝트 파일과 같은 폴더)
- `EditorLayout`: 블록 Canvas 좌표 (Runtime 실행에 영향 없음)
=======
# block-coding
>>>>>>> 95b6597149372516ae76375985f77aa1b4884c7c
