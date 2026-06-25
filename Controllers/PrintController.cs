using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MiPrinter.Interface;
using MiPrinter.Model;
using IOFile = System.IO.File;

namespace MiPrinter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrintController : ControllerBase
    {
        private readonly IPrints<Print, DataPrint> _print;
        private readonly ILogger<PrintController> _logger;

        public PrintController(IPrints<Print, DataPrint> print, ILogger<PrintController> logger)
        {
            _print = print;
            _logger = logger;
        }

        [HttpGet("/scanner")]
        public async Task<IActionResult> Scaneo()
        {
            var response = await _print.ScanPrints();
            if (response.Count() == 0) return NotFound("No se encontraron impresoras");
            return Ok(response);
        }

        [HttpGet("/prints_saved")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var response = await _print.GetAllPrints();
                return Ok(response);
            }
            catch (System.Exception ex)
            {
                return Problem(ex.Message);
            }
        }

        [HttpPost("/save_print")]
        public async Task<IActionResult> GuardarImpresora([FromQuery] Print data)
        {
            try
            {
                var response = await _print.SavePrint(data);
                return Ok(response);
            }
            catch (System.Exception ex)
            {
                return Problem(ex.Message);
            }
        }

        [HttpPost("/printer")]
        // [Consumes("application/json", "multipart/form-data")]
        public async Task<IActionResult> Imprimir([FromBody] DataPrint data)
        {
            try
            {
                var response = await _print.Printing(data);
                return Ok(response);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound($"algo fallo: {ex.Message}");
            }
            catch (System.Exception ex)
            {
                return Problem(ex.Message);
            }
        }

        [HttpGet("/clean_saved")]
        public async Task<IActionResult> Clean()
        {
            try
            {
                await _print.LimpiarArchivoPrintersAsync();
                return Ok(true);
            }
            catch (Exception ex) 
            {
                return Problem(ex.Message);
            }

        }
    }
}