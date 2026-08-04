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
        -- Giờ Trung Quốc (UTC+8) — tính từ GETUTCDATE() thay vì GETDATE() để không phụ thuộc
        -- timezone hệ điều hành của SQL Server, đồng bộ với mọi UpdatedAt khác trong hệ thống
        -- (ứng dụng luôn set giá trị này tường minh lúc insert, default này chỉ là lưới an toàn
        -- cho trường hợp insert thẳng bằng SQL không qua app).
        UpdatedAt           DATETIME           NOT NULL CONSTRAINT DF_SM_Sakura_PalletInfoTemplate_UpdatedAt DEFAULT DATEADD(HOUR, 8, GETUTCDATE()),
        CONSTRAINT PK_SM_Sakura_PalletInfoTemplate PRIMARY KEY (Id),
        CONSTRAINT UQ_SM_Sakura_PalletInfoTemplate_Name UNIQUE (TemplateName)
    );
END
-- Bảng đã được tạo từ trước (bản chưa có PO Number) -> bổ sung cột thay vì tạo lại bảng.
ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_PalletInfoTemplate') AND name = 'PoNumber')
BEGIN
    ALTER TABLE dbo.SM_Sakura_PalletInfoTemplate ADD PoNumber NVARCHAR(100) NOT NULL DEFAULT '';
END
GO

-- Bảng đã được tạo TỪ TRƯỚC với default GETDATE() (giờ server, không phải giờ Trung Quốc) trên
-- UpdatedAt -> tìm đúng default constraint hiện có (tên tự sinh, không cố định) và thay bằng
-- DATEADD(HOUR, 8, GETUTCDATE()). Idempotent — chỉ chạy nếu default hiện tại KHÔNG phải đã là
-- constraint DF_SM_Sakura_PalletInfoTemplate_UpdatedAt ở trên.
IF EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.SM_Sakura_PalletInfoTemplate')
      AND c.name = 'UpdatedAt'
      AND dc.name <> 'DF_SM_Sakura_PalletInfoTemplate_UpdatedAt'
)
BEGIN
    DECLARE @oldDefaultName sysname;
    SELECT @oldDefaultName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.SM_Sakura_PalletInfoTemplate') AND c.name = 'UpdatedAt';

    EXEC('ALTER TABLE dbo.SM_Sakura_PalletInfoTemplate DROP CONSTRAINT ' + @oldDefaultName);
    ALTER TABLE dbo.SM_Sakura_PalletInfoTemplate
        ADD CONSTRAINT DF_SM_Sakura_PalletInfoTemplate_UpdatedAt DEFAULT DATEADD(HOUR, 8, GETUTCDATE()) FOR UpdatedAt;
END
GO

