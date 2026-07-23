namespace PrintApp.Models;

// ── DTOs cho view Result (Middle) — hiển thị SVN_MiddleDimensionCheckResult ──

public class MiddleDimensionCheckResultDto
{
    public long Id { get; set; }
    public string UnitSn { get; set; } = "";

    // Không lấy từ SVN_MiddleDimensionCheckResult — được tra cứu sau khi map
    // (SVN_ProductionInputLogs.master_wo_code theo serial_code == UnitSn), xem MiddleController.
    public string? WorkOrder { get; set; }
    public string Status { get; set; } = "";
    public DateTime DateTime { get; set; }
    public string? Ut { get; set; }
    public string? SourceFile { get; set; }

    // 10 kênh đo (A..J) gộp lại — dùng để hiển thị gọn trong hàng mở rộng
    // thay vì 40 cột rời rạc trên grid chính.
    public List<MiddleChannelResultDto> Channels { get; set; } = new();

    public static MiddleDimensionCheckResultDto FromEntity(MiddleDimensionCheckResult e) => new()
    {
        Id = e.Id,
        UnitSn = e.UNIT_SN ?? "",
        Status = e.STATUS ?? "",
        DateTime = e.DATE_TIME,
        Ut = e.UT,
        SourceFile = e.source_file,
        Channels = new List<MiddleChannelResultDto>
        {
            new() { Channel = "A", Polarity = e.APolarity, Result = e.ARESULT, TestValue = e.Test_value_A, TestResult = e.A_TEST_RESULT },
            new() { Channel = "B", Polarity = e.BPolarity, Result = e.BRESULT, TestValue = e.Test_value_B, TestResult = e.B_TEST_RESULT },
            new() { Channel = "C", Polarity = e.CPolarity, Result = e.CRESULT, TestValue = e.Test_value_C, TestResult = e.C_TEST_RESULT },
            new() { Channel = "D", Polarity = e.DPolarity, Result = e.DRESULT, TestValue = e.Test_value_D, TestResult = e.D_TEST_RESULT },
            new() { Channel = "E", Polarity = e.EPolarity, Result = e.ERESULT, TestValue = e.Test_value_E, TestResult = e.E_TEST_RESULT },
            new() { Channel = "F", Polarity = e.FPolarity, Result = e.FRESULT, TestValue = e.Test_value_F, TestResult = e.F_TEST_RESULT },
            new() { Channel = "G", Polarity = e.GPolarity, Result = e.GRESULT, TestValue = e.Test_value_G, TestResult = e.G_TEST_RESULT },
            new() { Channel = "H", Polarity = e.HPolarity, Result = e.HRESULT, TestValue = e.Test_value_H, TestResult = e.H_TEST_RESULT },
            new() { Channel = "I", Polarity = e.IPolarity, Result = e.IRESULT, TestValue = e.Test_value_I, TestResult = e.I_TEST_RESULT },
            new() { Channel = "J", Polarity = e.JPolarity, Result = e.JRESULT, TestValue = e.Test_value_J, TestResult = e.J_TEST_RESULT },
        }
    };
}

public class MiddleChannelResultDto
{
    public string Channel { get; set; } = "";
    public string? Polarity { get; set; }
    public string? Result { get; set; }
    public string? TestValue { get; set; }
    public string? TestResult { get; set; }
}

public class MiddleDimensionCheckResultPageDto
{
    public List<MiddleDimensionCheckResultDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// ── DTO cho view Summary (Middle) — so sánh, theo từng Work Order, số Unit S/N ──
// đã test tại Middle với số lượng đã nhập kết quả sản xuất (SVN_ProductionInputLogs).
public class MiddleWorkOrderSummaryDto
{
    // null = các Unit S/N đã test tại Middle nhưng CHƯA có bản ghi tương ứng bên
    // SVN_ProductionInputLogs (chưa nhập kết quả sản xuất cho SN đó).
    public string? WorkOrder { get; set; }

    // Số Unit S/N (đã loại trùng do quét/test lại nhiều lần) test tại Middle ứng với WO này.
    public int MiddleTestedCount { get; set; }

    // Số lượng đã nhập kết quả sản xuất — lấy từ số thứ tự cuối cùng của wo_code
    // (vd "NM/MO/04025-140" => 140), MAX trong các bản ghi cùng master_wo_code.
    public int EnteredQty { get; set; }

    // Dương = Middle test nhiều hơn số đã nhập; Âm = còn thiếu so với số đã nhập; 0 = khớp.
    public int Diff => MiddleTestedCount - EnteredQty;
}
