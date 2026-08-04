-- Thêm cột ReworkType vào SM_SerialNumber_Rework — lưu field "x_drop_rework_type" trên
-- mrp.production của Rework WO (Odoo, vd "B"), lấy lúc reprint tem SN Label dưới Rework WO
-- (SakuraController.ReworkReprintSnLabel). Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SerialNumber_Rework') AND name = 'ReworkType')
BEGIN
    ALTER TABLE dbo.SM_SerialNumber_Rework ADD ReworkType NVARCHAR(10) NULL;
END
GO
