namespace PrintApp.Models;

public class ToastLabelRequest
{
    public string? PrinterId { get; set; }
    public string? PrinterIp { get; set; }
    public int PrinterPort { get; set; } = 9100;
    public int Copies { get; set; } = 2;

    // Label info
    public string? PalletId { get; set; }
    public string? PoNumber { get; set; }
    public string? LotId { get; set; }
    public string? SkuNumber { get; set; }
    public int Quantity { get; set; } = 5;

    // Serials (up to 5 per box)
    public string? SerialNumber1 { get; set; }
    public string? SerialNumber2 { get; set; }
    public string? SerialNumber3 { get; set; }
    public string? SerialNumber4 { get; set; }
    public string? SerialNumber5 { get; set; }
}

public class ToastPalletRequest
{
    public string? PrinterIp { get; set; }
    public int PrinterPort { get; set; } = 9100;
    public string? PalletId { get; set; }
    public string? PoNumber { get; set; }
    public string? LotId { get; set; }
    public string? SkuNumber { get; set; }
    public string? Desc { get; set; }
    public string? DescFr { get; set; }
    public string? ModelNumber { get; set; }
}

public class AstroLabelDataDto
{
    public string? Date { get; set; }
    public string? PackageID { get; set; }
    public string? Serial { get; set; }
    public string? ScanDate { get; set; }
    public string? PalletID { get; set; }
    public string? EmployeeID { get; set; }
    public int CountSerial { get; set; }
}