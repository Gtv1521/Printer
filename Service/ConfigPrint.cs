using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MiPrinter.Service
{
    public class ConfigPrint
    {
        #region CONFIGURACIÓN NATIVA DE WINDOWS
        [StructLayout(LayoutKind.Sequential)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [SupportedOSPlatform("windows7.0")]
        private bool ImprimirEnWindows(string nombreImpresora, byte[] bytes)
        {
            IntPtr hPrinter = IntPtr.Zero;
            DOCINFOA di = new() { pDocName = "Ticket_POS_Windows", pDataType = "RAW" };
            int dwWritten = 0;
            bool exito = false;

            if (OpenPrinter(nombreImpresora.Normalize(), out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        IntPtr pBytes = Marshal.AllocHGlobal(bytes.Length);
                        Marshal.Copy(bytes, 0, pBytes, bytes.Length);
                        exito = WritePrinter(hPrinter, pBytes, bytes.Length, out dwWritten);
                        EndPagePrinter(hPrinter);
                        Marshal.FreeHGlobal(pBytes);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            return exito;
        }
        #endregion


        #region CONFIGURACIÓN NATIVA DE LINUX
        private bool ImprimirEnLinux(string rutaDispositivo, byte[] bytes)
            {
                try
                {
                    // En Linux la "impresora" es un archivo físico (Ej: /dev/usb/lp0)
                    // Simplemente abrimos el flujo del archivo y escribimos los bytes binarios
                    using (FileStream fs = new FileStream(rutaDispositivo, FileMode.Open, FileAccess.Write))
                    {
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Flush();
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error imprimiendo en Linux: {ex.Message}");
                    return false;
                }
            }
        #endregion

            // El método central e inteligente que llamará tu API
            public bool Imprimir(string identificadorDispositivo, byte[] bytes)
            {
                if (OperatingSystem.IsWindows())
                {
                    // En Windows, pasamos el nombre asignado (ej: "POS-58")
                    return ImprimirEnWindows(identificadorDispositivo, bytes);
                }
                else if (OperatingSystem.IsLinux())
                {
                    // En Linux, pasamos la ruta absoluta (ej: "/dev/usb/lp0")
                    return ImprimirEnLinux(identificadorDispositivo, bytes);
                }
                else
                {
                    throw new PlatformNotSupportedException("Sistema operativo no compatible para impresión directa.");
                }
            }
        }
}
