namespace PrintApp.Services;

// Fallback templates — dùng để seed DB (SM_Sakura_ZplTemplate) và làm giá trị dự phòng
// nếu chưa có template nào trong DB. Nguồn thật để CHỈNH SỬA là bảng DB, không phải file này.
public static class ZplTemplates
{
    // ⚠ PLACEHOLDER — sẽ được seed vào SM_Sakura_ZplTemplate (key = "SnLabel").
    // {serialNumber} sẽ được thay vào cả field text người đọc lẫn field Code128 barcode.
    public const string DefaultSnLabel = @"^XA
^CI28
^PW406
^LL203
^FO20,20^A0N,28,28^FD{serialNumber}^FS
^FO20,60^BY2^BCN,90,Y,N,N^FD{serialNumber}^FS
^XZ
";

    // ⚠ PLACEHOLDER — sẽ được seed vào SM_Sakura_ZplTemplate (key = "CartonLabel").
    // Nguồn thật để CHỈNH SỬA vẫn là DB — đây chỉ là bản sao khớp với DB tại thời điểm viết,
    // dùng làm fallback + giá trị seed ban đầu. Xuất trực tiếp từ ZebraDesigner (giữ nguyên
    // cả khối setup/calibration máy in ở đầu file .prn — ~TA000/~JSN/^PR7,7/~SD15/^JUS...).
    // 10 ô Serial Number là các placeholder RIÊNG {sn1}..{sn10} — tọa độ/cỡ chữ/barcode của
    // từng ô đã viết sẵn trong chính template (khác bản cũ dùng 1 khối {snSlots} tự tính toạ
    // độ) — xem SakuraService.BuildCartonLabelZplAsync, chỉ còn việc thay giá trị vào.
    public const string DefaultCartonLabel = @"^XA
~TA000
~JSN
^LT0
^MNW
^MTT
^PON
^PMN
^LH0,0
^JMA
^PR7,7
~SD15
^JUS
^LRN
^CI27
^PA0,1,1,0
^XZ
^XA
^MMT
^PW1200
^LL1800
^LS0
^FO68,51^GB1107,1693,4^FS
^FO135,892^GB0,847,4^FS
^FO225,892^GB0,847,4^FS
^FO292,892^GB0,847,4^FS
^FO358,895^GB0,847,4^FS
^FO427,892^GB0,847,4^FS
^FO489,892^GB0,847,4^FS
^FO554,56^GFA,57,6732,4,:Z64:eJztwwEJAAAIA7AnMYnBjG+OwwabJKOqWnZVVQufqmrhB7kuzEI=:9C00
^FO74,890^GFA,77,448,64,:Z64:eJxjYCAOyP/HBB9wqLXHohYbAOmvJ1ItLv0UaCdJ/wMcfsWm9gCRYQoCDSSoxQYAnKXy0Q==:B0CA
^FO72,1315^GFA,29,32,8,:Z64:eJz7/x8M/v3HQQMADfof3Q==:CA3F
^FO226,1313^GB332,0,4^FS
^FPH,1^FT118,1711^A0B,38,38^FH\^CI28^FDCARTON NUMBER^FS^CI27
^FPH,1^FT754,1591^A0B,33,33^FH\^CI28^FD{sn2}^FS^CI27
^FO763,1290^BY2,2,60^BCB,60,N,N,N,A^FD{sn2}^FS
^FPH,1^FT915,1587^A0B,33,33^FH\^CI28^FD{sn3}^FS^CI27
^FO924,1290^BY2,2,60^BCB,60,N,N,N,A^FD{sn3}^FS
^FPH,1^FT1065,1587^A0B,33,33^FH\^CI28^FD{sn4}^FS^CI27
^FO1074,1290^BY2,2,60^BCB,60,N,N,N,A^FD{sn4}^FS
^FPH,1^FT613,1587^A0B,33,33^FH\^CI28^FD{sn1}^FS^CI27
^FO622,1290^BY2,2,60^BCB,60,N,N,N,A^FD{sn1}^FS
^FPH,1^FT274,1711^A0B,38,38^FH\^CI28^FDCARTON CONTAINS^FS^CI27
^FPH,1^FT118,1275^A0B,38,38^FH\^CI28^FD{cartonNumber}^FS^CI27
^BY3,3,63^FT212,1596^BCB,,N,N,N,A
^FH\^FD{cartonNumber}^FS
^FPH,1^FT341,1711^A0B,38,38^FH\^CI28^FDSKU/PV ID:^FS^CI27
^FPH,1^FT341,1283^A0B,38,38^FH\^CI28^FDDESCRIPTION:^FS^CI27
^FPH,1^FT474,1283^A0B,38,38^FH\^CI28^FDCONDITION:^FS^CI27
^FPH,1^FT474,1711^A0B,38,38^FH\^CI28^FDQUANTITY:^FS^CI27
^FPH,1^FT408,1668^A0B,38,38^FH\^CI28^FD{skuPvId}^FS^CI27
^FPH,1^FT408,1200^A0B,38,38^FH\^CI28^FD{description}^FS^CI27
^FPH,1^FT539,1134^A0B,38,38^FH\^CI28^FD{condition}^FS^CI27
^FPH,1^FT539,1544^A0B,38,38^FH\^CI28^FD{quantity}^FS^CI27
^BY5,19^FT523,857^B7B,19,2,,,N
^FH\^FD{pdf417Data}^FS
^FPH,1^FT754,994^A0B,33,33^FH\^CI28^FD{sn6}^FS^CI27
^FO763,694^BY2,2,60^BCB,60,N,N,N,A^FD{sn6}^FS
^FPH,1^FT915,991^A0B,33,33^FH\^CI28^FD{sn7}^FS^CI27
^FO924,694^BY2,2,60^BCB,60,N,N,N,A^FD{sn7}^FS
^FPH,1^FT1065,991^A0B,33,33^FH\^CI28^FD{sn8}^FS^CI27
^FO1074,694^BY2,2,60^BCB,60,N,N,N,A^FD{sn8}^FS
^FPH,1^FT613,991^A0B,33,33^FH\^CI28^FD{sn5}^FS^CI27
^FO622,694^BY2,2,60^BCB,60,N,N,N,A^FD{sn5}^FS
^FPH,1^FT754,449^A0B,33,33^FH\^CI28^FD{sn10}^FS^CI27
^FO763,148^BY2,2,60^BCB,60,N,N,N,A^FD{sn10}^FS
^FPH,1^FT613,445^A0B,33,33^FH\^CI28^FD{sn9}^FS^CI27
^FO622,148^BY2,2,60^BCB,60,N,N,N,A^FD{sn9}^FS
^PQ1,0,1,Y
^XZ
";

    // Màu → (SKU/PV ID, mô tả, EAN GTIN-13) cho Carton Label + Pallet Label — 1 nguồn dùng
    // chung, đổi màu sửa ở đây. Ean dùng cho tem Pallet (mục 10 — DELIVERY TO/EAN).
    public static readonly IReadOnlyDictionary<string, (string SkuPvId, string Description, string Ean)> CartonColorMeta =
        new Dictionary<string, (string SkuPvId, string Description, string Ean)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Blue"] = ("RM15A-1000NW", "FOLIO BLUE", "7090045253151"),
            ["Pink"] = ("RM15A-1001NW", "FOLIO PINK", "7090045253168"),
            ["Green"] = ("RM15A-1002NW", "FOLIO GREEN", "7090045253175"),
        };

    // Số ô Serial Number tối đa trên 1 tem Carton Label ({sn1}..{sn10} trong template).
    public const int CartonSnPlaceholderCount = 10;

    // ⚠ PLACEHOLDER — sẽ được seed vào SM_Sakura_ZplTemplate (key = "PalletLabel") bởi người
    // dùng sau khi có mã ZPL thật của tem Main Pallet Label. Để rỗng ở đây chỉ để không vỡ luồng
    // build (SakuraService.BuildPalletLabelZplAsync vẫn thay token bình thường vào chuỗi rỗng).
    public const string DefaultPalletLabel = "";
}
