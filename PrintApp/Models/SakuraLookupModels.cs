namespace PrintApp.Models;

// ── "Universal Lookup" (/sakura/lookup) — quét 1 mã bất kỳ (Carton/Pallet/Serial, sẽ có thêm
// loại khác sau), tự nhận diện loại mã rồi trả về toàn bộ thông tin liên quan. Xem
// PrintApp/Services/SakuraLookupHandlers.cs.

// Mỗi loại mã implement interface này — CanHandle() chỉ là gợi ý ưu tiên thử trước theo format
// (VD Pallet Number luôn bắt đầu "P-", Serial luôn bắt đầu SakuraService.Model = "RM15A"), KHÔNG
// phải điều kiện chặn tuyệt đối — SakuraLookupService vẫn thử các handler còn lại nếu handler ưu
// tiên không tìm thấy dữ liệu thật trong DB, để không bỏ sót khi format thực tế không như dự đoán.
public interface ISakuraLookupHandler
{
    string TypeKey { get; }
    bool CanHandle(string code);
    Task<SakuraLookupResponse?> ResolveAsync(string code);
}

// Envelope chung cho MỌI loại mã — 1 khung UI duy nhất ở Lookup.cshtml render được cho cả hiện
// tại lẫn các loại mã thêm sau này, không cần sửa frontend mỗi lần thêm handler mới.
public class SakuraLookupResponse
{
    public string TypeKey { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public string ScannedCode { get; set; } = "";

    // Thông tin chính (key-value) hiện ở đầu trang.
    public List<SakuraLookupField> Fields { get; set; } = new();

    // Danh sách con (VD toàn bộ carton trong 1 pallet, toàn bộ serial trong 1 carton, lịch sử
    // rework...) — mỗi section 1 bảng riêng.
    public List<SakuraLookupSection> Sections { get; set; } = new();
}

public class SakuraLookupField
{
    public string Label { get; set; } = "";
    public string? Value { get; set; }

    public SakuraLookupField() { }
    public SakuraLookupField(string label, string? value)
    {
        Label = label;
        Value = value;
    }
}

public class SakuraLookupSection
{
    public string Title { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public List<List<string?>> Rows { get; set; } = new();
}

// ── Raw row shape khớp cột trả về của sp_Sakura_Trace_Carton / sp_Sakura_Trace_Pallet ───────────
public class SakuraCartonTraceRow
{
    public int Id { get; set; }
    public string CartonNumber { get; set; } = "";
    public string WorkOrder { get; set; } = "";
    public string? Color { get; set; }
    public string? Condition { get; set; }
    public int CountSerial { get; set; }
    public string Serial { get; set; } = "";
    public DateTime ScanDate { get; set; }
    public string? PalletId { get; set; }
    public string? PalletNumber { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsReprint { get; set; }
    public DateTime? LastReprintAt { get; set; }
    public int ReprintCount { get; set; }
    public bool IsPalletReprint { get; set; }
    public DateTime? LastPalletReprintAt { get; set; }
    public int PalletReprintCount { get; set; }
    public string? PoNumber { get; set; }
    public string? InboundReference { get; set; }
    public string? WarehouseReference { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? ReworkWorkOrder { get; set; }
    public DateTime? LastRepackedAt { get; set; }
    public int RepackCount { get; set; }
}

// ── Raw row shape khớp cột trả về của sp_Sakura_Trace_Serial ─────────────────────────────────
public class SakuraSerialTraceRow
{
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public string? Variant { get; set; }
    public string? PrintColor { get; set; }
    public string? ProductionLine { get; set; }
    public DateTime? ProductionDate { get; set; }
    public string? RunningNumber { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string? PrintedBy { get; set; }
    public Guid? BatchId { get; set; }
    public string? PrintWorkOrder { get; set; }
    public int? ReprintCount { get; set; }
    public DateTime? LastReprintedAt { get; set; }
    public string? LastReprintedBy { get; set; }
    public string? Ean { get; set; }
    public string? Status { get; set; }
    public int? FailedStep { get; set; }
    public string? ReworkWorkOrder { get; set; }
    public int? ReworkReprintCount { get; set; }
    public DateTime? LastReworkReprintAt { get; set; }

    public string? CartonNumber { get; set; }
    public string? CartonWorkOrder { get; set; }
    public string? CartonColor { get; set; }
    public string? CartonCondition { get; set; }
    public string? PalletId { get; set; }
    public string? PalletNumber { get; set; }
    public DateTime? CartonScanDate { get; set; }
    public bool? CartonIsDeleted { get; set; }
}
