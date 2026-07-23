using Microsoft.AspNetCore.Mvc;
using PrintApp.Models;
using PrintApp.Services;

namespace PrintApp.Controllers;

public class PrintController : Controller
{
    private readonly ZplService _zplService;

    // ZplService được inject qua constructor (Dependency Injection)
    public PrintController(ZplService zplService)
    {
        _zplService = zplService;
    }

    // GET /Print/Index  (hoặc chỉ cần /)
    // Trả về trang HTML chính
    public IActionResult Index()
    {
        return View();
    }

    // POST /Print/GenerateZpl
    // Nhận nội dung, trả về chuỗi ZPL dạng JSON
    // Trình duyệt sẽ lấy ZPL này rồi tự gọi localhost:8021 để in
    [HttpPost]
    public IActionResult GenerateZpl([FromBody] PrintRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new ZplResult { Error = "Nội dung không được để trống." });
        }

        var zpl = _zplService.BuildZpl(
            request.Content,
            request.LabelWidth > 0 ? request.LabelWidth : 4,
            request.LabelHeight > 0 ? request.LabelHeight : 2
        );

        return Ok(new ZplResult { Zpl = zpl });
    }
}