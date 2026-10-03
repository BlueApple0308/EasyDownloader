using System;
using System.Collections.Generic;

namespace EasyDownloader
{
    // mods.txt 파싱 결과
    public class ModpackConfig
    {
        public string GameVersion;   // 예: "1.20.1"
        public string Loader;        // 예: "fabric"
        public List<ModEntry> Entries;

        public ModpackConfig()
        {
            GameVersion = "";
            Loader = "fabric";
            Entries = new List<ModEntry>();
        }
    }

    // 개별 모드/쉐이더/리소스팩 항목
    public class ModEntry
    {
        public string OriginalUrl;     // 유저가 적은 원본 URL
        public string Slug;            // modrinth slug (예: "sodium")
        public string ProjectType;     // "mod", "shader", "resourcepack" 또는 "direct"
        public string Title;           // Modrinth에서 가져온 모드 이름
        public string Description;     // 짧은 설명
        public string DownloadUrl;     // 실제 파일 다운로드 URL
        public string FileName;        // 저장할 파일명 (예: "sodium-0.5.11.jar")
        public long FileSize;          // 파일 크기 (bytes)
        public string TargetFolder;    // 대상 폴더 ("mods", "shaderpacks", "resourcepacks")
        public bool IsSelected;        // 체크박스 선택 여부
        public bool IsDirectLink;      // Modrinth가 아닌 직링크인지
        public string Status;          // "pending", "ready", "downloading", "done", "error", "skipped"
        public string ErrorMessage;    // 에러 발생 시 메시지
        public string ProjectId;       // Modrinth 고유 project ID (예: "ohNO6lps")
        public bool IsDependency;      // 하위 의존성(필수 라이브러리)으로 자동 추가되었는지 여부
        public List<string> RequiredDependencyIds; // 필수 의존 모드의 project_id 목록

        public ModEntry()
        {
            IsSelected = true;
            Status = "pending";
            TargetFolder = "mods";
            Title = "";
            Description = "";
            DownloadUrl = "";
            FileName = "";
            ErrorMessage = "";
            ProjectId = "";
            IsDependency = false;
            RequiredDependencyIds = new List<string>();
        }

        // 타입에 따른 한글 카테고리명
        public string GetCategoryName()
        {
            if (ProjectType == "shader") return "쉐이더팩";
            if (ProjectType == "resourcepack") return "리소스팩";
            if (ProjectType == "datapack") return "데이터팩";
            return "모드";
        }

        // 파일 크기 포맷
        public string GetSizeText()
        {
            if (FileSize <= 0) return "";
            if (FileSize < 1024) return FileSize + " B";
            if (FileSize < 1024 * 1024) return string.Format("{0:F1} KB", FileSize / 1024.0);
            return string.Format("{0:F1} MB", FileSize / (1024.0 * 1024.0));
        }

        // 체크박스 표시용 텍스트
        public string GetDisplayText()
        {
            string sizeText = GetSizeText();
            string sizeInfo = string.IsNullOrEmpty(sizeText) ? "" : " (" + sizeText + ")";

            if (!string.IsNullOrEmpty(Title))
            {
                return string.Format("[{0}] {1}{2}", GetCategoryName(), Title, sizeInfo);
            }
            if (!string.IsNullOrEmpty(Slug))
            {
                return string.Format("[{0}] {1}{2}", GetCategoryName(), Slug, sizeInfo);
            }
            return string.Format("[{0}] {1}{2}", GetCategoryName(), FileName, sizeInfo);
        }
    }
}
