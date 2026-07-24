using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// 1 dòng cho MỖI CARTON đã được IN THÀNH CÔNG (không phải 1 dòng/serial nữa) — dùng để (1) chặn
// 1 serial bị in trùng vào 2 carton khác nhau (kiểm tra DB lúc quét), (2) tính đã in/còn lại của
// 1 Work Order để quyết định carton hiện tại là đủ hộp (PcsPerCarton) hay lẻ hộp (phần dư).
// Serial/ScanDate/CountSerial là 3 cột bắt buộc theo yêu cầu nghiệp vụ:
//   - Serial: TOÀN BỘ serial trên carton này, nối chuỗi bằng dấu phẩy (VD "RM15A...00,RM15A...01,...").
//   - CountSerial: số lượng serial trong chuỗi Serial ở trên — 10 nếu đủ hộp, hoặc phần dư
//     (VD 5) nếu là carton lẻ hộp cuối cùng của Work Order.
//   - ScanDate: thời điểm in carton này.
[Table("SM_Sakura_CartonLabel_Data")]
public class CartonSnScanLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(400)]
    public string Serial { get; set; } = "";

    [Required]
    public DateTime ScanDate { get; set; }

    public int CountSerial { get; set; }

    [Required]
    [StringLength(50)]
    public string WorkOrder { get; set; } = "";

    [Required]
    [StringLength(30)]
    public string CartonNumber { get; set; } = "";

    [StringLength(20)]
    public string? Color { get; set; }

    [StringLength(10)]
    public string? Condition { get; set; }

    // Pallet ID người vận hành tự đặt (vd "PALLET-001") — gán vào carton row này khi carton
    // được quét vào 1 pallet (xem SakuraService.ScanCartonIntoPalletAsync). Null = chưa thuộc
    // pallet nào.
    [StringLength(50)]
    public string? PalletId { get; set; }

    // Mã Pallet Number tự sinh "P-RM15A-XXXXX" — gán cho TẤT CẢ carton cùng PalletId ngay khi
    // bấm in tem Pallet lần đầu (xem SakuraService.BuildPalletLabelZplAsync). Null = pallet
    // (nếu có PalletId) chưa được "chốt"/in tem lần nào.
    [StringLength(30)]
    public string? PalletNumber { get; set; }

    // YYYYMMDD lấy từ ScanDate — chỉ để tiện truy vấn sau này, không có logic nghiệp vụ khác.
    public int? Date { get; set; }

    // CHỈ đánh dấu/audit trail — bấm nút Delete trong modal Manage Pallet set cột này = true
    // (song song với gỡ PalletId = null NHƯ CŨ). KHÔNG lọc theo cột này ở bất kỳ query nghiệp vụ
    // nào khác (số lượng đã in của Work Order, chặn trùng Carton Number/Serial, trang History).
    public bool IsDeleted { get; set; }
}
