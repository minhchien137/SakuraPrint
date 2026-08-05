-- Backfill ReworkType cho các dòng SM_CartonUnpack_Log đã REWORKED/REPACKED TRƯỚC KHI cột
-- ReworkType tồn tại (insert bởi code cũ, chưa có SakuraController.ReworkReprintReportResult
-- gắn ReworkType) — thực tế toàn bộ Rework WO đã dùng tới giờ đều có x_drop_rework_type = "B"
-- trên Odoo, nên set cứng "B" cho các dòng đang trống. CHỈ áp dụng cho dòng đã có ReworkWorkOrder
-- (đã qua bước Rework) — dòng còn UNPACKED (chưa rework) giữ nguyên NULL vì chưa có Rework WO nào
-- gắn vào. Run against svn_pentaho, SAU KHI đã chạy SM_CartonUnpack_Log_AddReworkType.sql.

UPDATE dbo.SM_CartonUnpack_Log
SET ReworkType = 'B'
WHERE ReworkWorkOrder IS NOT NULL
  AND (ReworkType IS NULL OR ReworkType = '');
GO
