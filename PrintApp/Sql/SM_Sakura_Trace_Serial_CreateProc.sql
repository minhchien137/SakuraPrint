-- SP cho trang "Universal Lookup" (/sakura/lookup) — quét 1 Serial Number, trả về đúng 1 dòng gộp
-- (LEFT JOIN) thông tin in tem (SM_SNLabelPrint) + carton đang chứa serial này (tìm bằng LIKE trên
-- cột Serial dạng CSV của SM_Sakura_CartonLabel_Data, vì bảng đó không có 1-dòng-1-serial — xem
-- CHẨN ĐOÁN trong CLAUDE.md). Nếu serial chưa được xếp vào carton nào thì các cột Carton* = NULL.
-- Ưu tiên carton CHƯA bị xoá nếu serial (hiếm khi) khớp nhiều dòng carton lịch sử.
-- Xem PrintApp/Services/SakuraLookupHandlers.cs (SerialLookupHandler).
-- Run against svn_pentaho.

CREATE OR ALTER PROCEDURE dbo.sp_Sakura_Trace_Serial
    @SerialNumber NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        p.SerialNumber,
        p.Model,
        p.Variant,
        p.Color            AS PrintColor,
        p.ProductionLine,
        p.ProductionDate,
        p.RunningNumber,
        p.PrintedAt,
        p.PrintedBy,
        p.BatchId,
        p.WorkOrder         AS PrintWorkOrder,
        p.ReprintCount,
        p.LastReprintedAt,
        p.LastReprintedBy,
        p.Ean,
        p.Status,
        p.FailedStep,
        p.ReworkWorkOrder,
        p.ReworkReprintCount,
        p.LastReworkReprintAt,
        c.CartonNumber,
        c.WorkOrder          AS CartonWorkOrder,
        c.Color              AS CartonColor,
        c.Condition          AS CartonCondition,
        c.PalletId,
        c.PalletNumber,
        c.ScanDate           AS CartonScanDate,
        c.IsDeleted          AS CartonIsDeleted
    FROM dbo.SM_SNLabelPrint p
    LEFT JOIN dbo.SM_Sakura_CartonLabel_Data c
        ON c.Serial LIKE '%' + @SerialNumber + '%'
    WHERE p.SerialNumber = @SerialNumber
    ORDER BY c.IsDeleted ASC, c.ScanDate DESC;
END
GO
