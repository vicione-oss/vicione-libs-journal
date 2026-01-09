using System.Runtime.InteropServices;

namespace ViciOne.Journal;

[StructLayout(LayoutKind.Sequential)]
#pragma warning disable CA1815 // Equals und Gleichheitsoperator für Werttypen außer Kraft setzen
#pragma warning disable IDE1006 // Benennungsstile
internal struct IOVector
{
    public IntPtr iov_base;
    public UIntPtr iov_len;
}
#pragma warning restore IDE1006 // Benennungsstile
#pragma warning restore CA1815 // Equals und Gleichheitsoperator für Werttypen außer Kraft setzen
