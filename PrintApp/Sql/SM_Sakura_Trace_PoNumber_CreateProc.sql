-- SP cho trang "Universal Lookup" (/sakura/lookup) — quét 1 PO Number, trả về MỌI carton snapshot
-- PoNumber này (có thể trải nhiều Pallet, vì PO Number là free text operator tự gõ lúc in tem
-- Pallet — xem PalletPrintRequest.PoNumber/SM_Sakura_CartonLabel_Data.PoNumber, không có bảng PO
-- riêng). Client tự group theo PalletNumber để liệt kê "các Pallet thuộc PO này".
-- Xem PrintApp/Services/SakuraLookupHandlers.cs (PoNumberLookupHandler).
-- Run against svn_pentaho.

CREATE OR ALTER PROCEDURE dbo.sp_Sakura_Trace_PoNumber
    @PoNumber NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        CartonNumber,
        WorkOrder,
        Color,
        Condition,
        CountSerial,
        Serial,
        ScanDate,
        PalletId,
        PalletNumber,
        IsDeleted,
        IsReprint,
        LastReprintAt,
        ReprintCount,
        IsPalletReprint,
        LastPalletReprintAt,
        PalletReprintCount,
        PoNumber,
        InboundReference,
        WarehouseReference,
        DeliveryAddress,
        ReworkWorkOrder,
        LastRepackedAt,
        RepackCount
    FROM dbo.SM_Sakura_CartonLabel_Data
    WHERE PoNumber = @PoNumber
    ORDER BY PalletNumber ASC, ScanDate ASC;
END
GO
