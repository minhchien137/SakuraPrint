using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Entity: SVN_MiddleDimensionCheckResult — kết quả kiểm tra kích thước magnet,
// được nạp bởi service ngoài (magnet-log-collector, Node.js) đọc file Excel
// D:\MagnetLogfile_Summary\yyyyMMdd.xlsx/.xls và insert định kỳ. Bảng đã tồn
// tại sẵn trong SQL Server (không migrate từ đây) — 45 cột đo dưới đây map
// 1:1 theo thứ tự cột Excel gốc (header file nguồn là tiếng Trung, không map
// theo tên). Mỗi cột giữ nguyên văn chuỗi gốc, KHÔNG parse số, trừ DATE_TIME.
[Table("SVN_MiddleDimensionCheckResult")]
public class MiddleDimensionCheckResult
{
    [Key]
    public long Id { get; set; }

    public string? UNIT_SN { get; set; }
    public string? BARCODE_CONTENT1 { get; set; }
    public string? STATUS { get; set; }
    public DateTime DATE_TIME { get; set; }
    public string? UT { get; set; }

    public string? APolarity { get; set; }
    public string? ARESULT { get; set; }
    public string? BPolarity { get; set; }
    public string? BRESULT { get; set; }
    public string? CPolarity { get; set; }
    public string? CRESULT { get; set; }
    public string? DPolarity { get; set; }
    public string? DRESULT { get; set; }
    public string? EPolarity { get; set; }
    public string? ERESULT { get; set; }
    public string? FPolarity { get; set; }
    public string? FRESULT { get; set; }
    public string? GPolarity { get; set; }
    public string? GRESULT { get; set; }
    public string? HPolarity { get; set; }
    public string? HRESULT { get; set; }
    public string? IPolarity { get; set; }
    public string? IRESULT { get; set; }
    public string? JPolarity { get; set; }
    public string? JRESULT { get; set; }

    public string? Test_value_A { get; set; }
    public string? A_TEST_RESULT { get; set; }
    public string? Test_value_B { get; set; }
    public string? B_TEST_RESULT { get; set; }
    public string? Test_value_C { get; set; }
    public string? C_TEST_RESULT { get; set; }
    public string? Test_value_D { get; set; }
    public string? D_TEST_RESULT { get; set; }
    public string? Test_value_E { get; set; }
    public string? E_TEST_RESULT { get; set; }
    public string? Test_value_F { get; set; }
    public string? F_TEST_RESULT { get; set; }
    public string? Test_value_G { get; set; }
    public string? G_TEST_RESULT { get; set; }
    public string? Test_value_H { get; set; }
    public string? H_TEST_RESULT { get; set; }
    public string? Test_value_I { get; set; }
    public string? I_TEST_RESULT { get; set; }
    public string? Test_value_J { get; set; }
    public string? J_TEST_RESULT { get; set; }

    public string? source_file { get; set; }
    public DateTime created_at { get; set; }
}
