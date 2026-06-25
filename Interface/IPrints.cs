using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiPrinter.Interface
{
    public interface IPrints<T, R>
    {
        Task<IEnumerable<T>> ScanPrints();
        Task<bool> Printing(R data);
        Task<bool> DeletePrint(int id);
        Task<List<T>> GetAllPrints();
        Task<IEnumerable<T>> GetPrintsSave();
        Task<bool> SavePrint(T data);
        Task LimpiarArchivoPrintersAsync();
        Task<bool> PrintAny(byte[] data);
    }
}