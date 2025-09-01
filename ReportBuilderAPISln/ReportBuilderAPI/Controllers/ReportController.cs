using DinkToPdf;
using DinkToPdf.Contracts;
using IronPdf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ReportBuilderAPI.Models;
using ReportBuilderAPI.Services;
using System.Xml.Linq;

namespace ReportBuilderAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ReportService _service;
        private readonly IConverter _converter;

        public ReportController(ReportService service, IConverter converter)
        {
            _service = service;
            _converter = converter;
        }

        [HttpGet("templates")]
        public IActionResult GetTemplates()
        {
            var templates = _service.GetReportTemplates();
            return Ok(templates);
        }

        [HttpGet("{templateId}/metadata")]
        public IActionResult GetTemplateMetadata(int templateId)
        {
            var metadata = _service.GetTemplateMetadata(templateId);
            return Ok(metadata);
        }


        [HttpGet("templates/{id}/full")]
        public IActionResult GetFullTemplate(int id)
        {
            var template = _service.GetFullTemplate(id);
            if (template == null)
                return NotFound();
            return Ok(template);
        }

        [HttpPut("templates/{id}")]
        public async Task<IActionResult> UpdateTemplate(int id, [FromBody] SaveTemplateDto dto)
        {
            var result = await _service.UpdateTemplateAsync(id, dto);
            if (result)
                return Ok("Updated successfully");
            return StatusCode(500, "An error occurred while updating the report template.");
        }

        [HttpPost("generate")]
        public IActionResult GenerateReport([FromBody] ReportRequestDto request)
        {
            var result = _service.GenerateReport(request);
            return Ok(result);
        }

        //[HttpPost("export/excel")]
        //public IActionResult ExportToExcel([FromBody] ReportRequestDto request)
        //{
        //    var stream = _service.ExportToExcel(request);
        //    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "report.xlsx");
        //}


        [HttpPost("export/excel")]
        public IActionResult ExportToExcel([FromBody] ReportRequestDto request)
        {
            // Get visible columns from the request if available
            List<string> visibleColumns = null;
            if (request.GetType().GetProperty("VisibleColumns") != null)
            {
                var visibleColumnsProperty = request.GetType().GetProperty("VisibleColumns");
                visibleColumns = visibleColumnsProperty?.GetValue(request) as List<string>;
            }

            var stream = _service.ExportToExcel(request, visibleColumns);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "report.xlsx");
        }


        //[HttpPost("export/pdf")]
        //public IActionResult ExportToPdf([FromBody] ReportRequestDto request)
        //{
        //    var htmlContent = _service.GenerateHtmlReport(request); // returns your HTML report

        //    var renderer = new ChromePdfRenderer();
        //    var pdf = renderer.RenderHtmlAsPdf(htmlContent);

        //    return File(pdf.BinaryData, "application/pdf", "report.pdf");
        //}

        [HttpPost("export/pdf")]
        public IActionResult ExportToPdf([FromBody] ReportRequestDto request)
        {
            // Get visible columns from the request if available
            List<string> visibleColumns = null;
            if (request.GetType().GetProperty("VisibleColumns") != null)
            {
                var visibleColumnsProperty = request.GetType().GetProperty("VisibleColumns");
                visibleColumns = visibleColumnsProperty?.GetValue(request) as List<string>;
            }

            var htmlContent = _service.GenerateHtmlReport(request, visibleColumns); // returns your HTML report
            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf(htmlContent);

            return File(pdf.BinaryData, "application/pdf", "report.pdf");
        }


        //public IActionResult ExportToPdf([FromBody] ReportRequestDto request)
        //{
        //    var htmlContent = _service.GenerateHtmlReport(request); // returns string
        //    var doc = new HtmlToPdfDocument()
        //    {
        //        GlobalSettings = new GlobalSettings
        //        {
        //            PaperSize = PaperKind.A4,
        //            Orientation = Orientation.Portrait
        //        },
        //        Objects = {
        //    new ObjectSettings { HtmlContent = htmlContent }
        //}
        //    };
        //    var pdfBytes = _converter.Convert(doc);
        //    return File(pdfBytes, "application/pdf", "report.pdf");
        //}

        [HttpPost("import/tally")]
        public async Task<IActionResult> ImportTallyData(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("File missing.");
            using var stream = new StreamReader(file.OpenReadStream());
            var ext = Path.GetExtension(file.FileName);
            if (ext == ".xml")
            {
                var xml = await stream.ReadToEndAsync();
                var doc = XDocument.Parse(xml);
                // Parse your XML structure here and insert to Sales table
            }
            else if (ext == ".xlsx")
            {
                using var package = new ExcelPackage(file.OpenReadStream());
                var sheet = package.Workbook.Worksheets[0];
                for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                {
                    var invoice = sheet.Cells[row, 1].Text;
                    var date = DateTime.Parse(sheet.Cells[row, 2].Text);
                    var store = int.Parse(sheet.Cells[row, 3].Text);
                    var product = int.Parse(sheet.Cells[row, 4].Text);
                    var qty = int.Parse(sheet.Cells[row, 5].Text);
                    var price = decimal.Parse(sheet.Cells[row, 6].Text);
                    // Insert to Sales table
                }
            }
            return Ok("Imported successfully");
        }

        [HttpPost("templates/save")]
        public async Task<IActionResult> SaveTemplate([FromBody] SaveTemplateDto dto)
        {
            var result = await _service.SaveTemplateAsync(dto);
            if (result)
                return Ok("Saved successfully");
            else
                return StatusCode(500, "An error occurred while saving the report template.");
        }


        [HttpPost("filters/options")]
        public async Task<IActionResult> GetFilterOptions([FromBody] DropdownOptionRequestDto dto)
        {
            var result = await _service.GetFilterOptions(dto);
            return Ok(result);
        }

        [HttpPost("analyze-query")]
        public IActionResult AnalyzeSqlQuery([FromBody] SqlQueryAnalysisRequestDto request)
        {
            var result = _service.AnalyzeSqlQuery(request);
            if (!result.IsValid)
                return BadRequest(result.ErrorMessage);

            return Ok(result.Fields);
        }

        [HttpGet("{templateId}/group-metadata")]
        public IActionResult GetGroupMetadata(int templateId)
        {
            var metadata = _service.GetTemplateMetadata(templateId);

            // Return fields that are suitable for grouping
            var groupableFields = metadata.Fields.Where(f =>
                !f.Contains(" ") && !f.Contains("(") && !f.Contains("\"") && !f.Contains("'")
            ).ToList();

            return Ok(new
            {
                GroupableFields = groupableFields,
                NumericFields = metadata.Fields.Where(f =>
                    f.ToLower().Contains("amount") ||
                    f.ToLower().Contains("price") ||
                    f.ToLower().Contains("quantity") ||
                    f.ToLower().Contains("total") ||
                    f.ToLower().Contains("count")
                ).ToList()
            });
        }





    }
}
