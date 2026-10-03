# 게임 모드 간편 설치기 v2 (Easy Mod Installer)

서버 없이, 어드민이 메모장(.txt)에 Modrinth 링크만 적어서 디스코드에 올리면
유저가 간편 설치기(38KB EXE)로 원클릭 설치하는 초경량 모드팩 배포 솔루션입니다.

---

## 핵심 기능

- **서버 불필요**: 메모장 파일 하나로 모드팩 배포 (서버 유지비 0원)
- **38KB 초경량 EXE**: 디스코드 8MB 제한의 0.5%에 불과
- **Modrinth 링크 자동 인식**: 모드/쉐이더/리소스팩을 URL에서 자동 판별하여 올바른 폴더에 설치
- **어드민 지정 버전 자동 매칭**: VERSION=1.20.1 등의 설정에 맞는 파일만 정확히 다운로드
- **Fabric 로더 자동 설치**: 유저가 Fabric 사이트에 갈 필요 없이 자동으로 MC 런처에 등록
- **구버전 자동 정리**: 모드 충돌 및 게임 튕김 방지
- **체크박스 선택 설치**: 유저가 원하는 모드만 골라서 설치 가능
- **드래그 앤 드롭**: txt 파일을 exe에 끌어다 놓으면 자동 로드

---

## 사용법

### 어드민: 모드팩 메모장 작성

`mods.txt` 파일을 메모장으로 열고 아래와 같이 작성합니다:

```text
VERSION=1.20.1
LOADER=fabric

# 모드 (mods/ 폴더에 자동 설치)
https://modrinth.com/mod/sodium
https://modrinth.com/mod/iris
https://modrinth.com/mod/fabric-api

# 쉐이더 (shaderpacks/ 폴더에 자동 설치)
https://modrinth.com/shader/complementary-unbound

# 리소스팩 (resourcepacks/ 폴더에 자동 설치)
https://modrinth.com/resourcepack/faithful-32x
```

디스코드에 `EasyDownloader.exe`와 `mods.txt`를 함께 올립니다.

### 유저: 모드 설치

1. 디스코드에서 `EasyDownloader.exe`와 `mods.txt`를 다운로드
2. **방법 A**: `mods.txt`를 `EasyDownloader.exe` 아이콘 위에 끌어다 놓기
3. **방법 B**: exe를 실행 후 창에 txt 파일을 드래그 앤 드롭
4. **방법 C**: exe를 실행 후 [파일 열기] 버튼으로 txt 선택
5. 체크박스로 원하는 모드를 선택/해제
6. **[선택한 모드 설치 시작]** 버튼 클릭
7. 완료! MC 런처에서 Fabric 프로필 선택 후 플레이

---

## 프로젝트 구조

```
7.auto_downloader/
├── mods.txt                   # 어드민이 작성할 모드팩 설정 파일 (샘플)
├── client/
│   ├── src/
│   │   ├── Program.cs         # 진입점 (exe 드래그앤드롭 지원)
│   │   ├── Models.cs          # 데이터 모델
│   │   ├── ModpackParser.cs   # mods.txt 파서
│   │   ├── ModrinthApi.cs     # Modrinth API 연동
│   │   ├── FabricInstaller.cs # Fabric 로더 자동 설치
│   │   ├── DownloaderCore.cs  # 다운로드 엔진
│   │   ├── MinecraftFinder.cs # MC 경로 자동 탐색
│   │   └── MainForm.cs       # GUI (드래그앤드롭, 체크박스)
│   ├── build.bat              # 빌드 스크립트
│   └── dist/
│       └── EasyDownloader.exe # 배포용 실행 파일 (38KB)
├── server/                    # (이전 버전) 관리자 웹 서버 (선택사항)
└── README.md
```

---

## 빌드 방법

`client/build.bat`을 더블클릭하면 `client/dist/EasyDownloader.exe`가 생성됩니다.
Windows에 기본 탑재된 .NET Framework 컴파일러(csc.exe)를 사용하므로 별도 설치가 필요 없습니다.

---

## 다운로드

최신 버전은 [Releases](https://github.com/BlueApple0308/EasyDownloader/releases) 페이지에서 받을 수 있습니다.
배포되는 exe는 GitHub Actions가 이 저장소의 소스로 직접 빌드한 파일입니다.

---

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- 서명 대상: 이 저장소의 GitHub Actions(`.github/workflows/build.yml`)로 빌드된 `EasyDownloader.exe`만 서명합니다.
- 승인자(Approver): [BlueApple0308](https://github.com/BlueApple0308) — 모든 서명 요청은 수동으로 승인합니다.
- 커미터/리뷰어: [BlueApple0308](https://github.com/BlueApple0308)

## Privacy policy

이 프로그램은 사용자 정보를 수집하거나 전송하지 않습니다.
설치 과정에서 아래 공개 서버에만 접속합니다.

- `api.modrinth.com`, `cdn.modrinth.com` — 모드/쉐이더/리소스팩 정보 조회 및 다운로드
- `meta.fabricmc.net`, `maven.fabricmc.net` — Fabric 로더 설치
- 모드팩 txt에 어드민이 직접 적은 직링크 주소

---

## 라이선스

[MIT License](LICENSE)
