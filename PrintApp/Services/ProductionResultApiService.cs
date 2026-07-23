using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PrintApp.Services;

public class ProductionApiResult
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
}

// Gọi 2 API sản xuất (sigmaworldwide.io) dùng ở bước 3 (Enter Production Result) của
// trạm Laser (Back Panel) — chạy sau khi bước 2 (Print Laser/ghi SN.txt) đã thành công.
// Dùng ở BackPanelController.VerifySerial.
public class ProductionResultApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _checkLotSerialFgPath;
    private readonly string _inputProductionResultLogPath;
    private readonly TimeSpan _timeout;
    private readonly string _logDirectory;

    public ProductionResultApiService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient();
        _baseUrl = config["BackPanel:Laser:ProductionApi:BaseUrl"] ?? "https://api.sigmaworldwide.io";
        _checkLotSerialFgPath = config["BackPanel:Laser:ProductionApi:CheckLotSerialFgPath"] ?? "/api/Production/CheckLotSerialFG";
        _inputProductionResultLogPath = config["BackPanel:Laser:ProductionApi:InputProductionResultLogPath"] ?? "/api/Production/InputProductionResultLog";
        int timeoutSeconds = config.GetValue<int?>("BackPanel:Laser:ProductionApi:TimeoutSeconds") ?? 5;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        // Service này chạy trên server (IIS) — không phải máy dưới xưởng — nên fallback mặc
        // định phải là đường dẫn ngay trong thư mục app (server nào cũng có quyền ghi),
        // không được lấy đường dẫn ổ đĩa của máy xưởng (vd D:\LOG) làm mặc định.
        _logDirectory = config["BackPanel:Laser:LogDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "logs");
    }

    // Check xem serial đã được nhập kết quả sản xuất (cho product_id này) trước đó chưa.
    // ok=true  -> chưa nhập, được phép gọi tiếp InputProductionResultLogAsync.
    // ok=false -> đã nhập trước đó (trùng) — Message chứa lý do, hiện nguyên văn cho vận hành viên.
    public Task<ProductionApiResult> CheckLotSerialFgAsync(int productId, string serial)
    {
        var payload = new
        {
            product_id = productId,
            has_tracking = "serial",
            serial_code = serial
        };
        return PostAsync(_checkLotSerialFgPath, payload);
    }

    // Nhập kết quả sản xuất cho 1 serial — chỉ gọi sau khi CheckLotSerialFgAsync trả ok=true.
    public Task<ProductionApiResult> InputProductionResultLogAsync(
        string workOrder, string subName, string serial, int productId, decimal totalQuantity)
    {
        var payload = new
        {
            name = workOrder,
            subName,
            quantity = "1",
            productTracking = "serial",
            serial,
            productID = productId.ToString(CultureInfo.InvariantCulture),
            totalQuantity = totalQuantity.ToString("0.####", CultureInfo.InvariantCulture),
            products = Array.Empty<object>()
        };
        return PostAsync(_inputProductionResultLogPath, payload);
    }

    private async Task<ProductionApiResult> PostAsync(string path, object payload)
    {
        string url = _baseUrl.TrimEnd('/') + path;
        string requestJson = JsonSerializer.Serialize(payload);
        string status = "ERROR";
        string responseBody = "";

        try
        {
            using var cts = new CancellationTokenSource(_timeout);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cts.Token);
            responseBody = await response.Content.ReadAsStringAsync();
            status = ((int)response.StatusCode).ToString();

            var parsed = JsonSerializer.Deserialize<ProductionApiResult>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            await AppendLogAsync(url, requestJson, status, responseBody);
            return parsed ?? new ProductionApiResult { Ok = false, Message = $"Phản hồi không hợp lệ từ server (HTTP {status})." };
        }
        catch (OperationCanceledException)
        {
            await AppendLogAsync(url, requestJson, "TIMEOUT", responseBody);
            return new ProductionApiResult { Ok = false, Message = "Timeout khi gọi API sản xuất — vui lòng thử lại." };
        }
        catch (Exception ex)
        {
            await AppendLogAsync(url, requestJson, "EXCEPTION", ex.Message);
            return new ProductionApiResult { Ok = false, Message = $"Lỗi kết nối API sản xuất: {ex.Message}" };
        }
    }

    // Log kỹ thuật mỗi request/response (timestamp, endpoint, payload, status, body) —
    // best-effort, không được làm hỏng luồng chính nếu bản thân việc ghi log gặp lỗi.
    private async Task AppendLogAsync(string url, string requestJson, string status, string responseBody)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            string logFile = Path.Combine(_logDirectory, $"api_{DateTime.Now:yyyyMMdd}.log");
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{url}\treq={requestJson}\tstatus={status}\tresp={responseBody}{Environment.NewLine}";
            await File.AppendAllTextAsync(logFile, line);
        }
        catch
        {
            // Best-effort — bỏ qua lỗi ghi log để không ảnh hưởng luồng gọi API chính.
        }
    }
}
