using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using GiveMePath.Helper;
using SHDocVw;
namespace GiveMePath
{
    internal class Program:ExternDLLs
    {
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static System.Threading.Timer timer;
        private static Thread _SetPathThread;
        [STAThread]
        static void Main()
        {
           
            _hookID = SetHook(_proc);
            TimerCallback timerCallback = new TimerCallback(Starter);
            timer = new System.Threading.Timer(timerCallback, null, Timeout.Infinite, Timeout.Infinite);

            Application.Run();
            UnhookWindowsHookEx(_hookID);
            timer.Dispose();
        }
        private static void Starter(object state)
        {
            GetSelectedFilePath();
        }

        private static void EventFromDesktop()
        {
            SendKeys.SendWait("^c");
            IDataObject data = Clipboard.GetDataObject();
            string[] files = (string[])data.GetData(DataFormats.FileDrop);
            if (files.Length == 0)
                return;

            _SetPathThread = new Thread(SetClipboardPath);
            _SetPathThread.SetApartmentState(ApartmentState.STA);
            _SetPathThread.Start(files[0]);
            _SetPathThread.Join();
        }
        private static void GetSelectedFilePath()
        {
            
            const int nChars = 256;
            IntPtr handle = IntPtr.Zero;
            StringBuilder className = new StringBuilder(nChars);
            
            handle = GetForegroundWindow();
            GetClassName(handle, className, nChars);

            if(className.ToString()=="WorkerW")
            {
                Thread th=new Thread(EventFromDesktop);
                th.SetApartmentState(ApartmentState.STA);
                th.Start();
                th.Join();
            }
            else
            {
                foreach (InternetExplorer window in new SHDocVw.ShellWindows())
                {
                    string filename = Path.GetFileNameWithoutExtension(window.FullName)?.ToLower();
                    if (filename == "explorer")
                    {
                        Shell32.FolderItems items = ((Shell32.IShellFolderViewDual2)window.Document)?.SelectedItems();
                        if (items != null)
                        {
                            foreach (Shell32.FolderItem item in items)
                            {
                                _SetPathThread = new Thread(SetClipboardPath);
                                _SetPathThread.SetApartmentState(ApartmentState.STA);
                                _SetPathThread.Start(item.Path);
                                _SetPathThread.Join();
                            }
                        }
                    }
                }
            }
        }

        private static void SetClipboardPath(object path)
        {
            Clipboard.SetText(path.ToString());
        }
        
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if ((Control.ModifierKeys & Keys.Control) != 0 && (Keys)vkCode == Keys.P)
                {
                    timer.Change(1000, Timeout.Infinite);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        
    }
}
