using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace EasyDownloader
{
    // Fabric 로더 자동 설치기 (Fabric Meta API 활용)
    // Java 없이도 동작: 프로필 JSON을 직접 생성하여 MC 런처가 나머지를 처리하도록 함
    public static class FabricInstaller
    {
        private const string META_BASE = "https://meta.fabricmc.net/v2";
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // Fabric이 이미 설치되어 있는지 확인
        public static bool IsFabricInstalled(string mcDir, string gameVersion)
        {
            string versionsDir = Path.Combine(mcDir, "versions");
            if (!Directory.Exists(versionsDir)) return false;

            // fabric-loader-*-{gameVersion} 패턴의 폴더가 있는지 확인
            string[] dirs = Directory.GetDirectories(versionsDir, "fabric-loader-*");
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir);
                if (name.EndsWith("-" + gameVersion))
                {
                    // JSON 파일 및 더미 JAR 파일 확인 (MC 런처 인식 필수)
                    string jsonFile = Path.Combine(dir, name + ".json");
                    string jarFile = Path.Combine(dir, name + ".jar");
                    if (File.Exists(jsonFile))
                    {
                        if (!File.Exists(jarFile))
                        {
                            try { File.WriteAllBytes(jarFile, new byte[0]); } catch { }
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        // Fabric 로더 설치 실행
        // 반환: (성공 여부, 설치된 버전 이름 또는 에러 메시지)
        public static KeyValuePair<bool, string> Install(string mcDir, string gameVersion)
        {
            try
            {
                // 1. 최신 Fabric 로더 버전 조회
                string loaderVersion = GetLatestLoaderVersion(gameVersion);
                if (string.IsNullOrEmpty(loaderVersion))
                {
                    return new KeyValuePair<bool, string>(false,
                        string.Format("Fabric 로더가 마인크래프트 {0} 버전을 지원하지 않습니다.", gameVersion));
                }

                string versionName = string.Format("fabric-loader-{0}-{1}", loaderVersion, gameVersion);

                // 이미 설치되어 있다면 프로필 등록 확인 후 완료 처리
                if (IsFabricInstalled(mcDir, gameVersion))
                {
                    RegisterLauncherProfile(mcDir, versionName, gameVersion);
                    return new KeyValuePair<bool, string>(true, "이미 설치됨 (런처 프로필 갱신)");
                }

                // 2. 프로필 JSON 다운로드
                string profileJson = DownloadProfileJson(gameVersion, loaderVersion);
                if (string.IsNullOrEmpty(profileJson))
                {
                    return new KeyValuePair<bool, string>(false, "Fabric 프로필 JSON을 가져오지 못했습니다.");
                }

                // 3. 버전 폴더 생성 및 JSON 저장
                string versionDir = Path.Combine(mcDir, "versions", versionName);
                if (!Directory.Exists(versionDir))
                {
                    Directory.CreateDirectory(versionDir);
                }

                string jsonPath = Path.Combine(versionDir, versionName + ".json");
                File.WriteAllText(jsonPath, profileJson, Utf8NoBom);

                // 마인크래프트 런처 버전 인식 필수: 더미 JAR 파일 생성 (Fabric 공식 방식)
                string jarPath = Path.Combine(versionDir, versionName + ".jar");
                if (!File.Exists(jarPath))
                {
                    File.WriteAllBytes(jarPath, new byte[0]);
                }

                // 4. MC 런처 프로필에 Fabric 프로필 등록 (launcher_profiles.json)
                RegisterLauncherProfile(mcDir, versionName, gameVersion);

                return new KeyValuePair<bool, string>(true, versionName);
            }
            catch (Exception ex)
            {
                return new KeyValuePair<bool, string>(false, "Fabric 설치 오류: " + ex.Message);
            }
        }

        // 최신 Fabric 로더 버전 조회
        private static string GetLatestLoaderVersion(string gameVersion)
        {
            string url = META_BASE + "/versions/loader/" + Uri.EscapeDataString(gameVersion);
            string json = DownloadString(url);

            object[] versions = AsArray(Serializer.DeserializeObject(json));
            if (versions == null || versions.Length == 0) return null;

            // 첫 번째 항목이 최신 안정 버전
            var first = versions[0] as Dictionary<string, object>;
            if (first == null) return null;

            if (first.ContainsKey("loader"))
            {
                var loaderInfo = first["loader"] as Dictionary<string, object>;
                if (loaderInfo != null && loaderInfo.ContainsKey("version"))
                {
                    return loaderInfo["version"].ToString();
                }
            }

            return null;
        }

        // Fabric 프로필 JSON 다운로드
        private static string DownloadProfileJson(string gameVersion, string loaderVersion)
        {
            string url = string.Format("{0}/versions/loader/{1}/{2}/profile/json",
                META_BASE,
                Uri.EscapeDataString(gameVersion),
                Uri.EscapeDataString(loaderVersion));

            return DownloadString(url);
        }

        // MC 런처에 Fabric 프로필 자동 등록
        private static void RegisterLauncherProfile(string mcDir, string versionName, string gameVersion)
        {
            string profilesPath = Path.Combine(mcDir, "launcher_profiles.json");
            if (!File.Exists(profilesPath)) return;

            try
            {
                string content = File.ReadAllText(profilesPath, Encoding.UTF8);
                var data = Serializer.DeserializeObject(content) as Dictionary<string, object>;
                if (data == null) return;

                if (!data.ContainsKey("profiles")) return;
                var profiles = data["profiles"] as Dictionary<string, object>;
                if (profiles == null) return;

                string profileKey = "fabric-" + gameVersion;

                // 새 프로필 생성 또는 갱신
                var newProfile = new Dictionary<string, object>();
                newProfile["name"] = "Fabric " + gameVersion;
                newProfile["type"] = "custom";
                newProfile["icon"] = "Furnace";
                newProfile["lastVersionId"] = versionName;
                newProfile["javaArgs"] = "-Xmx8G -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M";
                newProfile["created"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                newProfile["lastUsed"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                profiles[profileKey] = newProfile;

                string newJson = Serializer.Serialize(data);
                File.WriteAllText(profilesPath, newJson, Utf8NoBom);
            }
            catch
            {
                // 프로필 등록 실패는 치명적이지 않으므로 무시
                // 유저가 런처에서 수동으로 Fabric 버전을 선택하면 됨
            }
        }

        private static string DownloadString(string url)
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("User-Agent", "seojy/EasyDownloader/2.0");
                return client.DownloadStringTaskAsync(new Uri(url)).Result;
            }
        }

        private static object[] AsArray(object obj)
        {
            if (obj == null) return null;
            if (obj is object[]) return (object[])obj;
            if (obj is ArrayList) return ((ArrayList)obj).ToArray();
            if (obj is IEnumerable)
            {
                var list = new List<object>();
                foreach (var item in (IEnumerable)obj) list.Add(item);
                return list.ToArray();
            }
            return null;
        }
    }
}
