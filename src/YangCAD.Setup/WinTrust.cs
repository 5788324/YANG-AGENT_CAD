using System;
using System.Runtime.InteropServices;

namespace YangCAD.Setup
{
    internal static class WinTrust
    {
        private static readonly Guid WintrustActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionIgnore = 0;
        private const uint WtdProvFlags = 0x00000010; // WTD_REVOCATION_CHECK_NONE

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WintrustFileInfo
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WintrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true, PreserveSig = true)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, IntPtr pWVTData);

        public static bool IsTrusted(string filePath, out int resultCode)
        {
            var fileInfo = new WintrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WintrustFileInfo)),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            IntPtr filePtr = IntPtr.Zero;
            IntPtr dataPtr = IntPtr.Zero;
            try
            {
                filePtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WintrustFileInfo)));
                Marshal.StructureToPtr(fileInfo, filePtr, false);
                var data = new WintrustData
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WintrustData)),
                    pPolicyCallbackData = IntPtr.Zero,
                    pSIPClientData = IntPtr.Zero,
                    dwUIChoice = WtdUiNone,
                    fdwRevocationChecks = WtdRevokeNone,
                    dwUnionChoice = WtdChoiceFile,
                    pFile = filePtr,
                    dwStateAction = WtdStateActionIgnore,
                    hWVTStateData = IntPtr.Zero,
                    pwszURLReference = null,
                    dwProvFlags = WtdProvFlags,
                    dwUIContext = 0
                };
                dataPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WintrustData)));
                Marshal.StructureToPtr(data, dataPtr, false);
                var action = WintrustActionGenericVerifyV2;
                resultCode = WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);
                return resultCode == 0;
            }
            finally
            {
                if (dataPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(dataPtr);
                if (filePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(filePtr);
            }
        }
    }
}
