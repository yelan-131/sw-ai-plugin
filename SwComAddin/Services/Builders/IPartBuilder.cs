using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public interface IPartBuilder
    {
        (bool success, string message) Build(Dictionary<string, object> parameters, ISldWorks swApp);
    }
}
