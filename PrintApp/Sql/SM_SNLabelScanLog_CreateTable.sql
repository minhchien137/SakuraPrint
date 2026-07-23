-- SM_SNLabelScanLog — log MỖI lần quét EAN+Serial ở Process (Check EAN -> Check Color &
-- Serial Number -> Print Label) trên trang SnLabel, kể cả các lần FAIL (không bị ghi đè
-- như SM_SNLabelPrint, vốn chỉ giữ đúng 1 dòng cho mỗi serial IN THÀNH CÔNG).
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_SNLabelScanLog'))
BEGIN
    CREATE TABLE dbo.SM_SNLabelScanLog (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        WorkOrder       NVARCHAR(50) NOT NULL,
        Ean             NVARCHAR(30) NULL,
        SerialNumber    NVARCHAR(20) NOT NULL,
        Model           NVARCHAR(10) NULL,
        Variant         NVARCHAR(2) NULL,
        Color           NVARCHAR(20) NULL,
        ProductionLine  NVARCHAR(1) NULL,
        RunningNumber   NVARCHAR(3) NULL,
        RunningNumberInt INT NULL,
        Status          NVARCHAR(10) NOT NULL, -- "PASS" / "FAIL" / "PENDING"
        FailedStep      INT NULL, -- NULL nếu PASS; 1=Check EAN, 2=Check Color, 3=Check Serial (KQSX), 4=Print Label
        Timeline        DATETIME NOT NULL
    );

    CREATE INDEX IX_SM_SNLabelScanLog_WorkOrder ON dbo.SM_SNLabelScanLog (WorkOrder);
    CREATE INDEX IX_SM_SNLabelScanLog_SerialNumber ON dbo.SM_SNLabelScanLog (SerialNumber);
END
GO
