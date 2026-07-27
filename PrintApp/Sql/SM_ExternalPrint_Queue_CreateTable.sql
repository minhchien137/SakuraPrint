-- SM_ExternalPrint_Queue — hàng đợi lệnh in cho ExternalPrintController.
-- Lý do: server chạy PrintApp (ds.sigmaworldwide.io) không có route mạng thật tới LAN nhà
-- máy nên không thể tự gửi ZPL thẳng tới máy in (xem debugLocalRouteIp trả về IP public thay
-- vì IP LAN). Thay vào đó API chỉ ghi lệnh in vào bảng này; PrintService (server.js) chạy
-- trên máy trong xưởng (cùng LAN với máy in) sẽ tự poll bảng này qua API rồi tự in.
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_ExternalPrint_Queue'))
BEGIN
    CREATE TABLE dbo.SM_ExternalPrint_Queue (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        Serial       NVARCHAR(100) NOT NULL,
        PrinterId    NVARCHAR(50) NOT NULL,
        PrinterIp    NVARCHAR(50) NOT NULL,
        PrinterPort  INT NOT NULL,
        Zpl          NVARCHAR(MAX) NOT NULL,
        Status       NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending | Claimed | Printed | Failed
        CreatedAt    DATETIME2 NOT NULL,
        ClaimedAt    DATETIME2 NULL,
        CompletedAt  DATETIME2 NULL,
        ErrorMessage NVARCHAR(500) NULL
    );

    CREATE INDEX IX_SM_ExternalPrint_Queue_Status ON dbo.SM_ExternalPrint_Queue (Status);
END
GO

-- Tem LotWip (thay cho mẫu SnLabel cũ) cần lưu thêm các giá trị đã tra cứu được (Product WO
-- từ SVN_ProductionInputLogs.master_wo_code, Product từ Odoo/Viidoo, Lot Qty từ component_list)
-- để có thể xem lại/debug sau này, không chỉ lưu mỗi ZPL đã build sẵn.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_ExternalPrint_Queue') AND name = 'ProductWO')
    ALTER TABLE dbo.SM_ExternalPrint_Queue ADD ProductWO NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_ExternalPrint_Queue') AND name = 'Product')
    ALTER TABLE dbo.SM_ExternalPrint_Queue ADD Product NVARCHAR(300) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_ExternalPrint_Queue') AND name = 'LotQty')
    ALTER TABLE dbo.SM_ExternalPrint_Queue ADD LotQty DECIMAL(18,6) NULL;
GO
