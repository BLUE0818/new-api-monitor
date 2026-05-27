using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CctqMonitor;

internal static class Dpapi
{
    private const int CryptProtectUiForbidden = 0x1;

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return "";
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var input = CreateBlob(bytes);
        var output = new DataBlob();
        try
        {
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var encrypted = new byte[output.cbData];
            Marshal.Copy(output.pbData, encrypted, 0, encrypted.Length);
            return Convert.ToBase64String(encrypted);
        }
        finally
        {
            FreeBlob(input);
            FreeBlob(output);
        }
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return "";
        }

        var bytes = Convert.FromBase64String(protectedText);
        var input = CreateBlob(bytes);
        var output = new DataBlob();
        var description = IntPtr.Zero;
        try
        {
            if (!CryptUnprotectData(ref input, ref description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var decrypted = new byte[output.cbData];
            Marshal.Copy(output.pbData, decrypted, 0, decrypted.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
        finally
        {
            FreeBlob(input);
            FreeBlob(output);
            if (description != IntPtr.Zero)
            {
                LocalFree(description);
            }
        }
    }

    private static DataBlob CreateBlob(byte[] bytes)
    {
        var blob = new DataBlob
        {
            cbData = bytes.Length,
            pbData = Marshal.AllocHGlobal(bytes.Length)
        };
        Marshal.Copy(bytes, 0, blob.pbData, bytes.Length);
        return blob;
    }

    private static void FreeBlob(DataBlob blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            LocalFree(blob.pbData);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        ref IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
