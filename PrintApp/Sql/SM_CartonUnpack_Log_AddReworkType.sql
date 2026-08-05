-- Thêm cột ReworkType vào SM_CartonUnpack_Log — lưu field "x_drop_rework_type" trên
-- mrp.production của Rework WO (Odoo, vd "B"), gắn cùng lúc với ReworkWorkOrder khi serial
-- chuyển UNPACKED -> REWORKED (xem SakuraService.MarkSerialReworkedAsync). Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_CartonUnpack_Log') AND name = 'ReworkType')
BEGIN
    ALTER TABLE dbo.SM_CartonUnpack_Log ADD ReworkType NVARCHAR(10) NULL;
END
GO
