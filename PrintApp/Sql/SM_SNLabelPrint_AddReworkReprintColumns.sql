-- Thêm cột ReworkReprintCount/LastReworkReprintAt vào SM_SNLabelPrint — đếm riêng số lần tem SN
-- Label đã in lại khi serial đó ĐANG gắn Rework WO (ReworkWorkOrder IS NOT NULL), tách biệt với
-- ReprintCount (tổng số lần in lại nói chung, không phân biệt rework hay không). Cập nhật ở cả 2
-- nơi: SakuraService.MarkReworkedReprintAsync (lần in rework đầu tiên) và MarkReprintedAsync
-- (Manual Reprint, nếu serial đó đã có ReworkWorkOrder từ trước). Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'ReworkReprintCount')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD ReworkReprintCount INT NOT NULL DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'LastReworkReprintAt')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD LastReworkReprintAt DATETIME NULL;
END
GO
