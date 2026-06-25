using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using Microsoft.AspNetCore.Mvc.Filters;
using MiPrinter.Interface;
using MiPrinter.Model;
using SixLabors.ImageSharp.Formats.Png;
using SkiaSharp;
using SixLabors.ImageSharp.Processing;
using Zeroconf;
using System.Drawing.Drawing2D;
using Microsoft.OpenApi.Extensions;
using SixLabors.ImageSharp.Processing.Processors;
using System.Management;
using System.Runtime.Versioning;

namespace MiPrinter.Service
{
    public class Printer : IPrints<Print, DataPrint>
    {
        private const int ESCPOS_PORT = 9100;
        private readonly ConfigPrint _printer ;

        public Printer(ConfigPrint printer)
        {
            _printer = printer;
        }

        public Task<bool> DeletePrint(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Printing(DataPrint data)
        {
            try
            {
                var Impresora = await ObtenerImpresaraPorDefectoAsync();
                if (Impresora?.IP == null) throw new FileNotFoundException("Impresora por defecto no hay");

                var e = new EPSON();

                var Header = ByteSplicer.Combine(
                    e.CenterAlign(),
                    e.PrintLine(data.Comercio.Nombre),
                    e.PrintLine($"Nit: {data.Comercio.Nit}"),
                    e.PrintLine($"{data.Comercio.Direccion}"),
                    e.PrintLine(data.Comercio.Telefono),
                    e.PrintLine("")
                );

                var Cliente = ByteSplicer.Combine(
                    GenerarLineaDosColumnas("Ticket:", data.IdTicket, true, e),
                    GenerarLineaDosColumnas("Cliente:", data.Cliente.Name, true, e),
                    GenerarLineaDosColumnas("Fecha:", DateTime.Now.ToString(), true, e),
                    GenerarLineaDosColumnas("Telefono:", data.Cliente.Telefono, true, e),
                    GenerarLineaDosColumnas("Modelo:", data.Modelo, true, e),
                    e.CenterAlign(),
                    e.PrintLine("--------------------------------")
                );

                // se pasan todas las observaciones para imprimirlas 
                byte[] bytesDelBody = GenerarCuerpoObservaciones(data.Observaciones, e);
                byte[] advertencia = GenerarCuerpoObservaciones(data.Advertencia, e);

                var Body = ByteSplicer.Combine(
                    e.LeftAlign(),
                    e.SetStyles(PrintStyle.Bold),
                    e.PrintLine("Fallas reportadas:"),
                    e.SetStyles(PrintStyle.None),
                    bytesDelBody,
                    e.SetStyles(PrintStyle.Bold),
                    e.PrintLine("Advertencias:"),
                    e.SetStyles(PrintStyle.None),
                    advertencia,
                    e.CenterAlign(),
                    e.PrintLine("--------------------------------")
                );

                var faltante = decimal.Parse(data.Total) - decimal.Parse(data.Antisipo);

                var totals = ByteSplicer.Combine(
                    e.RightAlign(),
                    e.PrintLine($"Antisipo: ${decimal.Parse(data.Antisipo):#,##0}"),
                    e.PrintLine($"Total: ${decimal.Parse(data.Total):#,##0}"),
                    e.PrintLine($"Faltante: ${faltante:#,##0}")
                );

                var Footer = ByteSplicer.Combine(
                    e.CenterAlign(),
                    DibujarLineaSolida(),
                    e.PrintLine(""),
                    e.PrintQRCode(data.Qr),
                    e.PrintLine(""),
                    e.PrintLine(data.Qr),
                    e.PartialCut()
                );


                var impresion = ByteSplicer.Combine(
                    Header,
                    Cliente,
                    Body,
                    totals,
                    Footer
               );

                if (Impresora?.Type == "NETWORK")
                {
                    var printer = new ImmediateNetworkPrinter(
                            new ImmediateNetworkPrinterSettings
                            {
                                ConnectionString = $"{Impresora.IP}:9100",
                                PrinterName = Impresora.Name
                            });

                    await printer.WriteAsync(impresion);
                }
                else if (Impresora?.Type == "USB")
                {
                    if(OperatingSystem.IsLinux())
                    {
                        System.IO.File.WriteAllBytes(
                            Impresora.IP,
                            impresion
                        );
                    }
                    else if (OperatingSystem.IsWindows())
                    {
                        Console.WriteLine("imprimir data", impresion);
                        _printer.Imprimir(Impresora.Name, impresion);
                    }
                }

                return true;
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Error printing: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Print>> GetAllPrints()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            if (!File.Exists(_filePath))
            {
                return new List<Print>();
            }

            try
            {
                string json = await File.ReadAllTextAsync(_filePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<Print>();
                }

                var listaData = JsonSerializer.Deserialize<List<Print>>(json, options);

                return listaData ?? new List<Print>();

            }
            catch (JsonException)
            {
                return new List<Print>();
            }
        }

        public async Task<bool> SavePrint(Print data)
        {

            List<Print> impresoras = new List<Print>();

            // 1. Leer las impresoras actuales si el archivo ya existe
            if (File.Exists(_filePath))
            {
                try
                {
                    string jsonExistente = File.ReadAllText(_filePath);
                    impresoras = JsonSerializer.Deserialize<List<Print>>(jsonExistente)
                                 ?? new List<Print>();
                }
                catch (JsonException)
                {
                    // Si el archivo está corrupto o vacío, inicializamos una lista limpia
                    impresoras = new List<Print>();
                }
            }

            // 2. Aplicar la lógica del campo 'Default'
            if (data.IsDefault)
            {
                // Si la nueva es predeterminada, ponemos todas las existentes en false
                foreach (var imp in impresoras)
                {
                    imp.IsDefault = false;
                }
            }
            else if (!impresoras.Any())
            {
                // Opcional: Si es la primera impresora que se agrega en la historia, 
                // la forzamos a ser default para que el sistema nunca se quede sin una activa.
                data.IsDefault = true;
            }

            // 3. Agregar la nueva impresora a la lista
            impresoras.Add(data);

            // 4. Serializar con formato ordenado (indented) y guardar en el archivo
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            string jsonActualizado = JsonSerializer.Serialize(impresoras, opciones);

            File.WriteAllText(_filePath, jsonActualizado);

            return true;
        }

        public async Task<IEnumerable<Print>> ScanPrints()
        {
            var printers = new List<Print>();

            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine("Windows");
                printers.AddRange(GetUsbPrintersWindows()); 
            }
            else if (OperatingSystem.IsLinux())
            {
                printers.AddRange(GetUsbPrinters());
            }
            printers.AddRange(
                await GetNetworkPrinters("192.168.1"));

            return printers;
        }

        public async Task<IEnumerable<Print>> GetPrintsSave()
        {
            if (!System.IO.File.Exists(_filePath))
            {
                return new List<Print>();
            }

            string json = await System.IO.File.ReadAllTextAsync(
                _filePath
            );

            var printers = JsonSerializer.Deserialize<List<Print>>(json);

            return printers ?? new List<Print>();
        }

        private byte[] GenerarCuerpoObservaciones(IEnumerable<string> observaciones, EPSON e)
        {
            var comandosList = new List<byte[]>();

            // 1. Configuración inicial del bloque
            comandosList.Add(e.LeftAlign());

            // 2. Recorremos las observaciones dinámicamente
            foreach (var item in observaciones)
            {
                // Si tu objeto es complejo, cambia 'item.ToString()' por la propiedad de texto (ej: item.Descripcion)
                string textoLinea = item?.ToString() ?? string.Empty;
                comandosList.Add(e.PrintLine($"*{textoLinea}"));
            }

            // 3. Cierre del bloque
            comandosList.Add(e.PrintLine(""));

            // 4. Combinamos y retornamos el fragmento de bytes
            return ByteSplicer.Combine(comandosList.ToArray());
        }

        private List<Print> GetUsbPrinters()
        {
            var printers = new List<Print>();

            if (!Directory.Exists("/dev/usb"))
                return printers;

            foreach (var device in Directory.GetFiles("/dev/usb", "lp*"))
            {
                printers.Add(new Print
                {
                    Name = Path.GetFileName(device),
                    Type = "USB",
                    IP = device
                });
            }

            return printers;
        }

        [SupportedOSPlatform("windows")]
        private List<Print> GetUsbPrintersWindows()
        {
            // 1. Inicializamos la lista igual que en tu método anterior
            var printers = new List<Print>();

            // 2. Usamos 'using' para asegurar que Windows libere la memoria de la consulta WMI
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer"))
            {
                foreach (ManagementObject printer in searcher.Get())
                {
                    string name = printer["Name"]?.ToString()!;
                    string port = printer["PortName"]?.ToString()!;

                    // 3. Filtramos solo los puertos USB (USB001, USB002, etc.)
                    if (port != null && port.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
                    {
                        // 4. Mapeamos los datos exactamente a tu estructura de salida 'Print'
                        printers.Add(new Print
                        {
                            Name = name ?? "Impresora Térmica",
                            Type = "USB",
                            IP = port // En impresoras locales USB, el puerto hace las veces de identificador físico
                        });
                    }

                    // Liberamos el objeto individual del bucle
                    printer.Dispose();
                }
            }

            // 5. Retornamos la lista finalizada
            return printers;
        }

        /// <summary>
        /// Limpia el archivo de configuración de impresoras dejándolo como un arreglo vacío [].
        /// </summary>
        public async Task LimpiarArchivoPrintersAsync()
        {
            try
            {
                // 1. Aseguramos que la carpeta "config" exista antes de escribir, por seguridad
                string? directoryPath = Path.GetDirectoryName(_filePath);
                if (directoryPath != null && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 2. Sobrescribimos el archivo con la estructura de un array vacío
                await File.WriteAllTextAsync(_filePath, "[]");

                Console.WriteLine("El archivo printers.json ha sido limpiado correctamente.");
            }
            catch (IOException ex)
            {
                // Manejo de errores en caso de que el archivo esté siendo usado por otro proceso
                Console.WriteLine($"Error de E/S al limpiar el archivo: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inesperado al limpiar el archivo: {ex.Message}");
                throw;
            }
        }



        private async Task<Print?> BuscarPorIdAsync(string idBuscado)
        {
            // 1. Verificar si el archivo existe para evitar excepciones
            if (!File.Exists(_filePath)) return null;

            // 2. Leer todo el texto del archivo JSON
            string json = await File.ReadAllTextAsync(_filePath);

            // 3. Deserializar el contenido en una lista de tu modelo
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var listaData = JsonSerializer.Deserialize<List<Print>>(json, options);

            if (listaData == null) return null;

            // 4. Filtrar y buscar el primer objeto que coincida con el Id
            Print? resultado = listaData.FirstOrDefault(x => x.Id == idBuscado);
            return resultado;
        }

        public async Task<Print?> ObtenerImpresaraPorDefectoAsync()
        {
            if (!File.Exists(_filePath)) return null;

            string json = await File.ReadAllTextAsync(_filePath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var listaData = JsonSerializer.Deserialize<List<Print>>(json, options);

            if (listaData == null) return null;

            return listaData.FirstOrDefault(x => x.IsDefault);
        }

        private async Task<List<Print>> GetNetworkPrinters(string subnet)
        {
            var printers = new List<Print>();

            var tasks = Enumerable.Range(1, 254)
                .Select(ip => CheckPrinter($"{subnet}.{ip}", printers));

            await Task.WhenAll(tasks);

            return printers;
        }

        private async Task CheckPrinter(
            string ip,
            List<Print> printers)
        {
            try
            {
                using var client = new TcpClient();

                var connectTask = client.ConnectAsync(ip, ESCPOS_PORT);

                if (await Task.WhenAny(connectTask,
                        Task.Delay(500)) == connectTask)
                {
                    lock (printers)
                    {
                        printers.Add(new Print
                        {
                            Name = $"Printer-{ip}",
                            Type = "NETWORK",
                            IP = ip
                        });
                    }
                }
            }
            catch
            {
            }
        }

        private byte[] DibujarLineaSolida()
        {
            byte[] activar = new byte[] { 0x1B, 0x2D, 0x02 };
            byte[] apagar = new byte[] { 0x1B, 0x2D, 0x00 };

            // Un e.PrintLine de 32 espacios en blanco
            var e = new EPSON();
            byte[] textoEspacios = e.PrintLine(new string(' ', 32));

            return ByteSplicer.Combine(activar, textoEspacios, apagar);
        }

        private byte[] GenerarLineaDosColumnas(string etiqueta, string valor, bool enNegrita, EPSON e)
        {
            // El ancho estándar para impresoras de 58mm suele ser de 32 caracteres.
            // Si notas que se descuadra en tu impresora, puedes ajustarlo a 30 o 34.
            const int maxCaracteres = 32;

            string txtEtiqueta = etiqueta ?? string.Empty;
            string txtValor = valor ?? string.Empty;

            // Calcular cuántos espacios van en el medio para empujar el valor a la derecha
            int espacosNecesarios = maxCaracteres - txtEtiqueta.Length - txtValor.Length;

            // Control de seguridad por si el texto es demasiado largo y da negativo
            if (espacosNecesarios < 1) espacosNecesarios = 1;

            // Armamos la línea combinando todo con los espacios en blanco
            string lineaCompleta = txtEtiqueta + new string(' ', espacosNecesarios) + txtValor;

            // Retornamos los bytes con la configuración de estilo correspondiente
            return ByteSplicer.Combine(
                e.LeftAlign(), // Siempre alineado a la izquierda
                e.SetStyles(enNegrita ? PrintStyle.Bold : PrintStyle.None),
                e.PrintLine(lineaCompleta),
                e.SetStyles(PrintStyle.None) // Limpiamos el estilo para que no afecte lo que venga abajo
            );
        }

        private readonly string _filePath = Path.Combine(
            AppContext.BaseDirectory,
            "config",
            "printers.json"
        );
    }
}