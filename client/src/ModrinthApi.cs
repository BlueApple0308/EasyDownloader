using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace EasyDownloader
{
    // Modrinth 공식 API v2 연동
    public static class ModrinthApi
    {
        private const string API_BASE = "https://api.modrinth.com/v2";
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        static ModrinthApi()
        {
            Serializer.MaxJsonLength = 10 * 1024 * 1024; // 10MB
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    (SecurityProtocolType)768 |
                    (SecurityProtocolType)3072;
            }
            catch { }
        }

        // 단일 프로젝트 정보 + 버전 매칭된 다운로드 URL을 한번에 가져오기
        public static void ResolveEntry(ModEntry entry, string gameVersion, string loader)
        {
            try
            {
                // 1단계: 프로젝트 기본 정보 가져오기
                FetchProjectInfo(entry);

                // 2단계: 지정된 게임 버전에 맞는 파일 찾기
                FetchVersionFile(entry, gameVersion, loader);

                if (!string.IsNullOrEmpty(entry.DownloadUrl))
                {
                    entry.Status = "ready";
                }
                else
                {
                    entry.Status = "error";
                    entry.ErrorMessage = string.Format(
                        "'{0}' 버전에 맞는 파일을 찾을 수 없습니다.", gameVersion);
                }
            }
            catch (WebException wex)
            {
                entry.Status = "error";
                var resp = wex.Response as HttpWebResponse;
                if (resp != null && resp.StatusCode == HttpStatusCode.NotFound)
                {
                    entry.ErrorMessage = string.Format(
                        "Modrinth에서 '{0}' 프로젝트를 찾을 수 없습니다.", entry.Slug);
                }
                else
                {
                    entry.ErrorMessage = "네트워크 오류: " + wex.Message;
                }
            }
            catch (Exception ex)
            {
                entry.Status = "error";
                entry.ErrorMessage = "API 오류: " + ex.Message;
            }
        }

        // 프로젝트 기본 정보 (이름, 설명, 타입)
        private static void FetchProjectInfo(ModEntry entry)
        {
            string url = API_BASE + "/project/" + Uri.EscapeDataString(entry.Slug);
            string json = DownloadString(url);
            var data = Serializer.DeserializeObject(json) as Dictionary<string, object>;

            if (data == null) return;

            if (data.ContainsKey("id"))
                entry.ProjectId = SafeStr(data["id"]);

            if (data.ContainsKey("title"))
                entry.Title = SafeStr(data["title"]);

            if (data.ContainsKey("description"))
                entry.Description = SafeStr(data["description"]);

            // API에서 반환한 project_type으로 폴더 매핑 재확인
            if (data.ContainsKey("project_type"))
            {
                string apiType = SafeStr(data["project_type"]).ToLowerInvariant();
                switch (apiType)
                {
                    case "mod":
                        entry.ProjectType = "mod";
                        entry.TargetFolder = "mods";
                        break;
                    case "shader":
                        entry.ProjectType = "shader";
                        entry.TargetFolder = "shaderpacks";
                        break;
                    case "resourcepack":
                        entry.ProjectType = "resourcepack";
                        entry.TargetFolder = "resourcepacks";
                        break;
                    case "datapack":
                        entry.ProjectType = "datapack";
                        entry.TargetFolder = "datapacks";
                        break;
                }
            }
        }

        // 지정된 게임 버전 + 로더에 맞는 파일 URL 찾기
        private static void FetchVersionFile(ModEntry entry, string gameVersion, string loader)
        {
            // 쿼리 URL 구성
            string url = API_BASE + "/project/" + Uri.EscapeDataString(entry.Slug) + "/version";

            // 게임 버전 필터
            string query = "?game_versions=" + Uri.EscapeDataString("[\"" + gameVersion + "\"]");

            // 모드 타입일 때만 로더 필터 적용 (쉐이더/리소스팩은 로더 무관)
            if (entry.ProjectType == "mod" && !string.IsNullOrEmpty(loader))
            {
                query += "&loaders=" + Uri.EscapeDataString("[\"" + loader + "\"]");
            }

            string json = DownloadString(url + query);
            object[] versions = AsArray(Serializer.DeserializeObject(json));

            if (versions == null || versions.Length == 0)
            {
                // 1) 쉐이더나 리소스팩은 마크 버전에 상관없이 호환되므로 버전 필터 없이 최신 파일 재조회
                if (entry.ProjectType == "shader" || entry.ProjectType == "resourcepack")
                {
                    json = DownloadString(url);
                    versions = AsArray(Serializer.DeserializeObject(json));
                }
                // 2) 로더 필터 없이 재시도 (쉐이더/리소스팩이 mod 타입으로 잘못 분류된 경우)
                else if (entry.ProjectType == "mod")
                {
                    string retryUrl = url + "?game_versions=" + Uri.EscapeDataString("[\"" + gameVersion + "\"]");
                    json = DownloadString(retryUrl);
                    versions = AsArray(Serializer.DeserializeObject(json));
                }

                if (versions == null || versions.Length == 0) return;
            }

            // release 타입 우선, 없으면 첫 번째 버전 사용
            Dictionary<string, object> bestVersion = null;

            foreach (object v in versions)
            {
                var ver = v as Dictionary<string, object>;
                if (ver == null) continue;

                string versionType = ver.ContainsKey("version_type") ? SafeStr(ver["version_type"]) : "";

                if (versionType == "release")
                {
                    bestVersion = ver;
                    break;
                }

                if (bestVersion == null)
                {
                    bestVersion = ver;
                }
            }

            if (bestVersion == null) return;

            // 필수 의존성 목록 수집 (required dependency)
            if (bestVersion.ContainsKey("dependencies"))
            {
                object[] deps = AsArray(bestVersion["dependencies"]);
                if (deps != null)
                {
                    foreach (object d in deps)
                    {
                        var dep = d as Dictionary<string, object>;
                        if (dep == null) continue;
                        string depType = dep.ContainsKey("dependency_type") ? SafeStr(dep["dependency_type"]) : "";
                        if (depType == "required")
                        {
                            string pid = dep.ContainsKey("project_id") ? SafeStr(dep["project_id"]) : "";
                            if (!string.IsNullOrEmpty(pid) && !entry.RequiredDependencyIds.Contains(pid))
                            {
                                entry.RequiredDependencyIds.Add(pid);
                            }
                        }
                    }
                }
            }

            // files 배열에서 primary 파일 찾기
            if (!bestVersion.ContainsKey("files")) return;
            object[] files = AsArray(bestVersion["files"]);
            if (files == null || files.Length == 0) return;

            Dictionary<string, object> bestFile = null;

            foreach (object f in files)
            {
                var file = f as Dictionary<string, object>;
                if (file == null) continue;

                bool isPrimary = file.ContainsKey("primary") && (file["primary"] is bool) && (bool)file["primary"];
                if (isPrimary || bestFile == null)
                {
                    bestFile = file;
                    if (isPrimary) break;
                }
            }

            if (bestFile == null) return;

            entry.DownloadUrl = bestFile.ContainsKey("url") ? SafeStr(bestFile["url"]) : "";
            entry.FileName = bestFile.ContainsKey("filename") ? SafeStr(bestFile["filename"]) : "";

            if (bestFile.ContainsKey("size"))
            {
                try { entry.FileSize = Convert.ToInt64(bestFile["size"]); }
                catch { }
            }
        }

        // HTTP GET 요청
        private static string DownloadString(string url)
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("User-Agent", "seojy/EasyDownloader/2.0 (game-mod-installer)");
                return client.DownloadStringTaskAsync(new Uri(url)).Result;
            }
        }

        // null-safe 문자열 변환
        private static string SafeStr(object obj)
        {
            if (obj == null) return "";
            return obj.ToString();
        }

        // JavaScriptSerializer 결과(object[] 또는 ArrayList)를 안전하게 배열로 변환
        private static object[] AsArray(object obj)
        {
            if (obj == null) return null;
            if (obj is object[]) return (object[])obj;
            if (obj is IEnumerable)
            {
                var list = new List<object>();
                foreach (var item in (IEnumerable)obj) list.Add(item);
                return list.ToArray();
            }
            return null;
        }

        // 프로젝트 ID를 기반으로 ModEntry 객체 생성 (하위 의존성 자동 로드용)
        public static ModEntry CreateEntryFromProjectId(string projectId)
        {
            try
            {
                string url = API_BASE + "/project/" + Uri.EscapeDataString(projectId);
                string json = DownloadString(url);
                var data = Serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (data == null) return null;

                var entry = new ModEntry();
                entry.ProjectId = projectId;
                entry.Slug = data.ContainsKey("slug") ? SafeStr(data["slug"]) : projectId;
                entry.Title = data.ContainsKey("title") ? SafeStr(data["title"]) : entry.Slug;
                entry.Description = data.ContainsKey("description") ? SafeStr(data["description"]) : "";
                entry.ProjectType = "mod";
                entry.TargetFolder = "mods";
                entry.IsDependency = true;
                entry.OriginalUrl = "https://modrinth.com/mod/" + entry.Slug;
                return entry;
            }
            catch
            {
                return null;
            }
        }
    }
}
