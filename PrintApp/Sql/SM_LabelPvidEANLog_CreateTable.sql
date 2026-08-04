-- SM_LabelPvidEANLog — audit trail của trạm Rework (/sakura/rework): mỗi lần thử một bước
-- trong Process (1 Check Work Order -> 2 Check Color & EAN of The Gift Box -> 3 Print Label)
-- ghi 1 dòng MỚI, kể cả các lần FAIL — không ghi đè. Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_LabelPvidEANLog'))
BEGIN
    CREATE TABLE dbo.SM_LabelPvidEANLog (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        WorkOrder   NVARCHAR(50) NOT NULL,
        Pvid        NVARCHAR(50) NULL,     -- mã sản phẩm (bracket code từ Odoo, vd "RM15A-1001RW")
        WoColor     NVARCHAR(20) NULL,     -- màu suy ra từ Work Order
        EanRW       NVARCHAR(30) NULL,     -- EAN thật của sản phẩm Rework (từ Odoo, lấy lúc lookup Work Order)
        EanGiftBox  NVARCHAR(30) NULL,     -- EAN quét trên hộp quà (Check Color & EAN of The Gift Box)
        EanColor    NVARCHAR(20) NULL,     -- màu suy ra từ EanGiftBox đã quét
        Status      NVARCHAR(10) NOT NULL, -- PASS / FAIL
        FailedStep  INT NULL,              -- 1=Check Work Order, 2=Check Color & EAN of The Gift Box, 3=Print Label
        Timeline    DATETIME NOT NULL
    );

    CREATE INDEX IX_SM_LabelPvidEANLog_WorkOrder ON dbo.SM_LabelPvidEANLog (WorkOrder);
END
GO

-- Bảng đã được tạo TỪ TRƯỚC (với cột "Ean" duy nhất, chưa tách EanRW/EanGiftBox) sẽ được nâng
-- cấp tại đây: đổi tên "Ean" -> "EanGiftBox" (giữ nguyên dữ liệu cũ, vì cột này vốn là EAN quét
-- trên hộp quà), rồi thêm cột "EanRW" mới (NULL cho các dòng cũ — không có dữ liệu để backfill).
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_LabelPvidEANLog') AND name = 'Ean')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_LabelPvidEANLog') AND name = 'EanGiftBox')
BEGIN
    EXEC sp_rename 'dbo.SM_LabelPvidEANLog.Ean', 'EanGiftBox', 'COLUMN';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_LabelPvidEANLog') AND name = 'EanRW')
    ALTER TABLE dbo.SM_LabelPvidEANLog ADD EanRW NVARCHAR(30) NULL;
GO
