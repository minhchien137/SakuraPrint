-- Bảng lưu các "Pallet Info Template" (PO Number / Inbound Reference / Warehouse Reference /
-- Delivery Address) cho vùng Print Pallet trên trang Carton SN — chọn 1 template là điền đủ cả
-- 4 trường, khỏi gõ tay mỗi lần in.
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SM_Sakura_PalletInfoTemplate')
BEGIN
    CREATE TABLE dbo.SM_Sakura_PalletInfoTemplate (
        Id                  INT IDENTITY(1,1) NOT NULL,
        TemplateName        NVARCHAR(100)      NOT NULL,
        PoNumber            NVARCHAR(100)      NOT NULL DEFAULT '',
        InboundReference    NVARCHAR(100)      NOT NULL DEFAULT '',
        WarehouseReference  NVARCHAR(100)      NOT NULL DEFAULT '',
        DeliveryAddress     NVARCHAR(500)      NOT NULL DEFAULT '',
        UpdatedAt           DATETIME           NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_SM_Sakura_PalletInfoTemplate PRIMARY KEY (Id),
        CONSTRAINT UQ_SM_Sakura_PalletInfoTemplate_Name UNIQUE (TemplateName)
    );
END
-- Bảng đã được tạo từ trước (bản chưa có PO Number) -> bổ sung cột thay vì tạo lại bảng.
ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_PalletInfoTemplate') AND name = 'PoNumber')
BEGIN
    ALTER TABLE dbo.SM_Sakura_PalletInfoTemplate ADD PoNumber NVARCHAR(100) NOT NULL DEFAULT '';
END
