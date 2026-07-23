using Microsoft.AspNetCore.Mvc;
using PrintApp.Services;

namespace PrintApp.Controllers;

// Endpoint độc lập để test tra cứu Odoo (mrp.production) thủ công.
// Logic thật nằm ở ViidooService — cũng được SakuraController dùng cho
// chế độ "In qua Work Order" ở Sakura/SnLabel.
[ApiController]
[Route("api/[controller]")]
public class ViidooController : ControllerBase
{
    private readonly ViidooService _viidoo;

    public ViidooController(ViidooService viidoo)
    {
        _viidoo = viidoo;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string productionCode)
    {
        if (string.IsNullOrWhiteSpace(productionCode))
            return BadRequest(new { message = "productionCode parameter is required" });

        try
        {
            var result = await _viidoo.SearchAsync(productionCode);
            if (result == null)
                return NotFound(new { message = "Product code not found" });

            return Ok(new { productCode = result.ProductCode, color = result.Color, quantity = result.Quantity, productId = result.ProductId });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new { message = "Error calling Odoo API", details = ex.Message });
        }
        catch (TaskCanceledException ex)
        {
            return StatusCode(408, new { message = "Request Timeout", details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "General Error", details = ex.Message });
        }
    }

    // Debug: xem nguyên record thô Odoo trả về cho productionCode, để đối chiếu trực
    // tiếp khi 1 field (vd. product_id) trả ra không khớp với những gì thấy trên Odoo UI.
    [HttpGet("raw")]
    public async Task<IActionResult> Raw([FromQuery] string productionCode)
    {
        if (string.IsNullOrWhiteSpace(productionCode))
            return BadRequest(new { message = "productionCode parameter is required" });

        try
        {
            var record = await _viidoo.GetRawRecordAsync(productionCode);
            if (record == null)
                return NotFound(new { message = "Product code not found" });

            return Content(record.Value.GetRawText(), "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
