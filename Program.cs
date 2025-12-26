using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        private const string ShortcutName = "oCam录屏.lnk";

        [STAThread]
        public static void Main(string[] args)
        {
            CheckAndCreateDesktopShortcut();

            if (!StartOcam())
                return;

            AdWindowKiller.ResizeAndCenterWindow(OcamExecutable, OcamWindowClass, DefaultWindowWidth, DefaultWindowHeight);
            AdWindowKiller.MonitorByProcessName(OcamExecutable);
        }

        private static bool StartOcam()
        {
            string ocamFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, OcamFolder);
            string appPath = Path.Combine(ocamFolder, OcamExecutable);

            if (!File.Exists(appPath))
            {
                MessageBox.Show($"找不到文件：\n{appPath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = appPath,
                    WorkingDirectory = Path.GetDirectoryName(appPath),
                    UseShellExecute = false
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static void CheckAndCreateDesktopShortcut()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, ShortcutName);

            if (File.Exists(shortcutPath))
                return;

            DialogResult result = MessageBox.Show(
                "是否在桌面创建快捷方式？",
                "提示",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                CreateShortcut(shortcutPath);
            }
        }

        private static void CreateShortcut(string shortcutPath)
        {
            object shell = null;
            object shortcut = null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(shellType);

                object[] args = new object[] { shortcutPath };
                shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, args);

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Process.GetCurrentProcess().MainModule.FileName });
                shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { AppDomain.CurrentDomain.BaseDirectory });
                shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "oCam录屏启动器" });
                shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch
            {
                // 忽略快捷方式创建失败
            }
            finally
            {
                if (shortcut != null)
                    Marshal.FinalReleaseComObject(shortcut);
                if (shell != null)
                    Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
