using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services
{
    public class SwConnector
    {
        private readonly ISldWorks _swApp;

        public SwConnector(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public bool IsConnected => _swApp != null;

        public object GetSwApp() => _swApp;

        public bool CreateNewPart()
        {
            try
            {
                var part = _swApp.NewPart();
                return part != null;
            }
            catch { return false; }
        }

        public string? GetActiveDocName()
        {
            try
            {
                var doc = _swApp.ActiveDoc as IModelDoc2;
                if (doc == null) return null;
                // IModelDoc2 does not expose a Name property in the strongly-typed
                // interop.  Use GetPathName() and extract the file name.
                var path = doc.GetPathName();
                return string.IsNullOrEmpty(path) ? null : System.IO.Path.GetFileName(path);
            }
            catch { return null; }
        }
    }
}
