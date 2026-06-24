using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiPrinter.Model
{
    public class Print
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string IP { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SerialMac { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}