-- Thêm cột LastRepackedAt/RepackCount vào SM_Sakura_CartonLabel_Data — giữ lại dấu vết carton
-- này từng ở Condition="New" trước khi Repack đổi sang "Refurb" (ReworkWorkOrder IS NOT NULL đã
-- tự là dấu hiệu "đã qua Repack", không cần thêm cột boolean riêng). Cập nhật cùng lúc với
-- Condition/ReworkWorkOrder mỗi lần Repack — xem SakuraService.RecordRepackResultAsync.
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'LastRepackedAt')
BEGIN
    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ADD LastRepackedAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'RepackCount')
BEGIN
    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ADD RepackCount INT NOT NULL DEFAULT (0);
END
GO
