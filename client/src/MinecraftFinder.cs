using System;
using System.IO;

namespace EasyDownloader
{
    public static class MinecraftFinder
    {
        // 마인크래프트 기본 설치 경로 반환
        public static string GetDefaultMinecraftPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string mcPath = Path.Combine(appData, ".minecraft");
            return mcPath;
        }

        // 경로 유효성 검증
        public static bool IsValidGameDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                // 경로가 존재하거나 생성 가능한지 확인
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
