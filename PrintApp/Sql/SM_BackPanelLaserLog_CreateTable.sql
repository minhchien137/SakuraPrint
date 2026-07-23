-- SM_BackPanelLaserLog — log mỗi lần quét Serial Number ở trạm Laser (Back Panel)
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_BackPanelLaserLog'))
BEGIN
    CREATE TABLE dbo.SM_BackPanelLaserLog (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        WorkOrder    NVARCHAR(50) NOT NULL,
        SerialNumber NVARCHAR(20) NOT NULL,
        Status       NVARCHAR(10) NOT NULL, -- "PASS" hoặc "NG"
        FailedStep   INT NULL, -- NULL nếu PASS toàn bộ quy trình; 1/2/3 = bước quy trình bị NG
        ProductionResultSubName NVARCHAR(50) NULL, -- subName đã dùng khi nhập KQSX (bước 3) thành công
        Timeline     DATETIME NOT NULL
    );

    CREATE INDEX IX_SM_BackPanelLaserLog_WorkOrder ON dbo.SM_BackPanelLaserLog (WorkOrder);
    CREATE INDEX IX_SM_BackPanelLaserLog_SerialNumber ON dbo.SM_BackPanelLaserLog (SerialNumber);
END
GO

-- Bảng đã tạo từ trước khi có FailedStep — thêm cột cho môi trường đã có sẵn bảng.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_BackPanelLaserLog') AND name = 'FailedStep')
BEGIN
    ALTER TABLE dbo.SM_BackPanelLaserLog ADD FailedStep INT NULL;
END
GO

-- Bảng đã tạo từ trước khi có ProductionResultSubName — thêm cột cho môi trường đã có sẵn bảng.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_BackPanelLaserLog') AND name = 'ProductionResultSubName')
BEGIN
    ALTER TABLE dbo.SM_BackPanelLaserLog ADD ProductionResultSubName NVARCHAR(50) NULL;
END
GO

-- Bảng đã tạo từ trước khi có FailReason — thêm cột cho môi trường đã có sẵn bảng.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_BackPanelLaserLog') AND name = 'FailReason')
BEGIN
    ALTER TABLE dbo.SM_BackPanelLaserLog ADD FailReason NVARCHAR(500) NULL;
END
GO
