# magnet-log-collector

Background service (Node.js) đọc định kỳ file kết quả kiểm tra kích thước magnet
(`D:\MagnetLogfile_Summary\yyyyMMdd.csv` — thực tế đã xác nhận là **CSV**, vẫn
hỗ trợ thêm `.xlsx`/`.xls` phòng khi trạm khác xuất Excel), lưu các dòng mới
vào SQL Server bảng `[svn_pentaho].[dbo].[SVN_MiddleDimensionCheckResult]`.

## Yêu cầu

- Node.js LTS (khuyến nghị 18.x hoặc 20.x) cài trên máy có quyền đọc thư mục
  `D:\MagnetLogfile_Summary` (thường là máy đo/máy trạm tại xưởng).
- Bảng `SVN_MiddleDimensionCheckResult` đã được tạo sẵn trong SQL Server, gồm
  45 cột dữ liệu theo đúng thứ tự liệt kê trong `src/columns.js`, cộng thêm
  `id`, `source_file`, `created_at`, và **UNIQUE INDEX trên (UNIT_SN, DATE_TIME)**.

## Định dạng file nguồn thật (CSV) — xác nhận từ file mẫu thực tế

Header CSV thật (tiếng Trung, 24 cột, KHÔNG map theo tên — chỉ map theo vị trí):

```
条码,测试结果,测试时间,单位,A测试值,A极性,B测试值,B极性,...,J测试值,J极性
```

| Vị trí | Cột CSV | Ý nghĩa | Map vào cột DB |
|---|---|---|---|
| 1 | 条码 | Barcode | `UNIT_SN` **và** `BARCODE_CONTENT1` (file chỉ có 1 cột barcode, dùng chung cho cả 2 cột DB) |
| 2 | 测试结果 | Kết quả test tổng | `STATUS` |
| 3 | 测试时间 | Thời gian test (`yyyy-MM-dd HH:mm:ss`) | `DATE_TIME` |
| 4 | 单位 | Đơn vị đo (luôn là `高斯` = Gauss) | `UT` |
| 5-24 | `A测试值,A极性` … `J测试值,J极性` | Giá trị đo + cực tính từng kênh A-J | `Test_value_A/APolarity` … `Test_value_J/JPolarity` |

**Quan trọng:** file CSV thật **không có** cột kết quả PASS/FAIL riêng cho
từng kênh (khác với 45 cột giả định ban đầu trong bảng DB) — nên 20 cột
`ARESULT`..`JRESULT` và `A_TEST_RESULT`..`J_TEST_RESULT` **luôn để trống**
(không có dữ liệu nguồn tương ứng để điền). Nếu sau này cần điền gì vào các
cột đó, cho tôi biết để chỉnh lại `src/csvMapper.js`.

## Cài đặt nhanh — chỉ 1 file bat (khuyến nghị)

**Trước tiên, mở `config.json` kiểm tra lại `sourceDir` và `db`** (xem bảng
cấu hình bên dưới), rồi:

1. Chuột phải file **`INSTALL_ALL.bat`** → **Run as administrator**
   (hoặc double-click, file tự xin quyền Admin nếu chưa có).
2. Ngồi chờ — file tự làm hết:
   - Tự cài Node.js nếu máy chưa có.
   - Tự `npm install`.
   - Tự tải NSSM và cài Windows Service tên **MagnetLogCollector** (tự khởi
     động cùng Windows, tự restart nếu crash).
   - Tự start service luôn.
3. Xong, không cần làm gì thêm. Kiểm tra bằng `Get-Service MagnetLogCollector`.

Muốn gỡ service: chạy `uninstall-service.ps1` (PowerShell quyền Admin).

## Cài đặt thủ công (nếu không dùng INSTALL_ALL.bat)

```bash
cd D:\PrintService\magnet-log-collector
npm install
```

## Chạy debug (console, không qua NSSM)

```bash
npm start
```

Log vừa in ra console vừa ghi vào `logs/collector-yyyyMMdd.log`. Nhấn `Ctrl+C`
để dừng khi debug.

## Xem trên trình duyệt — dashboard "đã lưu đủ dữ liệu chưa"

Service tự chạy kèm 1 trang web nhỏ, không cần cài gì thêm:

```
http://<tên-hoặc-IP-máy-chạy-service>:8022
```

Nếu mở ngay trên máy đang chạy service thì dùng `http://localhost:8022`.
Trang tự làm mới mỗi 15 giây, hiển thị theo từng file (hôm nay/hôm qua):
tổng số dòng trong Excel, số dòng hợp lệ, số dòng bị bỏ qua (kèm lý do khi
rê chuột vào), số dòng hiện có trong DB, và 1 nhãn to rõ:

- 🟢 **ĐỦ DỮ LIỆU** — DB đã lưu đủ số dòng hợp lệ.
- 🟠 **THIẾU N** — còn thiếu N dòng, xem log để biết lý do.
- 🔴 **LỖI DB** — chu kỳ vừa rồi không kết nối được DB (kèm thông báo lỗi).

Muốn xem từ máy khác trong cùng mạng LAN, cần mở port `8022` trên firewall
Windows của máy chạy service (`New-Netfw Rule` hoặc Windows Defender Firewall
GUI). Trang này chỉ đọc (GET), không nhận input gì từ người xem nên an toàn
để mở trong mạng nội bộ.

Muốn tắt hẳn trang này: sửa `config.json` → `"statusServer": { "enabled": false }`.
Muốn đổi cổng: sửa `"statusServer": { "port": ... }`.

## Cấu hình (`config.json`)

| Field | Ý nghĩa |
|---|---|
| `sourceDir` | Thư mục chứa file Excel nguồn. **Sửa nếu máy chạy service không phải `D:\MagnetLogfile_Summary`.** |
| `pollIntervalMs` | Chu kỳ quét (mặc định 30000 = 30 giây). |
| `tempDir` | Thư mục tạm dùng để copy file trước khi đọc (an toàn khi file gốc đang bị khóa). |
| `logDir` | Thư mục log, xoay file theo ngày. |
| `logLevel` | `debug` / `info` / `warn` / `error`. Dùng `debug` khi cần xem chi tiết lý do bỏ qua file/dòng. |
| `timeZone` | Múi giờ dùng để tính "hôm nay/hôm qua" và tên file log (mặc định `Asia/Bangkok`, tương đương giờ Việt Nam). |
| `table` | Tên bảng đích đầy đủ. |
| `statusServer.enabled/port/host` | Bật/tắt và cấu hình trang dashboard xem trên trình duyệt (xem mục trên). |
| `csvEncoding` | `auto` (mặc định, tự nhận diện UTF-8/GBK), `utf8`, hoặc `gbk`. Đổi sang `gbk` nếu thấy chữ Trung Quốc (vd cột UT) hiện lỗi font trên dashboard — dấu hiệu file CSV thật sự xuất theo mã ANSI/GBK (máy đo Windows tiếng Trung) thay vì UTF-8. |
| `db.server`, `db.database`, `db.user`, `db.password` | **⚠️ Cần điền/kiểm tra lại.** Đã điền sẵn tạm thời trùng với connection string `ProdConnectionString` trong `PrintApp/appsettings.json` (cùng SQL Server `103.121.89.139`, database `svn_pentaho`) vì đây là service phụ trợ cho cùng hệ thống. Đổi lại nếu môi trường thực tế dùng server/tài khoản khác. |
| `db.options.encrypt` | **Để `false`** — bắt buộc khi kết nối SQL Server qua địa chỉ IP (không phải hostname) từ Node.js: driver `tedious` không cho phép dùng địa chỉ IP làm TLS SNI servername khi `encrypt: true`, sẽ báo lỗi kết nối. Nếu sau này đổi sang kết nối bằng hostname (DNS) thì có thể bật `encrypt: true` lại. |

**Chỗ cần điền/kiểm tra trước khi chạy thật:**
1. `sourceDir` — xác nhận đúng đường dẫn trên máy sẽ cài service.
2. `db.server` / `db.password` — xác nhận đúng, hoặc đổi sang tài khoản SQL
   riêng cho service này nếu không muốn dùng chung tài khoản `sa` với web app.

## Cài chạy ngầm bằng NSSM (production)

```powershell
# PowerShell chạy với quyền Administrator
cd D:\PrintService\magnet-log-collector
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\install-service.ps1
```

Script tự tải NSSM (nếu chưa có), cài `npm install` (nếu chưa có
`node_modules`), tạo service Windows tên **MagnetLogCollector**, tự khởi động
cùng Windows, tự restart nếu crash.

Gỡ service:

```powershell
.\uninstall-service.ps1
```

## Xem log

- Log nghiệp vụ (số dòng insert mỗi chu kỳ, cảnh báo dòng lỗi, lỗi DB/file):
  `logs/collector-yyyyMMdd.log`
- Log stdout/stderr của tiến trình NSSM (crash, exception ở tầng process):
  `service-logs/stdout.log`, `service-logs/stderr.log`

## Cơ chế chống trùng — không dùng state file

Mỗi chu kỳ, với file của hôm nay và hôm qua, service:
1. Đọc và validate **toàn bộ** dòng dữ liệu trong file (không chỉ phần mới).
2. Hỏi thẳng DB xem cặp `(UNIT_SN, DATE_TIME)` nào **đã có sẵn** cho đúng file
   đó (`source_file = tên file`).
3. Chỉ insert những dòng **chưa có** trong DB.

Vì luôn xét lại toàn bộ file (không dựa vào "đã đọc đến dòng thứ mấy" lưu
trong 1 file trạng thái riêng), service **tự phục hồi hoàn toàn** trong mọi
tình huống, không cần lo xử lý case riêng:
- Máy tắt qua đêm, sáng hôm sau/ngày kế mới bật lại → chu kỳ đầu tiên tự vét
  nốt phần còn thiếu của file hôm qua (miễn file đó còn tồn tại trong
  `sourceDir`).
- Mất kết nối DB giữa chừng → chu kỳ sau tự thử lại, không mất dữ liệu.
- File bị phần mềm đo tạo lại/ghi đè → không có khái niệm "offset bị lệch"
  vì không lưu offset.

UNIQUE INDEX `(UNIT_SN, DATE_TIME)` trên bảng vẫn là lớp bảo vệ cuối cùng
chống trùng (bắt lỗi 2601/2627) cho trường hợp hiếm gặp 2 tiến trình cùng ghi
1 dòng ở cùng thời điểm.

## Kiểm tra đã lưu ĐỦ dữ liệu hay chưa

Có 3 cách, từ tiện nhất đến chắc chắn nhất:

**Cách 1 — mở dashboard trên trình duyệt (khuyến nghị, xem mục ở trên):**
`http://localhost:8022` (hoặc IP máy chạy service).

**Cách 2 — dùng lệnh kiểm tra tự động trong terminal:**

```bash
cd D:\magnet-log-collector
npm run check              # kiểm tra file của HÔM NAY
node src/check.js 20260716 # kiểm tra 1 ngày cụ thể
node src/check.js all      # kiểm tra TẤT CẢ file đang có trong sourceDir
```

Lệnh này **tự đọc lại file Excel** (đếm số dòng hợp lệ, KHÔNG insert gì) và
**tự query DB** (đếm số dòng đã lưu ứng với đúng file đó), rồi in ra so sánh:

```
===== 20260716.xlsx =====
Tong so dong du lieu trong Excel      : 128
So dong hop le (du dieu kien de luu)  : 127
So dong bi bo qua (thieu UNIT_SN/STATUS/DATE_TIME loi): 1
   - Dong 45: thieu UNIT_SN - bo qua dong.
So dong hien co trong DB (source_file = 20260716.xlsx) : 127
==> DU DU LIEU (DB da luu >= so dong hop le trong file).
```

Nếu báo `THIEU n dong` — kiểm tra lại log nghiệp vụ để biết lý do (mất kết
nối DB, file đang bị khóa liên tục, v.v.). Không sửa gì thì cứ chờ chu kỳ
tiếp theo, service sẽ tự đọc lại và tự vét đúng phần còn thiếu.

**Cách 3 — query DB trực tiếp** (nếu có sẵn công cụ SQL, không cần Node):

```sql
SELECT source_file, COUNT(*) AS so_dong
FROM [svn_pentaho].[dbo].[SVN_MiddleDimensionCheckResult]
GROUP BY source_file
ORDER BY source_file DESC;
```

So số này với số dòng thực tế trong từng file Excel (mở file, xem số dòng
cuối cùng có dữ liệu, trừ đi 1 dòng header).

## Cách test end-to-end

1. Đảm bảo service đang chạy (console `npm start` hoặc Windows Service).
2. Mở file hôm nay (`yyyyMMdd.csv` — hoặc `.xlsx`/`.xls` nếu trạm đó xuất Excel —
   trong `D:\MagnetLogfile_Summary`), thêm 1 dòng dữ liệu mới (điền đủ ít nhất
   cột barcode/UNIT_SN, STATUS, DATE_TIME), lưu lại.
   - Nếu phần mềm đo đang giữ file mở, service sẽ tự bỏ qua chu kỳ đó và thử lại
     chu kỳ sau — không cần đóng phần mềm đo.
3. Chờ tối đa 1 chu kỳ (`pollIntervalMs`, mặc định 30 giây).
4. Kiểm tra log `logs/collector-yyyyMMdd.log` — sẽ thấy dòng dạng:
   `... tong X dong, hop le Y, da co san trong DB Z, insert moi 1, trung lap luc insert 0, khong hop le 0.`
   hoặc mở dashboard `http://localhost:8022` để xem trực quan.
5. Query lại bảng để xác nhận:
   ```sql
   SELECT TOP 5 * FROM [svn_pentaho].[dbo].[SVN_MiddleDimensionCheckResult]
   ORDER BY id DESC;
   ```
6. Test chống trùng: chạy lại đúng chu kỳ (không sửa gì thêm) — log sẽ báo
   `da co san trong DB` tăng thêm đúng bằng số dòng vừa insert, và `insert moi`
   = 0 (không insert lại).

## Lưu ý kỹ thuật / điểm khác so với đề bài gốc

- **File nguồn thật là CSV, không phải Excel** (phát hiện khi test thật trên
  máy xưởng — file `.xlsx`/`.xls` trong đề bài gốc không khớp thực tế). Đã
  thêm hẳn 1 nhánh đọc CSV riêng (`src/csvMapper.js` + phần CSV trong
  `src/excelReader.js`), map đúng theo cấu trúc 24 cột thật (xem mục "Định
  dạng file nguồn thật" ở trên). Nhánh đọc Excel cũ vẫn được giữ nguyên,
  service tự nhận diện theo đuôi file tìm thấy (`.csv` → `.xlsx` → `.xls`).
- Encoding CSV tự nhận diện UTF-8/GBK (xem `csvEncoding` trong bảng cấu hình)
  vì máy đo chạy Windows tiếng Trung đôi khi xuất CSV theo mã ANSI/GBK thay
  vì UTF-8 — nếu đoán sai, chữ Hán (cột UT) sẽ hiện lỗi font trên dashboard,
  lúc đó chỉ cần set `csvEncoding: "gbk"` (hoặc `"utf8"`) trong config.json.
- Đề bài gợi ý dùng `exceljs` cho `.xlsx` và `xlsx`/SheetJS cho `.xls`. Thực tế
  service này dùng **`xlsx` (SheetJS) cho cả hai định dạng**, vì:
  1. SheetJS đọc được cả `.xlsx` lẫn `.xls` (binary cũ) trong cùng 1 API, nên
     không cần rẽ nhánh code theo đuôi file.
  2. Yêu cầu "giữ nguyên văn giá trị cell thành chuỗi" (kể cả dấu phẩy ngăn
     cách hàng nghìn như `"190,734"`) cần đọc `cell.w` (chuỗi đã format hiển
     thị) — SheetJS hỗ trợ trực tiếp; `exceljs` không tính toán number format
     thành chuỗi hiển thị nên sẽ trả về số thô, sai yêu cầu.
  Nếu muốn bắt buộc dùng đúng `exceljs` cho nhánh `.xlsx`, cho biết để điều
  chỉnh lại `src/excelReader.js`.
- Đề bài gợi ý dùng state file JSON để lưu offset đã đọc. Bản hiện tại **bỏ
  hẳn state file**, mỗi chu kỳ xét lại toàn bộ file hôm nay/hôm qua và hỏi
  thẳng DB xem dòng nào đã có (xem mục "Cơ chế chống trùng" ở trên) — đơn
  giản hơn, tự phục hồi tốt hơn (kể cả khi máy tắt qua đêm), đổi lại là mỗi
  chu kỳ tốn thêm 1-2 câu query DB nhẹ (SELECT theo source_file) so với cách
  cũ. Với quy mô dữ liệu 1 trạm đo/ngày, chi phí này không đáng kể.
