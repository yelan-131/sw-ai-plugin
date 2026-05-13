using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SwComAddin
{
    // Resolve assemblies from our DLL directory when loaded by SolidWorks
    internal static class AssemblyResolver
    {
        private static readonly string DllDir = Path.GetDirectoryName(
            typeof(AssemblyResolver).Assembly.Location);

        public static void Install()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name).Name + ".dll";
                var path = Path.Combine(DllDir, name);
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
                return null;
            };
        }
    }

    // SolidWorks ISwAddin COM interface
    // Must inherit from IUnknown (NOT IDispatch) for correct vtable layout:
    //   Slot 0: QueryInterface
    //   Slot 1: AddRef
    //   Slot 2: Release
    //   Slot 3: ConnectToSW
    //   Slot 4: DisconnectFromSW
    [ComImport]
    [Guid("DA306A0D-EAC5-4406-8610-B1DA805D9270")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface ISwAddin
    {
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool ConnectToSW(
            [In, MarshalAs(UnmanagedType.IDispatch)] object ThisSW,
            [In] int Cookie);

        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool DisconnectFromSW();
    }

    [ComVisible(true)]
    [Guid("B3E7D8A1-4F2C-4A91-B5D6-E8F0A1C2D3E4")]
    [ProgId("SwAiPlugin.Addin")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class SwAddin : ISwAddin
    {
        private ISldWorks? _swApp;
        private int _cookie;
        private TaskpaneView? _taskPaneView;
        private SwTaskPaneControl? _taskPaneControl;

        private static readonly string LogPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
            "SwAddin.log");

        private static void Log(string msg)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); }
            catch { }
        }

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            AssemblyResolver.Install();
            Log("=== ConnectToSW called (managed) ===");
            try
            {
                _swApp = (ISldWorks)ThisSW;
                _cookie = Cookie;
                Log("Got ISldWorks, calling SetAddinCallbackInfo2...");
                _swApp.SetAddinCallbackInfo2(0, this, Cookie);
                Log("SetAddinCallbackInfo2 OK");
                CreateTaskPane();
                Log("ConnectToSW SUCCESS");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ConnectToSW FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            Log("DisconnectFromSW called");
            try
            {
                if (_taskPaneView != null)
                {
                    _taskPaneView.DeleteView();
                    Marshal.ReleaseComObject(_taskPaneView);
                    _taskPaneView = null;
                }

                if (_taskPaneControl != null)
                {
                    _taskPaneControl.Dispose();
                    _taskPaneControl = null;
                }

                if (_swApp != null)
                {
                    Marshal.ReleaseComObject(_swApp);
                    _swApp = null;
                }

                Log("DisconnectFromSW SUCCESS");
                return true;
            }
            catch (Exception ex)
            {
                Log($"DisconnectFromSW FAILED: {ex.Message}");
                return false;
            }
        }

        private void CreateTaskPane()
        {
            if (_swApp == null) return;

            Log("Creating SwTaskPaneControl...");
            _taskPaneControl = new SwTaskPaneControl(_swApp);
            Log($"Control created, Handle={_taskPaneControl.Handle}");

            Log("Calling CreateTaskpaneView2...");
            _taskPaneView = _swApp.CreateTaskpaneView2(null, "SW AI Plugin");

            if (_taskPaneView != null)
            {
                Log("Calling DisplayWindowFromHandle...");
                _taskPaneView.DisplayWindowFromHandle(_taskPaneControl.Handle.ToInt32());
                Log("TaskPane displayed OK");
            }
            else
            {
                Log("ERROR: CreateTaskpaneView2 returned null");
            }
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            string keyPath = $@"SOFTWARE\SolidWorks\Addins\{t.GUID:B}";
            using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
            {
                key?.SetValue(null, 1);
                key?.SetValue("Title", "SolidWorks AI Plugin");
                key?.SetValue("Description", "AI-powered SolidWorks assistant with parametric modeling");
            }

            string userKeyPath = $@"SOFTWARE\SolidWorks\AddInsStartup\{t.GUID:B}";
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(userKeyPath))
            {
                key?.SetValue(null, 1);
            }

            Log($"Register: {t.GUID:B}");
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            string keyPath = $@"SOFTWARE\SolidWorks\Addins\{t.GUID:B}";
            try { Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(keyPath); } catch { }

            string userKeyPath = $@"SOFTWARE\SolidWorks\AddInsStartup\{t.GUID:B}";
            try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(userKeyPath); } catch { }

            Log($"Unregister: {t.GUID:B}");
        }
    }
}
