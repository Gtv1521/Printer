using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ESCPOS_NET.Utils;

namespace MiPrinter.Model
{
    public class DataPrint
    {
        public DataCliente Cliente { get; set; } = new DataCliente();
        public Comercio Comercio { get; set; } = new Comercio();

        [Required]
        public string IdTicket { get; set; } = string.Empty;
        [Required]
        public string[] Observaciones { get; set; } = [];
        [Required]
        public string[] Advertencia { get; set; } = [];
        [Required]
        public string Modelo { get; set; } = string.Empty;
        [Required]
        public string Antisipo { get; set; } = string.Empty;
        [Required]
        public string Total { get; set; } = string.Empty;
        [Required]
        public string Qr { get; set; } = string.Empty;
    }


    public class Comercio
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;
        [Required]
        public string Nit { get; set; } = string.Empty;
        [Required]
        public string Telefono { get; set; } = string.Empty;
        [Required]
        public string Direccion { get; set; } = string.Empty;
    }

    public class DataCliente
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Telefono { get; set; } = string.Empty;
    }
}