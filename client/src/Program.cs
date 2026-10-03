using System;
using System.Windows.Forms;

namespace EasyDownloader
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // TLS 1.2 활성화 (Modrinth 및 Fabric Meta API 연동 필수)
            try
            {
                System.Net.ServicePointManager.SecurityProtocol =
                    System.Net.SecurityProtocolType.Tls12 |
                    (System.Net.SecurityProtocolType)768 |
                    (System.Net.SecurityProtocolType)3072;
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // exe에 파일을 직접 드래그 앤 드롭한 경우
            // (탐색기에서 mods.txt를 EasyDownloader.exe 위에 끌어다 놓으면 args[0]에 경로 전달)
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                Application.Run(new MainForm(args[0]));
            }
            else
            {
                Application.Run(new MainForm());
            }
        }
    }
}
