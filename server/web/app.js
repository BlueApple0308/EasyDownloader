// 전역 상태 데이터
let manifestData = {
  pack_name: "서버 공식 모드팩",
  version: "1.0.0",
  game_type: "minecraft",
  minecraft_version: "1.20.1",
  loader: "Fabric",
  auto_cleanup: true,
  notice: "",
  files: []
};

// DOM 로드 완료 후 실행
document.addEventListener("DOMContentLoaded", () => {
  initApiUrl();
  loadManifestFromServer();
  bindEvents();
});

// API 엔드포인트 URL 표시 설정
function initApiUrl() {
  const host = window.location.host || "localhost:5000";
  const protocol = window.location.protocol.startsWith("http") ? window.location.protocol : "http:";
  const url = `${protocol}//${host}/api/manifest`;
  document.getElementById("apiEndpointUrl").textContent = url;
}

// 서버에서 manifest.json 데이터 가져오기
async function loadManifestFromServer() {
  try {
    const res = await fetch("/api/manifest");
    if (res.ok) {
      manifestData = await res.json();
      renderConfig();
      renderFileTable();
      showToast("서버로부터 설정을 불러왔습니다.");
    } else {
      showToast("서버에서 설정을 가져오지 못했습니다.", true);
    }
  } catch (err) {
    console.error("Fetch manifest error:", err);
    // 오프라인 로컬 파일로 열었을 경우 기본 렌더링
    renderConfig();
    renderFileTable();
  }
}

// 기본 설정 폼에 데이터 바인딩
function renderConfig() {
  document.getElementById("inputPackName").value = manifestData.pack_name || "";
  document.getElementById("inputVersion").value = manifestData.version || "1.0.0";
  document.getElementById("inputGameType").value = manifestData.game_type || "minecraft";
  document.getElementById("inputMcVersion").value = manifestData.minecraft_version || "1.20.1";
  document.getElementById("inputLoader").value = manifestData.loader || "Fabric";
  document.getElementById("inputNotice").value = manifestData.notice || "";
  document.getElementById("inputAutoCleanup").checked = manifestData.auto_cleanup !== false;
}

// 모드 파일 목록 테이블 렌더링
function renderFileTable() {
  const tbody = document.getElementById("fileTableBody");
  const countEl = document.getElementById("fileCount");
  const emptyEl = document.getElementById("emptyMessage");

  tbody.innerHTML = "";
  const files = manifestData.files || [];
  countEl.textContent = files.length;

  if (files.length === 0) {
    emptyEl.style.display = "block";
    return;
  }
  emptyEl.style.display = "none";

  files.forEach((file, index) => {
    const tr = document.createElement("tr");

    // 크기 포맷
    const sizeStr = file.size_kb ? `${(file.size_kb / 1024).toFixed(1)} MB` : "-";

    tr.innerHTML = `
      <td class="file-name-cell">
        <strong>${escapeHtml(file.name)}</strong>
        <span>${escapeHtml(file.filename)}</span>
      </td>
      <td>
        <span class="dir-badge">${escapeHtml(file.target_dir || "mods")}</span>
      </td>
      <td>
        <a href="${escapeHtml(file.url)}" target="_blank" class="url-link" title="${escapeHtml(file.url)}">
          🔗 ${escapeHtml(file.url)}
        </a>
      </td>
      <td>${sizeStr}</td>
      <td>
        <div class="action-buttons">
          <button class="btn-sm" onclick="editFile('${file.id}')">수정</button>
          <button class="btn-danger-sm" onclick="deleteFile('${file.id}')">삭제</button>
        </div>
      </td>
    `;
    tbody.appendChild(tr);
  });
}

// 이벤트 리스너 바인딩
function bindEvents() {
  // 모달 열기
  document.getElementById("btnOpenAddModal").addEventListener("click", () => {
    openModal();
  });

  // 모달 닫기
  document.getElementById("btnCloseModal").addEventListener("click", closeModal);
  document.getElementById("btnCancelModal").addEventListener("click", closeModal);

  // 모달 저장
  document.getElementById("btnSaveModal").addEventListener("click", saveModalFile);

  // 대상 폴더 '직접 입력' 토글
  document.getElementById("modalInputTargetDir").addEventListener("change", (e) => {
    const customGroup = document.getElementById("customTargetDirGroup");
    customGroup.style.display = e.target.value === "custom" ? "block" : "none";
  });

  // 스마트 링크 변환 버튼
  document.getElementById("btnAutoConvertUrl").addEventListener("click", autoConvertInputUrl);

  // URL 입력 시 파일명 자동 유추
  document.getElementById("modalInputUrl").addEventListener("change", () => {
    autoGuessFilename();
  });

  // 서버 저장 버튼
  document.getElementById("btnSaveServer").addEventListener("click", saveToServer);

  // JSON 내보내기 / 불러오기
  document.getElementById("btnExportJson").addEventListener("click", exportJsonFile);
  document.getElementById("btnImportJson").addEventListener("click", () => {
    document.getElementById("fileInputJson").click();
  });
  document.getElementById("fileInputJson").addEventListener("change", handleImportJson);

  // API URL 복사 버튼
  document.getElementById("btnCopyApiUrl").addEventListener("click", () => {
    const url = document.getElementById("apiEndpointUrl").textContent;
    navigator.clipboard.writeText(url).then(() => {
      showToast("API 주소가 클립보드에 복사되었습니다!");
    });
  });
}

// 스마트 링크 변환기 (Google Drive, Dropbox, GitHub 등 직링크로 변환)
function convertToDirectLink(url) {
  if (!url) return url;
  let trimmed = url.trim();

  // 1. Google Drive 링크 변환
  // 예: https://drive.google.com/file/d/1a2b3c.../view?usp=sharing
  const gdriveMatch = trimmed.match(/\/file\/d\/([a-zA-Z0-9_-]+)/);
  if (gdriveMatch && gdriveMatch[1]) {
    const fileId = gdriveMatch[1];
    return `https://drive.usercontent.google.com/download?id=${fileId}&export=download`;
  }
  const gdriveIdParam = trimmed.match(/[?&]id=([a-zA-Z0-9_-]+)/);
  if (trimmed.includes("drive.google.com") && gdriveIdParam && gdriveIdParam[1]) {
    return `https://drive.usercontent.google.com/download?id=${gdriveIdParam[1]}&export=download`;
  }

  // 2. Dropbox 링크 변환
  // dl=0 -> dl=1 또는 www.dropbox.com -> dl.dropboxusercontent.com
  if (trimmed.includes("dropbox.com")) {
    if (trimmed.includes("dl=0")) {
      return trimmed.replace("dl=0", "dl=1");
    }
    if (!trimmed.includes("dl=1")) {
      const sep = trimmed.includes("?") ? "&" : "?";
      return `${trimmed}${sep}dl=1`;
    }
  }

  // 3. GitHub blob -> raw 변환
  if (trimmed.includes("github.com") && trimmed.includes("/blob/")) {
    return trimmed.replace("github.com", "raw.githubusercontent.com").replace("/blob/", "/");
  }

  return trimmed;
}

// 모달 내 URL 자동 변환 실행
function autoConvertInputUrl() {
  const urlInput = document.getElementById("modalInputUrl");
  const rawUrl = urlInput.value;
  if (!rawUrl) {
    showToast("변환할 URL을 먼저 입력해주세요.", true);
    return;
  }
  const converted = convertToDirectLink(rawUrl);
  if (converted !== rawUrl) {
    urlInput.value = converted;
    showToast("직다운로드 링크로 자동 변환되었습니다!");
  } else {
    showToast("이미 직링크 형식이거나 변환 규칙이 필요 없는 링크입니다.");
  }
  autoGuessFilename();
}

// URL로부터 파일명 자동 추출
function autoGuessFilename() {
  const url = document.getElementById("modalInputUrl").value.trim();
  const filenameInput = document.getElementById("modalInputFilename");
  if (!url || filenameInput.value.trim()) return;

  try {
    const pathname = new URL(url).pathname;
    const parts = pathname.split("/");
    const lastPart = parts[parts.length - 1];
    if (lastPart && (lastPart.endsWith(".jar") || lastPart.endsWith(".zip") || lastPart.endsWith(".json"))) {
      filenameInput.value = decodeURIComponent(lastPart);
      if (!document.getElementById("modalInputName").value) {
        document.getElementById("modalInputName").value = decodeURIComponent(lastPart).replace(/\.jar$|\.zip$/i, "");
      }
    }
  } catch (e) {
    // 무시
  }
}

// 모달 열기 (추가 또는 수정)
function openModal(fileToEdit = null) {
  const modal = document.getElementById("fileModal");
  const title = document.getElementById("modalTitle");

  if (fileToEdit) {
    title.textContent = "모드 / 파일 수정";
    document.getElementById("editFileId").value = fileToEdit.id;
    document.getElementById("modalInputName").value = fileToEdit.name || "";
    document.getElementById("modalInputUrl").value = fileToEdit.url || "";
    document.getElementById("modalInputFilename").value = fileToEdit.filename || "";

    const standardDirs = ["mods", "config", "shaderpacks", "resourcepacks"];
    if (standardDirs.includes(fileToEdit.target_dir)) {
      document.getElementById("modalInputTargetDir").value = fileToEdit.target_dir;
      document.getElementById("customTargetDirGroup").style.display = "none";
    } else {
      document.getElementById("modalInputTargetDir").value = "custom";
      document.getElementById("modalInputCustomDir").value = fileToEdit.target_dir || "";
      document.getElementById("customTargetDirGroup").style.display = "block";
    }

    document.getElementById("modalInputSize").value = fileToEdit.size_kb || "";
    document.getElementById("modalInputSha256").value = fileToEdit.sha256 || "";
    document.getElementById("modalInputDesc").value = fileToEdit.description || "";
  } else {
    title.textContent = "새 모드 / 파일 추가";
    document.getElementById("editFileId").value = "";
    document.getElementById("modalInputName").value = "";
    document.getElementById("modalInputUrl").value = "";
    document.getElementById("modalInputFilename").value = "";
    document.getElementById("modalInputTargetDir").value = "mods";
    document.getElementById("customTargetDirGroup").style.display = "none";
    document.getElementById("modalInputCustomDir").value = "";
    document.getElementById("modalInputSize").value = "";
    document.getElementById("modalInputSha256").value = "";
    document.getElementById("modalInputDesc").value = "";
  }

  modal.style.display = "flex";
}

function closeModal() {
  document.getElementById("fileModal").style.display = "none";
}

// 모달에서 입력된 파일 저장
function saveModalFile() {
  const id = document.getElementById("editFileId").value || "file-" + Date.now();
  const name = document.getElementById("modalInputName").value.trim();
  let url = document.getElementById("modalInputUrl").value.trim();
  const filename = document.getElementById("modalInputFilename").value.trim();

  if (!name || !url || !filename) {
    alert("모드 이름, 다운로드 링크, 파일명은 필수 항목입니다.");
    return;
  }

  // 링크 자동 변환 적용
  url = convertToDirectLink(url);

  let targetDir = document.getElementById("modalInputTargetDir").value;
  if (targetDir === "custom") {
    targetDir = document.getElementById("modalInputCustomDir").value.trim() || "mods";
  }

  const sizeKb = parseInt(document.getElementById("modalInputSize").value, 10) || 0;
  const sha256 = document.getElementById("modalInputSha256").value.trim();
  const description = document.getElementById("modalInputDesc").value.trim();

  const fileItem = {
    id,
    name,
    filename,
    target_dir: targetDir,
    url,
    size_kb: sizeKb,
    sha256,
    description
  };

  const existingIndex = manifestData.files.findIndex(f => f.id === id);
  if (existingIndex >= 0) {
    manifestData.files[existingIndex] = fileItem;
    showToast(`'${name}' 모드가 수정되었습니다.`);
  } else {
    manifestData.files.push(fileItem);
    showToast(`'${name}' 모드가 추가되었습니다.`);
  }

  closeModal();
  renderFileTable();
}

// 모드 파일 수정
window.editFile = function(id) {
  const file = manifestData.files.find(f => f.id === id);
  if (file) {
    openModal(file);
  }
};

// 모드 파일 삭제
window.deleteFile = function(id) {
  const file = manifestData.files.find(f => f.id === id);
  if (!file) return;

  if (confirm(`'${file.name}' (${file.filename}) 항목을 삭제하시겠습니까?`)) {
    manifestData.files = manifestData.files.filter(f => f.id !== id);
    renderFileTable();
    showToast(`'${file.name}' 항목이 삭제되었습니다.`);
  }
};

// 현재 설정값 수집
function collectCurrentManifest() {
  manifestData.pack_name = document.getElementById("inputPackName").value.trim() || "모드팩";
  manifestData.version = document.getElementById("inputVersion").value.trim() || "1.0.0";
  manifestData.game_type = document.getElementById("inputGameType").value;
  manifestData.minecraft_version = document.getElementById("inputMcVersion").value.trim();
  manifestData.loader = document.getElementById("inputLoader").value;
  manifestData.notice = document.getElementById("inputNotice").value.trim();
  manifestData.auto_cleanup = document.getElementById("inputAutoCleanup").checked;
  return manifestData;
}

// 서버에 매니페스트 저장
async function saveToServer() {
  const data = collectCurrentManifest();
  try {
    const res = await fetch("/api/manifest", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data)
    });

    if (res.ok) {
      showToast("💾 서버에 성공적으로 저장되었습니다!");
    } else {
      const errData = await res.json();
      showToast(`저장 실패: ${errData.error || "서버 오류"}`, true);
    }
  } catch (err) {
    showToast("서버 통신 실패: " + err.message, true);
  }
}

// JSON 파일로 내보내기 (다운로드)
function exportJsonFile() {
  const data = collectCurrentManifest();
  const jsonStr = JSON.stringify(data, null, 2);
  const blob = new Blob([jsonStr], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `manifest_${data.version || "1.0.0"}.json`;
  a.click();
  URL.revokeObjectURL(url);
  showToast("manifest.json 파일이 다운로드되었습니다.");
}

// JSON 파일 불러오기
function handleImportJson(e) {
  const file = e.target.files[0];
  if (!file) return;

  const reader = new FileReader();
  reader.onload = (event) => {
    try {
      const parsed = JSON.parse(event.target.result);
      if (parsed && Array.isArray(parsed.files)) {
        manifestData = parsed;
        renderConfig();
        renderFileTable();
        showToast("JSON 설정을 성공적으로 불러왔습니다. [서버에 저장]을 눌러 적용하세요.");
      } else {
        alert("올바른 manifest.json 파일 형식이 아닙니다.");
      }
    } catch (err) {
      alert("JSON 파싱 에러: " + err.message);
    }
  };
  reader.readAsText(file);
  e.target.value = "";
}

// 토스트 메시지 표시
function showToast(msg, isError = false) {
  const toast = document.getElementById("toast");
  toast.textContent = msg;
  toast.className = "toast show" + (isError ? " error" : "");
  setTimeout(() => {
    toast.className = "toast";
  }, 3000);
}

// XSS 방지 이스케이프
function escapeHtml(str) {
  if (!str) return "";
  return str.replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
}
