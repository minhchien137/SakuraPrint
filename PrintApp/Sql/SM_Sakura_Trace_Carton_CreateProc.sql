-- SP cho trang "Universal Lookup" (/sakura/lookup) — quét 1 Carton Number, trả về đúng 1 dòng
-- (carton mới nhất khớp mã, ưu tiên dòng chưa bị xoá) gồm đầy đủ thông tin carton + pallet đang
-- gán + audit reprint/repack. Xem PrintApp/Services/SakuraLookupHandlers.cs (CartonLookupHandler).
-- Run against svn_pentaho.

CREATE OR ALTER PROCEDURE dbo.sp_Sakura_Trace_Carton
    @CartonNumber NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
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
    WHERE CartonNumber = @CartonNumber
    ORDER BY IsDeleted ASC, ScanDate DESC;
END
GO
