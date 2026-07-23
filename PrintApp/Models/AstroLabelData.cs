using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

[Table("SVN_Astro_Label_Data")]
public class AstroLabelData
{
    [Key]
    public int Id { get; set; }
    public string? Date { get; set; }
    public string? PackageID { get; set; }
    public string? Serial { get; set; }
    public string? ScanDate { get; set; }
    public string? PalletID { get; set; }
    public bool isDeleted { get; set; } = false;
    public string? EmployeeID { get; set; }
    public int CountSerial { get; set; }
}