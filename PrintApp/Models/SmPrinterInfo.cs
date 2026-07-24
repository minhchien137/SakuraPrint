using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Map bảng SM_Printer_Info (đã tạo sẵn trong DB, riêng biệt với SVN_Printer_Info_New) —
// chỉ phục vụ ExternalPrintController tra IP/Port máy in theo ID_Printer.
[Table("SM_Printer_Info")]
public class SmPrinterInfo
{
    [Key]
    public string ID_Printer { get; set; } = "";
    public string Name_Printer { get; set; } = "";
    public string IP_Printer { get; set; } = "";
    public string Port_Printer { get; set; } = "";
}

// ── Request DTO cho ExternalPrintController ──────────────────────────────────

public class ExternalPrintRequest
{
    public string Serial { get; set; } = "";
    public string PrinterId { get; set; } = "SAKURA_01";
    public int Copies { get; set; } = 1;
}
