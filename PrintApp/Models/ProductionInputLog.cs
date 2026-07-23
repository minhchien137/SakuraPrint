using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Entity: SVN_ProductionInputLogs — chỉ map các cột cần dùng (tra cứu
// master_wo_code theo serial_code), bảng đã tồn tại sẵn trong SQL Server.
[Table("SVN_ProductionInputLogs")]
public class ProductionInputLog
{
    [Key]
    public long Id { get; set; }

    [Column("serial_code")]
    public string? SerialCode { get; set; }

    [Column("master_wo_code")]
    public string? MasterWoCode { get; set; }

    // Dạng "{master_wo_code}-{running:000}" — số cuối cùng sau dấu '-' là số thứ tự
    // (= số lượng) đã nhập kết quả sản xuất cho WO đó, xem ParseWoSuffix ở MiddleController.
    [Column("wo_code")]
    public string? WoCode { get; set; }

    // Ngày nhập kết quả sản xuất hoàn tất — dùng để lọc Entered Qty (Summary) theo cùng
    // khoảng ngày với bộ lọc trên bảng Middle, thay vì luôn tính trên toàn bộ lịch sử WO.
    [Column("date_finished")]
    public DateTime? DateFinished { get; set; }
}
