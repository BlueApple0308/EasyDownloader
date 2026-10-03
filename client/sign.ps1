# EasyDownloader 코드 서명 스크립트 (build.bat이 빌드 후 자동 호출)
# 아래 설정 중 하나만 채우면 됩니다. 둘 다 비어 있으면 서명을 건너뜁니다.
param([string]$Target = "$PSScriptRoot\dist\EasyDownloader.exe")

# ── 방법 A: 인증서 저장소의 코드 서명 인증서 (Certum SimplySign, USB 토큰 등) ──
# certmgr.msc > 개인 > 인증서 에서 인증서를 열고 [자세히] > 지문(Thumbprint) 값을 붙여넣으세요
$CertThumbprint = ""

# ── 방법 B: Azure Artifact Signing (사업자 계정이 있는 경우) ──
$ArtifactEndpoint = ""   # 예: https://krc.codesigning.azure.net
$ArtifactAccount  = ""   # Artifact Signing 계정 이름
$ArtifactProfile  = ""   # 인증서 프로필 이름

$TimestampUrl = "http://timestamp.digicert.com"

$ErrorActionPreference = "Stop"

if (-not $CertThumbprint -and -not $ArtifactAccount) {
    Write-Host "[SIGN] 서명 설정이 비어 있어 서명을 건너뜁니다. (client\sign.ps1)"
    exit 0
}
if (-not (Test-Path $Target)) { Write-Host "[SIGN] 파일 없음: $Target"; exit 1 }

# signtool 찾기: Windows SDK가 없으면 NuGet의 SDK BuildTools를 tools\ 아래에 받아둠
function Get-SignTool {
    $found = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($found) { return $found.FullName }

    $toolsDir = Join-Path $PSScriptRoot "tools\sdk-buildtools"
    $local = Get-ChildItem "$toolsDir\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($local) { return $local.FullName }

    Write-Host "[SIGN] signtool이 없어 Microsoft.Windows.SDK.BuildTools (nuget.org)를 받습니다..."
    $zip = Join-Path $env:TEMP "sdk-buildtools.zip"
    Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools" -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $toolsDir -Force
    Remove-Item $zip
    return (Get-ChildItem "$toolsDir\bin\*\x64\signtool.exe" | Select-Object -First 1).FullName
}

$signtool = Get-SignTool
$common = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256",
            "/d", "EasyDownloader")

if ($CertThumbprint) {
    $thumb = $CertThumbprint -replace '\s', ''
    & $signtool @common /sha1 $thumb $Target
} else {
    # Artifact Signing: Azure CLI 로그인(az login) 상태여야 함
    $dlibDir = Join-Path $PSScriptRoot "tools\artifact-signing"
    $dlib = Get-ChildItem "$dlibDir\bin\x64\Azure.CodeSigning.Dlib.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dlib) {
        Write-Host "[SIGN] Artifact Signing 클라이언트 (nuget.org)를 받습니다..."
        $zip = Join-Path $env:TEMP "artifact-signing.zip"
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.Trusted.Signing.Client" -OutFile $zip -UseBasicParsing
        Expand-Archive $zip $dlibDir -Force
        Remove-Item $zip
        $dlib = Get-ChildItem "$dlibDir\bin\x64\Azure.CodeSigning.Dlib.dll" -Recurse | Select-Object -First 1
    }
    $meta = Join-Path $env:TEMP "artifact-signing-metadata.json"
    @{ Endpoint = $ArtifactEndpoint; CodeSigningAccountName = $ArtifactAccount;
       CertificateProfileName = $ArtifactProfile } | ConvertTo-Json | Set-Content $meta -Encoding utf8
    & $signtool @common /dlib $dlib.FullName /dmdf $meta $Target
}

if ($LASTEXITCODE -ne 0) { Write-Host "[SIGN] 서명 실패 (signtool 종료 코드 $LASTEXITCODE)"; exit 1 }

& $signtool verify /pa $Target | Out-Null
if ($LASTEXITCODE -eq 0) { Write-Host "[SIGN] 서명 완료 및 검증 통과: $Target" }
else { Write-Host "[SIGN] 서명은 됐지만 검증 실패 - 인증서 체인을 확인하세요"; exit 1 }
