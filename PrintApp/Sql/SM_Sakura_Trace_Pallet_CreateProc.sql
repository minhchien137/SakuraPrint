-- SP cho trang "Universal Lookup" (/sakura/lookup) — quét 1 Pallet ID hoặc Pallet Number, trả về
-- MỌI carton đang gán vào pallet đó (khớp theo PalletId HOẶC PalletNumber, vì 2 cột này khác thời
-- điểm gán — xem CartonSnScanLog.PalletId/PalletNumber). Client tự tổng hợp Box Count/Unit Count
-- từ danh sách trả về. Xem PrintApp/Services/SakuraLookupHandlers.cs (PalletLookupHandler).
-- Run against svn_pentaho.

CREATE OR ALTER PROCEDURE dbo.sp_Sakura_Trace_Pallet
    @PalletCode NVARCHAR(50)
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
    WHERE PalletId = @PalletCode OR PalletNumber = @PalletCode
    ORDER BY IsDeleted ASC, ScanDate ASC;
END
GO
