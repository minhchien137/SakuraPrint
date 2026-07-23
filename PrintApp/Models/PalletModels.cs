namespace PrintApp.Models;

// Payload/DTO cho vùng "Print Pallet" trên view Carton SN (PrintApp/Views/Sakura/CartonSN.cshtml)
// — gom nhiều carton đã in (SM_Sakura_CartonLabel_Data) vào 1 Pallet ID, đếm số thùng/unit
// realtime, sinh Pallet Number tự động lúc "chốt"/in tem (xem SakuraService.BuildPalletLabelZplAsync).

public class PalletScanBoxRequest
{
    public string PalletId { get; set; } = "";
    public string CartonNumber { get; set; } = "";
    public string Color { get; set; } = "";
}

public class PalletUnscanBoxRequest
{
    public string PalletId { get; set; } = "";
    public string CartonNumber { get; set; } = "";
}

// 1 dòng trong bảng "Quản lý Pallet" — cột hiển thị: ID | Serial (= CartonNumber) | Pallet ID | Xóa.
public class PalletBoxDto
{
    public int Id { get; set; }
    public string CartonNumber { get; set; } = "";
    public string? Color { get; set; }
    public int CountSerial { get; set; }
    public string? PalletId { get; set; }
}

public class PalletBoxesResponse
{
    public string PalletId { get; set; } = "";
    public int BoxCount { get; set; }
    public int UnitCount { get; set; }
    public List<PalletBoxDto> Boxes { get; set; } = new();
}

public class PalletPrintRequest
{
    public string PalletId { get; set; } = "";
    public string PoNumber { get; set; } = "";
    public string InboundReference { get; set; } = "";
    public string WarehouseReference { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
}
