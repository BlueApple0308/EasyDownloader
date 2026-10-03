using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EasyDownloader
{
    // 다운로드 엔진 v2
    public class DownloaderCore
    {
        public DownloaderCore()
        {
            // TLS 1.2 필수 활성화 (Modrinth CDN, GitHub 등 HTTPS 필수)
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    (SecurityProtocolType)768 |
                    (SecurityProtocolType)3072;
            }
            catch { }
        }

        // 단일 파일 다운로드 (스트리밍 + 진행률 보고)
        public async Task DownloadFileAsync(
            ModEntry entry,
            string gameRoot,
            IProgress<DownloadProgress> progress)
        {
            string destFolder = Path.Combine(gameRoot, entry.TargetFolder);
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            string destPath = Path.Combine(destFolder, entry.FileName);

            // 이미 동일 파일이 존재하고 크기가 같으면 스킵
            if (File.Exists(destPath) && entry.FileSize > 0)
            {
                var fi = new FileInfo(destPath);
                if (fi.Length == entry.FileSize)
                {
                    entry.Status = "done";
                    if (progress != null)
                    {
                        progress.Report(new DownloadProgress
                        {
                            EntryTitle = entry.Title,
                            Message = entry.Title + " - 이미 최신 버전 (스킵)",
                            Percent = 100
                        });
                    }
                    return;
                }
            }

            // 임시 파일로 다운로드 후 완료 시 이동
            string tempPath = destPath + ".tmp";

            entry.Status = "downloading";

            var req = (HttpWebRequest)WebRequest.Create(entry.DownloadUrl);
            req.UserAgent = "seojy/EasyDownloader/2.0 (Mozilla/5.0)";
            req.Timeout = 60000;
            req.AllowAutoRedirect = true;

            using (var resp = await req.GetResponseAsync())
            using (var responseStream = resp.GetResponseStream())
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long totalBytes = resp.ContentLength;
                byte[] buffer = new byte[16384];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    int percent = 0;
                    if (totalBytes > 0)
                    {
                        percent = (int)((totalRead * 100) / totalBytes);
                    }

                    if (progress != null)
                    {
                        string sizeInfo = "";
                        if (totalBytes > 0)
                        {
                            sizeInfo = string.Format(" ({0:F1}/{1:F1} MB)",
                                totalRead / (1024.0 * 1024.0),
                                totalBytes / (1024.0 * 1024.0));
                        }

                        progress.Report(new DownloadProgress
                        {
                            EntryTitle = entry.Title,
                            Message = string.Format("{0}{1} - {2}%", entry.Title, sizeInfo, percent),
                            Percent = percent
                        });
                    }
                }
            }

            // 기존 파일이 있으면 삭제 후 임시 파일 이동
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            File.Move(tempPath, destPath);
            entry.Status = "done";
        }

        // 구버전 / 미등록 모드 자동 정리 (백업 폴더로 안전 이동)
        public int CleanupOldFiles(string gameRoot, string targetFolder, HashSet<string> registeredFiles)
        {
            string dir = Path.Combine(gameRoot, targetFolder);
            if (!Directory.Exists(dir)) return 0;

            int cleaned = 0;
            string backupDir = Path.Combine(dir, "_old_backup");

            // 대상 폴더의 모든 관련 파일 검사
            string[] patterns;
            if (targetFolder == "mods")
                patterns = new string[] { "*.jar" };
            else if (targetFolder == "shaderpacks")
                patterns = new string[] { "*.zip", "*.jar" };
            else if (targetFolder == "resourcepacks")
                patterns = new string[] { "*.zip", "*.jar" };
            else
                patterns = new string[] { "*.jar", "*.zip" };

            foreach (string pattern in patterns)
            {
                string[] files = Directory.GetFiles(dir, pattern);
                foreach (string file in files)
                {
                    string filename = Path.GetFileName(file).ToLowerInvariant();
                    if (!registeredFiles.Contains(filename))
                    {
                        try
                        {
                            if (!Directory.Exists(backupDir))
                                Directory.CreateDirectory(backupDir);

                            string backupPath = Path.Combine(backupDir, Path.GetFileName(file));
                            if (File.Exists(backupPath)) File.Delete(backupPath);
                            File.Move(file, backupPath);
                            cleaned++;
                        }
                        catch { }
                    }
                }
            }

            return cleaned;
        }
    }

    // 다운로드 진행률 보고 객체
    public class DownloadProgress
    {
        public string EntryTitle;
        public string Message;
        public int Percent;
    }
}
