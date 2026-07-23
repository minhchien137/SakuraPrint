# Project.md — Tài liệu cấu trúc dự án InternetPrint

## Tổng quan

**InternetPrint** là ứng dụng web ASP.NET Core 8 dùng để sinh chuỗi lệnh ZPL và điều phối in nhãn lên máy in Zebra trong môi trường nhà xưởng. Ứng dụng chạy trên server, nhưng việc gửi lệnh in thực tế đến máy in được thực hiện bởi **trình duyệt** thông qua một bridge agent cục bộ (`localhost:8021`) — server không kết nối trực tiếp đến máy in.

- Framework: ASP.NET Core 8 MVC
- RootNamespace: `ZplPrinter`
- Path base: `/print` (mọi URL đều có tiền tố này)
- Database: SQL Server tại `10.10.99.10`, catalog `svn_pentaho`
- ORM: Entity Framework Core 8 (schema có sẵn, không dùng migrations)

---

## Cấu trúc thư mục

```
InternetPrint/
├── InternetPrint.sln
└── PrintApp/
    ├── PrintApp.csproj
    ├── Program.cs              ← Cấu hình DI, middleware, routes
    ├── appsettings.json        ← Connection string SQL Server
    ├── Controllers/
    │   ├── PrintController.cs
    │   ├── ToastController.cs
    │   └── ToastSerialController.cs
    ├── Models/
    │   ├── PrintModel.cs           ← Request/Response cho ZPL generic
    │   ├── ToastModel.cs           ← Request models cho Toast label/pallet
    │   ├── AstroLabelData.cs       ← Entity: SVN_Astro_Label_Data
    │   ├── PrinterInfo.cs          ← Entity: SVN_Printer_Info_New
    │   ├── SvnToastSerialInfo.cs   ← Entity: SVN_Toast_Serial_Info + FCT/FQC request models
    │   └── Entity/
    │       └── AppDbContext.cs     ← EF Core DbContext
    ├── Services/
    │   ├── ZplService.cs           ← Sinh ZPL generic
    │   ├── ToastService.cs         ← Toast label/pallet logic + TCP send
    │   └── ToastSerialService.cs   ← FCT/FQC serial tracking logic
    └── Views/
        ├── Print/
        │   └── Index.cshtml        ← Giao diện in nhãn ZPL generic
        ├── Toast/
        │   ├── Index.cshtml        ← Giao diện in nhãn/pallet Toast
        │   ├── FctScan.cshtml      ← Giao diện scan FCT
        │   └── FqcScan.cshtml      ← Giao diện scan FQC
        ├── Shared/
        │   └── _Layout.cshtml      ← Layout chung (font Google, site.css)
        ├── _ViewImports.cshtml
        └── _ViewStart.cshtml
```

---

## Luồng in (kiến trúc then chốt)

```
Trình duyệt
    │
    ├─ 1. Gọi API server → nhận ZPL string
    │       (POST /Print/GenerateZpl  hoặc  POST /api/toast/print)
    │
    ├─ 2. Gửi ZPL đến bridge cục bộ
    │       POST http://localhost:8021/print
    │       body: { zpl, printerIp, printerPort }
    │
    └─ 3. Bridge gửi ZPL qua TCP/USB đến máy in Zebra
```

> **Server không bao giờ kết nối trực tiếp đến máy in.** Bridge `localhost:8021` là một agent riêng biệt chạy trên máy của operator (không thuộc codebase này).

---

## Controllers

### `PrintController`
| Route | Method | Mô tả |
|---|---|---|
| `/` hoặc `/Print/Index` | GET | Trả về trang in ZPL generic |
| `/Print/GenerateZpl` | POST | Nhận `PrintRequest` (content, width, height), trả về `ZplResult` chứa chuỗi ZPL |

Không có logic phức tạp — chỉ ủy thác cho `ZplService.BuildZpl()`.

---

### `ToastController`
Quản lý toàn bộ luồng in nhãn thùng và nhãn pallet cho dòng sản phẩm **Toast Go 3**.

| Route | Method | Mô tả |
|---|---|---|
| `/toast` | GET | Trang in Toast, load danh sách máy in từ DB (`target = "Toast"`) |
| `/api/toast/checkserial/{prefix}/{serial}` | GET | Kiểm tra serial đã in chưa (dùng trước khi in để tránh trùng) |
| `/api/toast/label` | POST | Lưu dữ liệu thùng vào `SVN_Astro_Label_Data` |
| `/api/toast/print` | POST | Build ZPL nhãn thùng từ template DB, trả về chuỗi ZPL |
| `/api/toast/printpallet` | POST | Build ZPL nhãn pallet (tổng hợp serials của cả pallet) |
| `/api/toast/count/{palletId}` | GET | Đếm số thùng trong pallet |
| `/api/toast/pallet/{palletId}` | GET | Lấy danh sách thùng trong pallet |
| `/api/toast/delete/{serial}` | GET | Xóa một thùng khỏi pallet |
| `/api/toast/printers` | GET | Lấy danh sách máy in Toast từ DB |

---

### `ToastSerialController`
Quản lý quy trình kiểm tra chất lượng serial **FCT → FQC** cho sản phẩm Toast.

| Route | Method | Mô tả |
|---|---|---|
| `/toast/fct` | GET | Trang scan FCT |
| `/toast/fqc` | GET | Trang scan FQC (truyền `PrinterInfo` vào view) |
| `/FctScanToast/Submit` | POST | Ghi FCT status lần đầu cho serial mới |
| `/UpdateFqcStatus` | POST | Cập nhật FQC status (chỉ khi FCT đã OK) |
| `/api/toastserial/{serial}` | GET | Tra cứu thông tin serial |

**Quy tắc nghiệp vụ FCT/FQC:**
- Serial phải đúng **13 ký tự**.
- FCT status: `"OK"` hoặc `"NG"` (normalized thành uppercase).
- FQC bị chặn nếu: serial chưa qua FCT, FCT là NG, hoặc FQC đã được ghi rồi.
- Nếu serial trùng khi insert → trả 409 Conflict.

---

## Services

### `ZplService`
Sinh ZPL từ nội dung văn bản thuần.

- DPI cố định: **203**.
- Tính `PW` (label width) và `LL` (label length) từ kích thước inch.
- Mỗi dòng text được đặt tại `y += 44` dots (font 30×30).
- Tự động thêm barcode **Code128** nếu nội dung là 1 dòng, ≤ 30 ký tự, không có dấu cách.

### `ToastService`
Xử lý toàn bộ logic Toast label:

- **`BuildToastZpl`**: Lấy template từ `PrinterInfo.ZPL_Temp`, thay thế các placeholder token (`{toastPartNumber}`, `{serialBlock1..5}`, `{lotId1..3}`, `{poNumber}`, ...).
- **`GenerateSerialBlock`**: Sinh đoạn ZPL cho một serial (barcode + text), dùng tọa độ cứng được truyền vào.
- **`BuildPalletZplAsync`**: Tổng hợp tối đa 30 serials từ DB thành 3 nhóm (`ser1`, `ser2`, `ser3`), nếu nhóm nào rỗng thì xóa cả lệnh QR tương ứng khỏi template.
- **`ResolveSkuMeta`**: Map SKU code → (description, descFr, modelNumber). Hiện tại chỉ có `HW0032`, `HW0172`, `HW0170` → "Toast Go 3 Charging Dock".
- **`SendZplAsync`**: Gửi ZPL trực tiếp qua TCP socket (dùng nội bộ, không gọi qua bridge).

### `ToastSerialService`
- **`VietnamNow()`**: Lấy giờ hiện tại theo múi giờ Việt Nam (ICT/UTC+7), dùng timezone ID khác nhau giữa Windows (`"SE Asia Standard Time"`) và Linux (`"Asia/Bangkok"`).
- **`SubmitFctAsync`**: Kiểm tra trùng → tạo mới `SVNToastSerialInfo`.
- **`UpdateFqcAsync`**: Validate business rules → cập nhật `FQCStatus` và `FQCStatusDatetime`.

---

## Models / Entities

### Entities (ánh xạ DB)

#### `AstroLabelData` → bảng `SVN_Astro_Label_Data`
| Cột | Kiểu | Mô tả |
|---|---|---|
| `Id` | int (PK) | Auto-increment |
| `Date` | string? | Ngày scan (format `yyyyMMdd`) |
| `PackageID` | string? | ID package (dùng làm prefix tìm kiếm) |
| `Serial` | string? | Chuỗi serials nối nhau bằng dấu phẩy |
| `ScanDate` | string? | Datetime dạng string |
| `PalletID` | string? | ID pallet |
| `isDeleted` | bool | Soft delete flag |
| `EmployeeID` | string? | ID nhân viên |
| `CountSerial` | int | Số lượng serial trong thùng |

#### `PrinterInfo` → bảng `SVN_Printer_Info_New`
| Cột | Kiểu | Mô tả |
|---|---|---|
| `ID_Printer` | string (PK) | Mã máy in |
| `Name_Printer` | string? | Tên máy in (dùng để lookup đặc biệt: `"Pallet_Toast"`, `"TEST_TOAST_1SERIAL"`) |
| `IP_Printer` | string? | Địa chỉ IP |
| `Port_Printer` | string? | Cổng TCP |
| `ZPL_Temp` | string? | Template ZPL chứa placeholder tokens |
| `target` | string? | Phân loại máy in (ví dụ: `"Toast"`) |
| `width`, `height`, `Size` | string? | Kích thước nhãn |

#### `SVNToastSerialInfo` → bảng `SVN_Toast_Serial_Info`
| Cột (column name) | Property | Mô tả |
|---|---|---|
| `serial_number` (PK) | `SerialNumber` | Serial 13 ký tự |
| `work_order` | `WorkOrder` | Mã work order |
| `FCT_status` | `FCTStatus` | `"OK"` hoặc `"NG"` |
| `FCT_status_datetime` | `FCTStatusDatetime` | Thời điểm scan FCT |
| `FQC_status` | `FQCStatus` | Status FQC |
| `FQC_status_datetime` | `FQCStatusDatetime` | Thời điểm scan FQC |
| `update_by_svncode` | `updateBySVNCode` | Mã SVN cập nhật |

### Request/Response models (không ánh xạ DB)

| Class | File | Dùng cho |
|---|---|---|
| `PrintRequest` | `PrintModel.cs` | Input `POST /Print/GenerateZpl` |
| `ZplResult` | `PrintModel.cs` | Response `GenerateZpl` |
| `ToastLabelRequest` | `ToastModel.cs` | Input `POST /api/toast/print` |
| `ToastPalletRequest` | `ToastModel.cs` | Input `POST /api/toast/printpallet` |
| `AstroLabelDataDto` | `ToastModel.cs` | Input `POST /api/toast/label` |
| `FctSubmitReq` | `SvnToastSerialInfo.cs` | Input `POST /FctScanToast/Submit` |
| `FqcUpdateRequest` | `SvnToastSerialInfo.cs` | Input `POST /UpdateFqcStatus` |

---

## Views

### `Print/Index.cshtml`
Giao diện đơn giản: textarea nhập nội dung, chọn IP/port/kích thước, nút in. Hỗ trợ 2 mode:
- **TCP/IP**: nhập IP và port thủ công.
- **USB**: chọn từ danh sách máy in USB (gọi `localhost:8021`).

JavaScript inline khởi tạo `window.APP_URLS.generateZpl`, sau đó load `~/js/print.js` để xử lý logic in.

### `Toast/Index.cshtml`
Giao diện phức tạp nhất. Toàn bộ logic nằm trong `@section Scripts`:
- Load danh sách máy in từ `Model` (List<PrinterInfo>), auto-fill IP/port khi chọn.
- Persist form vào `localStorage` (key: `toast_form_v1`).
- Serial grid động: số ô input = `quantity`, mỗi ô validate real-time (13 ký tự, không trùng trong grid, không trùng DB). Khi điền đủ serial cuối → tự động trigger in.
- Modal quản lý pallet: hiển thị danh sách thùng, cho phép xóa từng thùng.
- SKU map client-side (`resolveSku`) phải đồng bộ với `ToastService.ResolveSkuMeta` phía server.

### `Toast/FctScan.cshtml`
Trang scan FCT: nhập serial + chọn status (OK/NG), gọi `POST /FctScanToast/Submit`. Hiển thị kết quả ngay trên trang.

### `Toast/FqcScan.cshtml`
Trang scan FQC: nhận `@model PrinterInfo` từ controller (lookup sẵn `TEST_TOAST_1SERIAL`). Scan serial → gọi `POST /UpdateFqcStatus` → hiển thị kết quả + tự động in nhãn nếu thành công.

### `Shared/_Layout.cshtml`
Layout tối giản: không có navbar. Load font Google (Azeret Mono + DM Sans), `site.css`, và đặt `window.APP_BASE` để JS tính đúng path base `/print`.

---

## Cấu hình (`Program.cs`)

```
DI Services đăng ký:
  - AppDbContext (Scoped) → SQL Server
  - ZplService (Scoped)
  - ToastService (Scoped)
  - ToastSerialService (Scoped)

Middleware pipeline:
  UseForwardedHeaders
  UsePathBase("/print")   ← hardcode path base
  Use(... context.Request.PathBase = "/print" ...)
  UseStaticFiles
  UseRouting
  UseAuthorization

Default route: {controller=Print}/{action=Index}/{id?}
```

`ToastSerialController` có `[IgnoreAntiforgeryToken]` vì các endpoint FCT/FQC được gọi từ client không có cookie CSRF (có thể là thiết bị scan barcode công nghiệp).

---

## Điểm chú ý khi phát triển

- **SKU map phải đồng bộ hai nơi**: `ToastService.ResolveSkuMeta` (C#) và `resolveSku` trong `Toast/Index.cshtml` (JavaScript).
- **ZPL template lưu trong DB**: thay đổi layout nhãn cần sửa trực tiếp cột `ZPL_Temp` trong bảng `SVN_Printer_Info_New`, không phải trong code.
- **Tọa độ serial block cứng**: vị trí barcode/text cho 5 serial trong `GenerateSerialBlock` được hardcode trong `ToastController.PrintLabel` (call site), không phải trong service.
- **`localhost:8021` bắt buộc**: không có bridge agent chạy trên máy operator thì không in được từ các trang Toast và Print.
- **Không có migrations**: database schema tồn tại sẵn, EF Core chỉ đọc/ghi, không tạo/sửa bảng.
