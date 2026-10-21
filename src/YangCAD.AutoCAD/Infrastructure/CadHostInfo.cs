using System;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace YangCAD.AutoCADHost.Infrastructure
{
    public static class CadHostInfo
    {
        private static string? _productOverride;

        public static void SetProductOverride(string? product)
        {
            _productOverride = string.IsNullOrWhiteSpace(product) ? null : product!.Trim();
        }

        public static string Product
        {
            get
            {
                var overridden = _productOverride;
                if (!string.IsNullOrWhiteSpace(overridden)) return overridden!;
                try
                {
                    var value = Convert.ToString(Application.GetSystemVariable("PRODUCT"));
                    return string.IsNullOrWhiteSpace(value) ? "AutoCAD" : value!;
                }
                catch { return "AutoCAD"; }
            }
        }

        public static string Version
        {
            get
            {
                try
                {
                    var value = Convert.ToString(Application.GetSystemVariable("ACADVER"));
                    return value ?? "";
                }
                catch { return ""; }
            }
        }
    }
}
