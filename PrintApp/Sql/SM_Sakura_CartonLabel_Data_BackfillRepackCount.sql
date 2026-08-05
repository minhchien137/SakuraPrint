-- Backfill RepackCount cho các carton đã Repack TRƯỚC KHI cột RepackCount/LastRepackedAt tồn tại
-- (Condition/ReworkWorkOrder đã được set đúng bởi code cũ, nhưng chưa có logic tăng đếm) — carton
-- nào đã có ReworkWorkOrder (= đã qua Repack ít nhất 1 lần) mà RepackCount vẫn = 0 thì chắc chắn
-- là thiếu do thứ tự deploy, set về 1 (không biết chính xác đã repack bao nhiêu lần trước đó, chỉ
-- biết chắc >= 1).
-- Run against svn_pentaho, SAU KHI đã chạy SM_Sakura_CartonLabel_Data_AddRepackColumns.sql.

UPDATE dbo.SM_Sakura_CartonLabel_Data
SET RepackCount = 1
WHERE ReworkWorkOrder IS NOT NULL
  AND RepackCount = 0;
GO

-- Backfill LastRepackedAt bằng dữ liệu THẬT lấy từ SM_CartonUnpack_Log.RepackedAt — bảng này ghi
-- RepackedAt từ trước khi CartonSnScanLog.LastRepackedAt tồn tại, nên vẫn giữ đúng thời điểm thật
-- của lần repack cũ. Lấy MAX(RepackedAt) theo từng CartonNumber (= lần repack gần nhất) cho những
-- dòng đang NULL. Carton nào không có dòng REPACKED tương ứng trong SM_CartonUnpack_Log (vd dữ
-- liệu test/thao tác thủ công từ trước khi có bảng này) sẽ vẫn giữ NULL — không có cách nào bịa ra
-- thời điểm chính xác cho trường hợp đó.
UPDATE c
SET c.LastRepackedAt = sub.MaxRepackedAt
FROM dbo.SM_Sakura_CartonLabel_Data c
CROSS APPLY (
    SELECT MAX(u.RepackedAt) AS MaxRepackedAt
    FROM dbo.SM_CartonUnpack_Log u
    WHERE u.CartonNumber = c.CartonNumber AND u.Status = 'REPACKED'
) sub
WHERE c.ReworkWorkOrder IS NOT NULL
  AND c.LastRepackedAt IS NULL
  AND sub.MaxRepackedAt IS NOT NULL;
GO
