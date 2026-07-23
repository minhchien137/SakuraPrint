namespace PrintApp.Models;

// Payload gửi lên từ view Carton SN (PrintApp/Views/Sakura/CartonSN.cshtml) để build ZPL
// cho tem Carton Label (template key "CartonLabel" trong SM_Sakura_ZplTemplate).
public class CartonLabelPrintRequest
{
    public string CartonNumber { get; set; } = "";
    public string Color { get; set; } = "";
    public string Condition { get; set; } = "";
    public List<string> SerialNumbers { get; set; } = new();
}

// Trả về từ workorder-lookup — Color/TotalQuantity lấy từ Odoo (giống SnLabel), PrintedQuantity/
// RemainingQuantity tính từ SM_Sakura_CartonLabel_Data. ExpectedQuantity là số serial cần quét cho
// CARTON HIỆN TẠI: bằng CartonPcsPerCarton nếu còn đủ hộp, hoặc bằng đúng RemainingQuantity
// nếu đây là carton lẻ hộp cuối cùng (RemainingQuantity < CartonPcsPerCarton).
public class CartonWorkOrderLookupResponse
{
    public string WorkOrder { get; set; } = "";
    public string Color { get; set; } = "";
    public int TotalQuantity { get; set; }
    public int PrintedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public int ExpectedQuantity { get; set; }
    public int TotalCarton { get; set; }
    public int RemainingCarton { get; set; }
    public int? ProductId { get; set; }
}

// Trình duyệt gọi SAU KHI đã gửi ZPL thành công tới bridge cục bộ (in thật, không phải Preview)
// — lưu các serial không rỗng vào SM_Sakura_CartonLabel_Data. SerialNumbers PHẢI đúng vị trí SN1..SN10
// trên form (ô trống giữ ""), giống payload gửi cho /api/sakura/cartonsn/print.
public class CartonReportPrintResultRequest
{
    public string WorkOrder { get; set; } = "";
    public string CartonNumber { get; set; } = "";
    public string Color { get; set; } = "";
    public string Condition { get; set; } = "";
    public List<string> SerialNumbers { get; set; } = new();
    // Pallet ID hiện đang nhập trên UI (nếu có) — carton này được gom thẳng vào pallet đó ngay
    // lúc lưu kết quả in, không cần thao tác "Manage" thủ công. Xem RecordCartonScanAsync.
    public string? PalletId { get; set; }
}

// 1 dòng lịch sử = 1 carton đã in (khớp 1-1 với SM_Sakura_CartonLabel_Data) — dùng cho trang
// History của Carton SN Label.
public class CartonSnHistoryItemDto
{
    public int Id { get; set; }
    public string CartonNumber { get; set; } = "";
    public string WorkOrder { get; set; } = "";
    public string Color { get; set; } = "";
    public string Condition { get; set; } = "";
    public int CountSerial { get; set; }
    public string Serial { get; set; } = ""; // CSV toàn bộ serial của carton này
    public DateTime ScanDate { get; set; }
}

public class CartonSnHistoryPageDto
{
    public List<CartonSnHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
