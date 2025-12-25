using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OcamLauncher
{
    static class AdWindowKiller
    {
        #region 常量定义

        private const int SW_HIDE = 0;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int MaxRetries = 50;
        private const int RetryIntervalMs = 100;
        private const int ProcessNotFoundThreshold = 3;
        private const int PollingIntervalMs = 50;
        private const string AdWindowClassName = "TfrmBuyPage";
        private const int ClassNameBufferCapacity = 256;

        #endregion

        #region 字段

        private static string _targetProcessName = null;
        private static IntPtr _hookHandle = IntPtr.Zero;
        private static WinEventDelegate _winEventDelegate;

        #endregion

        #region 公共方法

        public static void ResizeAndCenterWindow(string processName, string className, int width, int height)
        {
            if (string.IsNullOrEmpty(processName) || processName.Trim().Length == 0)
            {
                Console.WriteLine("警告：进程名称不能为空");
                return;
            }

            string targetProcess = processName.Replace(".exe", "");

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(targetProcess);
                    if (processes.Length == 0)
                    {
                        Thread.Sleep(RetryIntervalMs);
                        continue;
                    }

                    foreach (var process in processes)
                    {
                        IntPtr foundWindow = FindWindowByProcessAndClass(process.Id, className);
                        if (foundWindow != IntPtr.Zero)
                        {
                            SetWindowCentered(foundWindow, width, height);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"调整窗口时发生错误：{ex.Message}");
                }

                Thread.Sleep(RetryIntervalMs);
            }

            Console.WriteLine($"警告：未找到类名为 '{className}' 的窗口");
        }

        public static void MonitorByProcessName(string processName)
        {
            _targetProcessName = processName.Replace(".exe", "");
            _winEventDelegate = new WinEventDelegate(WinEventProc);

            if (!InstallEventHook())
            {
                Console.WriteLine("警告：无法安装窗口事件钩子，使用轮询模式");
                MonitorByPolling();
                return;
            }

            Console.WriteLine("事件钩子已安装，正在监控...");

            Thread monitorThread = new Thread(MonitorProcessExit)
            {
                IsBackground = true
            };
            monitorThread.Start();

            RunMessageLoop();

            CleanupHook();
        }

        #endregion

        #region 私有方法

        private static IntPtr FindWindowByProcessAndClass(int processId, string className)
        {
            IntPtr result = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out int pid);
                if (pid == processId && IsWindowVisible(hWnd))
                {
                    string cls = GetClassNameValue(hWnd);
                    if (cls == className)
                    {
                        result = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        private static void SetWindowCentered(IntPtr hWnd, int clientWidth, int clientHeight)
        {
            // 先恢复窗口到正常状态
            ShowWindow(hWnd, 9); // SW_RESTORE = 9

            int style = GetWindowLong(hWnd, GWL_STYLE);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            RECT rect = new RECT { Left = 0, Top = 0, Right = clientWidth, Bottom = clientHeight };
            AdjustWindowRectEx(ref rect, style, false, exStyle);

            int actualWidth = rect.Right - rect.Left;
            int actualHeight = rect.Bottom - rect.Top;

            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            int x = screenWidth - actualWidth - 200;
            int y = (screenHeight - actualHeight) / 2;

            SetWindowPos(hWnd, IntPtr.Zero, x, y, actualWidth, actualHeight,
                SWP_NOZORDER | SWP_SHOWWINDOW | SWP_FRAMECHANGED);

            Console.WriteLine($"已调整窗口客户区尺寸为 {clientWidth}x{clientHeight}，实际窗口尺寸为 {actualWidth}x{actualHeight}");
        }

        private static bool InstallEventHook()
        {
            _hookHandle = SetWinEventHook(
                EVENT_OBJECT_SHOW,
                EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            return _hookHandle != IntPtr.Zero;
        }

        private static void RunMessageLoop()
        {
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private static void CleanupHook()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hWnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0 || !IsWindowVisible(hWnd))
                return;

            GetWindowThreadProcessId(hWnd, out int pid);

            if (!IsTargetProcess(pid))
                return;

            string className = GetClassNameValue(hWnd);
            if (className == AdWindowClassName)
            {
                HandleAdWindow(hWnd, pid);
            }
        }

        private static bool IsTargetProcess(int pid)
        {
            try
            {
                Process p = Process.GetProcessById(pid);
                return p.ProcessName.Equals(_targetProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void HandleAdWindow(IntPtr hWnd, int pid)
        {
            ShowWindow(hWnd, SW_HIDE);

            try
            {
                Process p = Process.GetProcessById(pid);
                p.Kill();
            }
            catch
            {
                // 进程可能已退出
            }
        }

        private static void MonitorProcessExit()
        {
            int notFoundCount = 0;

            while (true)
            {
                Process[] processes = Process.GetProcessesByName(_targetProcessName);

                if (processes.Length == 0)
                {
                    notFoundCount++;
                    if (notFoundCount >= ProcessNotFoundThreshold)
                    {
                        Environment.Exit(0);
                    }
                }
                else
                {
                    notFoundCount = 0;
                }

                Thread.Sleep(1000);
            }
        }

        private static void MonitorByPolling()
        {
            int notFoundCount = 0;

            while (true)
            {
                Process[] processes = Process.GetProcessesByName(_targetProcessName);

                if (processes.Length == 0)
                {
                    notFoundCount++;
                    if (notFoundCount >= ProcessNotFoundThreshold)
                    {
                        Environment.Exit(0);
                    }
                    Thread.Sleep(1000);
                    continue;
                }

                notFoundCount = 0;

                foreach (var p in processes)
                {
                    if (!p.HasExited)
                    {
                        EnumWindows((hWnd, lParam) =>
                        {
                            int targetPid = (int)lParam;

                            if (!IsWindowVisible(hWnd))
                                return true;

                            GetWindowThreadProcessId(hWnd, out int pid);
                            if (pid != targetPid)
                                return true;

                            string cls = GetClassNameValue(hWnd);
                            if (cls == AdWindowClassName)
                            {
                                HandleAdWindow(hWnd, pid);
                            }

                            return true;
                        }, (IntPtr)p.Id);
                    }
                }

                Thread.Sleep(PollingIntervalMs);
            }
        }

        private static string GetClassNameValue(IntPtr hWnd)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(ClassNameBufferCapacity);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        #endregion

        #region P/Invoke 声明

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y,
            int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool AdjustWindowRectEx(ref RECT lpRect, int dwStyle, bool bMenu, int dwExStyle);

        #endregion
    }
}
