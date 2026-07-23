-- SM_MiddleLog — log mỗi lần quét Serial Number ở trạm Nhập kết quả sản xuất (Middle)
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_MiddleLog'))
BEGIN
    CREATE TABLE dbo.SM_MiddleLog (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        WorkOrder    NVARCHAR(50) NOT NULL,
        SerialNumber NVARCHAR(20) NOT NULL,
        Status       NVARCHAR(10) NOT NULL, -- "PASS" hoặc "FAIL"
        FailedStep   INT NULL, -- NULL nếu PASS toàn bộ quy trình; 1/2/3 = bước quy trình bị FAIL
        ProductionResultSubName NVARCHAR(50) NULL, -- subName đã dùng khi nhập KQSX (bước 3) thành công
        FailReason   NVARCHAR(500) NULL,
        Timeline     DATETIME NOT NULL
    );

    CREATE INDEX IX_SM_MiddleLog_WorkOrder ON dbo.SM_MiddleLog (WorkOrder);
    CREATE INDEX IX_SM_MiddleLog_SerialNumber ON dbo.SM_MiddleLog (SerialNumber);
END
GO
