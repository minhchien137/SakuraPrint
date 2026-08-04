-- SM_SerialNumber_Rework — bảng tích hợp cho hệ thống nhập kết quả sản xuất bên ngoài (do team
-- khác phụ trách): mỗi lần trạm SN Label reprint 1 tem gift box dưới 1 Rework Work Order, insert
-- 1 dòng MỚI vào đây (WorkOrder = Rework WO, SerialNumber) TRƯỚC khi gọi bước nhập kết quả sản
-- xuất — hệ thống bên ngoài tự check bảng này để biết serial này là rework và bỏ qua validate
-- trùng serial khi thêm bản ghi kết quả sản xuất mới dưới Rework WO. Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_SerialNumber_Rework'))
BEGIN
    CREATE TABLE dbo.SM_SerialNumber_Rework (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        WorkOrder    NVARCHAR(50) NOT NULL,  -- Rework WO
        SerialNumber NVARCHAR(20) NOT NULL,
        CreatedAt    DATETIME NOT NULL
    );

    CREATE INDEX IX_SM_SerialNumber_Rework_SerialNumber ON dbo.SM_SerialNumber_Rework (SerialNumber);
END
GO
