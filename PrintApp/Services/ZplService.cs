using System.Text;

namespace PrintApp.Services;

public class ZplService
{
    private const int Dpi = 203;

    /// <summary>
    /// Sinh chuỗi ZPL từ nội dung và kích thước nhãn
    /// </summary>
    public string BuildZpl(string content, int widthInch = 4, int heightInch = 2)
    {
        int pw = widthInch * Dpi;
        int ll = heightInch * Dpi;

        var sb = new StringBuilder();
        sb.AppendLine("^XA");
        sb.AppendLine("^CI28");           // UTF-8
        sb.AppendLine($"^PW{pw}");        // label width (dots)
        sb.AppendLine($"^LL{ll}");        // label length (dots)
        sb.AppendLine("^LH0,0");          // label home

        var lines = content.Split(new[] { '\n', '\r' },
                                  StringSplitOptions.RemoveEmptyEntries);
        int y = 30;
        foreach (var line in lines)
        {
            sb.AppendLine($"^FO30,{y}^A0N,30,30^FD{line}^FS");
            y += 44;
        }

        // Thêm barcode Code128 nếu nội dung là 1 dòng ngắn, không dấu cách
        if (lines.Length == 1 && content.Length <= 30 && !content.Contains(' '))
        {
            sb.AppendLine($"^FO30,{y + 10}^BY2^BCN,60,Y,N,N^FD{content}^FS");
        }

        sb.AppendLine("^XZ");
        return sb.ToString();
    }
}