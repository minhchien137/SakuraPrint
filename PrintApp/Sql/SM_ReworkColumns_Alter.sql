-- Cột ReworkWorkOrder cho luồng Unpack/Rework/Repack (/sakura/rework):
-- - SM_SNLabelPrint.ReworkWorkOrder: set lúc reprint tem gift box dưới 1 Rework WO
--   (POST /api/sakura/snlabel/rework-reprint) — WorkOrder gốc của serial giữ nguyên không đổi.
-- - SM_Sakura_CartonLabel_Data.ReworkWorkOrder: set lúc in lại tem Carton sau khi Repack đủ
--   serial (Condition đổi thành Refurb cùng lúc) — chỉ phản ánh lần rework GẦN NHẤT của carton,
--   lịch sử đầy đủ tra ở SM_CartonUnpack_Log. Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'ReworkWorkOrder')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD ReworkWorkOrder NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'ReworkWorkOrder')
BEGIN
    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ADD ReworkWorkOrder NVARCHAR(50) NULL;
END
GO
