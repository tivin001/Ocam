using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OcamLauncher
{
    public class Program
    {
        private const string OcamFolder = "App";
        private const string OcamExecutable = "oCam.exe";
        private const string OcamWindowClass = "Hi! oCam";
        private const int DefaultWindowWidth = 500;
        private const int DefaultWindowHeight = 110;

        [STAThread]
        public static void Main(string[] args)
        {
            Process ocamProcess = StartOcam();
            if (ocamProcess is null)
            {
                return;
            }

            AdWindowKiller.ResizeAndCenterWindow(OcamExecutable, OcamWindowClass, DefaultWindowWidth, DefaultWindowHeight);
            AdWindowKiller.MonitorByProcessName(OcamExecutable);
        }

        private static Process StartOcam()
        {
            string appPath = GetOcamPath();

            if (!File.Exists(appPath))
            {
                ShowErrorMessage($"找不到文件：\n{appPath}");
                return null;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath),
                UseShellExecute = false
            };

            try
            {
                Process process = Process.Start(startInfo);
                return process;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"启动失败：{ex.Message}");
                return null;
            }
        }

        private static string GetOcamPath()
        {
            return Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, OcamFolder), OcamExecutable);
        }

        private static void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
