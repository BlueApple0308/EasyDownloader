using System;
using System.Collections.Generic;
using System.IO;

namespace EasyDownloader
{
    // mods.txt 파일 파서
    public static class ModpackParser
    {
        public static ModpackConfig ParseFile(string filePath)
        {
            var config = new ModpackConfig();

            if (!File.Exists(filePath))
                throw new FileNotFoundException("파일을 찾을 수 없습니다: " + filePath);

            string[] lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                // 빈 줄이나 주석(#)은 건너뜀
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // VERSION= 지시어
                if (line.StartsWith("VERSION=", StringComparison.OrdinalIgnoreCase))
                {
                    config.GameVersion = line.Substring("VERSION=".Length).Trim();
                    continue;
                }

                // LOADER= 지시어
                if (line.StartsWith("LOADER=", StringComparison.OrdinalIgnoreCase))
                {
                    config.Loader = line.Substring("LOADER=".Length).Trim().ToLowerInvariant();
                    continue;
                }

                // URL 파싱
                if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    ModEntry entry = ParseUrl(line);
                    if (entry != null)
                    {
                        config.Entries.Add(entry);
                    }
                }
            }

            return config;
        }

        private static ModEntry ParseUrl(string url)
        {
            var entry = new ModEntry();
            entry.OriginalUrl = url.Trim();

            // Modrinth URL 파싱: https://modrinth.com/{type}/{slug}
            if (url.Contains("modrinth.com/"))
            {
                entry.IsDirectLink = false;
                ParseModrinthUrl(entry, url);
            }
            else
            {
                // 일반 직링크 (직접 다운로드 URL)
                entry.IsDirectLink = true;
                entry.ProjectType = "mod";
                entry.TargetFolder = "mods";
                entry.DownloadUrl = url;

                // URL에서 파일명 추출 시도
                try
                {
                    Uri uri = new Uri(url);
                    string lastSegment = uri.Segments[uri.Segments.Length - 1];
                    lastSegment = Uri.UnescapeDataString(lastSegment).Trim('/');
                    if (!string.IsNullOrEmpty(lastSegment) && lastSegment.Contains("."))
                    {
                        entry.FileName = lastSegment;
                        entry.Title = lastSegment;
                    }
                    else
                    {
                        entry.FileName = "download_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        entry.Title = url;
                    }
                }
                catch
                {
                    entry.FileName = "download_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    entry.Title = url;
                }
                entry.Status = "ready"; // 직링크는 API 조회 불필요
            }

            return entry;
        }

        private static void ParseModrinthUrl(ModEntry entry, string url)
        {
            // https://modrinth.com/mod/sodium  ->  type="mod", slug="sodium"
            // https://modrinth.com/shader/complementary-unbound  ->  type="shader"
            // https://modrinth.com/resourcepack/faithful-32x  ->  type="resourcepack"

            try
            {
                Uri uri = new Uri(url);
                string path = uri.AbsolutePath.Trim('/');  // "mod/sodium"
                string[] parts = path.Split('/');

                if (parts.Length >= 2)
                {
                    string type = parts[0].ToLowerInvariant();
                    string slug = parts[1].ToLowerInvariant();

                    entry.Slug = slug;
                    entry.Title = slug; // API 조회 전 임시 이름

                    switch (type)
                    {
                        case "mod":
                            entry.ProjectType = "mod";
                            entry.TargetFolder = "mods";
                            break;
                        case "shader":
                        case "shaders":
                            entry.ProjectType = "shader";
                            entry.TargetFolder = "shaderpacks";
                            break;
                        case "resourcepack":
                        case "resourcepacks":
                            entry.ProjectType = "resourcepack";
                            entry.TargetFolder = "resourcepacks";
                            break;
                        case "datapack":
                        case "datapacks":
                            entry.ProjectType = "datapack";
                            entry.TargetFolder = "datapacks";
                            break;
                        case "plugin":
                        case "plugins":
                            entry.ProjectType = "plugin";
                            entry.TargetFolder = "plugins";
                            break;
                        default:
                            entry.ProjectType = "mod";
                            entry.TargetFolder = "mods";
                            break;
                    }
                }
                else
                {
                    entry.Slug = path;
                    entry.ProjectType = "mod";
                    entry.TargetFolder = "mods";
                }
            }
            catch
            {
                entry.Slug = url;
                entry.ProjectType = "mod";
                entry.TargetFolder = "mods";
            }
        }
    }
}
