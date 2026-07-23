namespace PrintApp.Models;

/// <summary>
/// Dữ liệu gửi lên từ form / fetch để sinh ZPL
/// </summary>
public class PrintRequest
{
    public string Content { get; set; } = string.Empty;
    public int LabelWidth { get; set; } = 4;   // inch
    public int LabelHeight { get; set; } = 2;   // inch
}

/// <summary>
/// Kết quả trả về từ API generate-zpl
/// </summary>
public class ZplResult
{
    public string Zpl { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool Success => Error == null;
}