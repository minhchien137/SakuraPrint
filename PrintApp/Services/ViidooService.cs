using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;

namespace PrintApp.Services;

public class ViidooSearchResult
{
    public string ProductCode { get; set; } = "";
    public string? Color { get; set; }
    public decimal? Quantity { get; set; }
    public int? ProductId { get; set; }
}

// Tra cứu Work Order trên Odoo (mrp.production) — trả về mã sản phẩm, màu, số lượng.
// Dùng chung bởi ViidooController (test endpoint độc lập) và SakuraController
// (chế độ "In qua Work Order" ở Sakura/SnLabel).
public class ViidooService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;

    private const string OdooApiUrl = "https://sigmaworldwide.io/web/dataset/call_kw/mrp.production/web_search_read";
    private const string OdooProductReadUrl = "https://sigmaworldwide.io/web/dataset/call_kw/product.product/read";

    // Số cấp WO cha tối đa sẽ truy ngược khi tìm màu (tránh lặp vô hạn).
    private const int MaxParentLookupDepth = 5;

    // Danh sách màu hợp lệ của sản phẩm. Có màu mới chỉ cần thêm vào đây.
    private static readonly string[] ValidColors = { "Green", "Blue", "Pink" };

    public ViidooService(IHttpClientFactory httpClientFactory, AppDbContext db)
    {
        _httpClient = httpClientFactory.CreateClient();
        _db = db;
    }

    // Lấy cookie từ bảng SVN_Defect_Cookie (lấy record đầu tiên).
    // Trả về null nếu bảng rỗng.
    private async Task<string?> GetCookieFromDbAsync()
    {
        var record = await _db.SvnDefectCookies.AsNoTracking().FirstOrDefaultAsync();
        return record == null || string.IsNullOrWhiteSpace(record.cookie) ? null : record.cookie;
    }

    // Debug: trả về nguyên record thô (JSON) mà Odoo trả về cho productionCode — dùng để
    // đối chiếu trực tiếp khi nghi ngờ 1 field (vd. product_id) không khớp với Odoo UI.
    public async Task<JsonElement?> GetRawRecordAsync(string productionCode)
    {
        string? cookie = await GetCookieFromDbAsync();
        if (cookie == null)
            throw new SakuraCodedException("odoo.cookieNotConfigured", "Odoo cookie not configured. Please update SVN_Defect_Cookie table.");

        return await SearchProductionRecordAsync(productionCode, cookie);
    }

    // Tra cứu mã sản phẩm + màu sắc + số lượng từ Work Order.
    // - Màu: quét toàn bộ tên sản phẩm, tìm từ trong ValidColors
    // - Nếu không có màu -> truy ngược lên WO cha qua "origin" (tối đa MaxParentLookupDepth cấp)
    // - productCode và quantity luôn lấy từ WO đang tìm kiếm (WO con)
    // Trả về null nếu không tìm thấy Work Order / không có product_id hợp lệ.
    // Ném SakuraCodedException("odoo.cookieNotConfigured", ...) nếu chưa cấu hình cookie.
    public async Task<ViidooSearchResult?> SearchAsync(string productionCode)
    {
        string? cookie = await GetCookieFromDbAsync();
        if (cookie == null)
            throw new SakuraCodedException("odoo.cookieNotConfigured", "Odoo cookie not configured. Please update SVN_Defect_Cookie table.");

        var record = await SearchProductionRecordAsync(productionCode, cookie);
        if (record == null) return null;

        string? productDescription = GetProductDescription(record.Value);
        string? productCode = ExtractCodeFromDescription(productDescription);
        decimal? quantity = record.Value.TryGetProperty("product_qty", out var qtyEl) && qtyEl.ValueKind == JsonValueKind.Number
            ? qtyEl.GetDecimal()
            : null;
        int? productId = ExtractProductId(record.Value);

        if (string.IsNullOrEmpty(productCode)) return null;

        string? color = ExtractColorFromDescription(productDescription);

        var currentRecord = record.Value;
        int depth = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { productionCode };

        while (string.IsNullOrEmpty(color) && depth < MaxParentLookupDepth)
        {
            string? origin = currentRecord.TryGetProperty("origin", out var originEl) && originEl.ValueKind == JsonValueKind.String
                ? originEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(origin) || origin.Equals("false", StringComparison.OrdinalIgnoreCase))
                break;

            if (!visited.Add(origin))
                break;

            var parentRecord = await SearchProductionRecordAsync(origin, cookie);
            if (parentRecord == null) break;

            string? parentDescription = GetProductDescription(parentRecord.Value);
            color = ExtractColorFromDescription(parentDescription);
            currentRecord = parentRecord.Value;
            depth++;
        }

        return new ViidooSearchResult { ProductCode = productCode, Color = color, Quantity = quantity, ProductId = productId };
    }

    // Tra WO -> lấy product_id (dùng lại SearchAsync) -> gọi product.product/read để lấy
    // mã EAN (x_custcode). Dùng cho bước "Check EAN" ở Process (SnLabel): người vận hành
    // quét mã EAN trên sản phẩm, so khớp với mã trả về từ Odoo.
    // Trả về null nếu không tìm thấy WO, WO không có product_id, hoặc sản phẩm không có x_custcode.
    public async Task<string?> GetEanByWorkOrderAsync(string productionCode)
    {
        var result = await SearchAsync(productionCode);
        if (result?.ProductId is not int productId) return null;

        return await GetProductEanAsync(productId);
    }

    // Gọi Odoo read trên product.product theo productId, trả về x_custcode (mã EAN) hoặc
    // null nếu sản phẩm không có field này / không tìm thấy.
    public async Task<string?> GetProductEanAsync(int productId)
    {
        string? cookie = await GetCookieFromDbAsync();
        if (cookie == null)
            throw new SakuraCodedException("odoo.cookieNotConfigured", "Odoo cookie not configured. Please update SVN_Defect_Cookie table.");

        string finalJson = $@"
    {{
        ""id"": 555555556,
        ""jsonrpc"": ""2.0"",
        ""method"": ""call"",
        ""params"": {{
            ""model"": ""product.product"",
            ""method"": ""read"",
            ""args"": [[{productId}], [""x_custcode""]],
            ""kwargs"": {{
                ""context"": {{
                    ""lang"": ""vi_VN"",
                    ""tz"": ""Asia/Ho_Chi_Minh"",
                    ""uid"": 2,
                    ""allowed_company_ids"": [1]
                }}
            }}
        }}
    }}";

        var jsonContent = new StringContent(finalJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, OdooProductReadUrl) { Content = jsonContent };
        request.Headers.Add("Cookie", cookie);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            Console.WriteLine($"Odoo returned error: {error}");
            return null;
        }

        // read() trả result là mảng record thẳng (khác web_search_read có wrapper "records").
        if (!root.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Array ||
            result.GetArrayLength() == 0)
        {
            return null;
        }

        var record = result[0];
        if (record.TryGetProperty("x_custcode", out var eanEl) && eanEl.ValueKind == JsonValueKind.String)
        {
            string? ean = eanEl.GetString();
            return string.IsNullOrWhiteSpace(ean) ? null : ean;
        }

        return null;
    }

    // Gọi Odoo web_search_read trên mrp.production theo productionCode,
    // trả về record đầu tiên (JsonElement, đã Clone khỏi JsonDocument gốc) hoặc null nếu không tìm thấy.
    private async Task<JsonElement?> SearchProductionRecordAsync(string productionCode, string cookie)
    {
        // JsonSerializer.Serialize bọc chuỗi vào dấu ngoặc kép + escape ký tự đặc biệt,
        // tránh việc productionCode phá vỡ cấu trúc JSON khi nối chuỗi.
        string safeCode = JsonSerializer.Serialize(productionCode);

        string finalJson = $@"
    {{
        ""id"": 555555555,
        ""jsonrpc"": ""2.0"",
        ""method"": ""call"",
        ""params"": {{
            ""model"": ""mrp.production"",
            ""method"": ""web_search_read"",
            ""args"": [],
            ""kwargs"": {{
                ""limit"": 80,
                ""offset"": 0,
                ""order"": """",
                ""context"": {{
                    ""lang"": ""vi_VN"",
                    ""tz"": ""Asia/Ho_Chi_Minh"",
                    ""uid"": 2,
                    ""allowed_company_ids"": [1],
                    ""bin_size"": true,
                    ""default_company_id"": 1
                }},
                ""count_limit"": 10001,
                ""domain"": [
                    ""&"",
                    [""picking_type_id.active"", ""="", true],
                    ""|"",
                    [""name"", ""ilike"", {safeCode}],
                    [""origin"", ""ilike"", {safeCode}]
                ],
                ""fields"": [
                    ""name"", ""product_id"", ""origin"", ""product_qty"",
                    ""product_uom_id"", ""state""
                ]
            }}
        }}
    }}";

        var jsonContent = new StringContent(finalJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, OdooApiUrl) { Content = jsonContent };
        request.Headers.Add("Cookie", cookie);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            Console.WriteLine($"Odoo returned error: {error}");
            return null;
        }

        if (!root.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("records", out var records) ||
            records.ValueKind != JsonValueKind.Array ||
            records.GetArrayLength() == 0)
        {
            return null;
        }

        // Ưu tiên record có name khớp chính xác với productionCode
        // (vì domain dùng ilike trên cả name và origin nên có thể trả về nhiều record).
        // .Clone() bắt buộc vì JsonDocument (doc) sẽ bị dispose khi hàm này return.
        foreach (var rec in records.EnumerateArray())
        {
            if (rec.TryGetProperty("name", out var nameEl) &&
                nameEl.ValueKind == JsonValueKind.String &&
                string.Equals(nameEl.GetString(), productionCode, StringComparison.OrdinalIgnoreCase))
            {
                return rec.Clone();
            }
        }

        return records[0].Clone();
    }

    // Lấy chuỗi mô tả sản phẩm từ product_id[1] của record.
    // Ví dụ: "[WSS03-00307] Back Panel- Semi-finished product - Sakura Green"
    private static string? GetProductDescription(JsonElement record)
    {
        if (record.TryGetProperty("product_id", out var productId) &&
            productId.ValueKind == JsonValueKind.Array &&
            productId.GetArrayLength() >= 2)
        {
            var second = productId[1];
            return second.ValueKind == JsonValueKind.String ? second.GetString() : second.ToString();
        }
        return null;
    }

    // Lấy id số của sản phẩm từ product_id[0] của record — ví dụ product_id: [1356, "..."] -> 1356.
    // Dùng ở trạm Laser để gửi kèm khi gọi API Nhập kết quả sản xuất (sẽ bổ sung sau).
    private static int? ExtractProductId(JsonElement record)
    {
        if (record.TryGetProperty("product_id", out var productId) &&
            productId.ValueKind == JsonValueKind.Array &&
            productId.GetArrayLength() >= 1 &&
            productId[0].ValueKind == JsonValueKind.Number)
        {
            return productId[0].GetInt32();
        }
        return null;
    }

    // Trích mã sản phẩm trong ngoặc vuông: "[WSS03-00307] ..." -> "WSS03-00307"
    private static string? ExtractCodeFromDescription(string? productDescription)
    {
        if (string.IsNullOrEmpty(productDescription)) return null;

        var match = Regex.Match(productDescription, @"^\[([^\]]+)\]");
        return match.Success ? match.Groups[1].Value : null;
    }

    // Quét toàn bộ tên sản phẩm, nếu tìm thấy từ nào nằm trong danh sách màu
    // (Green, Blue, Pink) thì trả về màu đó (không phân biệt hoa thường).
    //
    // Dùng \b (word boundary) để tránh khớp nhầm khi màu là một phần của từ khác,
    // ví dụ "Greenland" sẽ KHÔNG bị nhận nhầm là "Green".
    private static string? ExtractColorFromDescription(string? productDescription)
    {
        if (string.IsNullOrEmpty(productDescription)) return null;

        foreach (var color in ValidColors)
        {
            if (Regex.IsMatch(productDescription, $@"\b{Regex.Escape(color)}\b", RegexOptions.IgnoreCase))
                return color; // Trả về đúng chuẩn trong danh sách: "Green", "Blue", "Pink"
        }

        return null; // Không tìm thấy màu nào -> caller sẽ truy ngược lên WO cha
    }
}
