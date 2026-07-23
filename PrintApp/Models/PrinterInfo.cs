using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

[Table("SVN_Printer_Info_New")]
public class PrinterInfo
{
    [Key]
    public string ID_Printer { get; set; } = "";
    public string? Name_Printer { get; set; }
    public string? IP_Printer { get; set; }
    public string? Port_Printer { get; set; }
    public string? ZPL_Temp { get; set; }
    public string? target { get; set; }
    public string? width { get; set; }
    public string? height { get; set; }
    public string? Size { get; set; }
}