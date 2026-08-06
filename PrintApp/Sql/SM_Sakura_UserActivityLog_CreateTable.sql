-- SM_Sakura_UserActivityLog — ghi lại mỗi lần 1 tài khoản vào 1 trang thao tác chính của
-- Sakura (Index, SnLabel, CartonSN, Rework, CartonSnReprint — KHÔNG log các trang History,
-- chỉ đọc, ít giá trị audit) — xem Filters/LogPageVisitFilter.cs. Lưu giờ Trung Quốc (UTC+8),
-- giống các bảng log khác của Sakura (SM_LabelPvidEANLog...), KHÔNG phải giờ Việt Nam.
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_Sakura_UserActivityLog'))
BEGIN
    CREATE TABLE dbo.SM_Sakura_UserActivityLog (
        Id       INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL,
        Path     NVARCHAR(200) NOT NULL,
        Timeline DATETIME NOT NULL
    );

    CREATE INDEX IX_SM_Sakura_UserActivityLog_Username ON dbo.SM_Sakura_UserActivityLog (Username);
    CREATE INDEX IX_SM_Sakura_UserActivityLog_Timeline ON dbo.SM_Sakura_UserActivityLog (Timeline);
END
GO
