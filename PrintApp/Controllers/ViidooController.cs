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

    // Tra stock.picking theo code (VD "WH/INT/00107") -> trả về các dòng stock.move.line
    // thuộc picking đó: lot, số lượng, sản phẩm, kho nguồn/đích.
    [HttpGet("picking-move-lines")]
    public async Task<IActionResult> PickingMoveLines([FromQuery] string pickingCode)
    {
        if (string.IsNullOrWhiteSpace(pickingCode))
            return BadRequest(new { message = "pickingCode parameter is required" });

        try
        {
            var lines = await _viidoo.GetPickingMoveLinesAsync(pickingCode);
            if (lines.Count == 0)
                return NotFound(new { message = "Picking not found or has no move lines" });

            return Ok(lines.Select(l => new
            {
                id = l.Id,
                lotName = l.LotName,
                qtyDone = l.QtyDone,
                productName = l.ProductName,
                pickingName = l.PickingName,
                locationName = l.LocationName,
                locationDestName = l.LocationDestName,
            }));
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
