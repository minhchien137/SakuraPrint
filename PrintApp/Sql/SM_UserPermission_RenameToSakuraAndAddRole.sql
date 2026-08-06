-- Đổi tên SM_UserPermission -> SM_Sakura_UserPermission (bám theo convention đặt tên bảng
-- Sakura dùng chung, vd SM_Sakura_CartonLabel_Data — xem SM_Sakura_CartonLabel_Data_CreateTable.sql)
-- để dễ nhận ra đây là bảng thuộc Sakura khi nhìn danh sách bảng. Đồng thời mở rộng thêm
-- Role/IsActive/DisplayName để dùng cho login toàn site Sakura (thay modal verify-reprint-login
-- cũ — xem AccountController). Run against svn_pentaho.
--
-- ⚠️ Role "LineLeader" — nếu bạn đã chạy bản trước của script này (dùng tên role "Supervisor"),
-- chạy tiếp SM_Sakura_UserPermission_RenameSupervisorToLineLeader.sql để đổi tên role.

-- 1) Đổi tên bảng + index (chỉ chạy nếu bảng cũ còn tên cũ và bảng mới chưa tồn tại).
IF OBJECT_ID('dbo.SM_UserPermission') IS NOT NULL AND OBJECT_ID('dbo.SM_Sakura_UserPermission') IS NULL
BEGIN
    EXEC sp_rename 'dbo.SM_UserPermission', 'SM_Sakura_UserPermission';
    EXEC sp_rename 'dbo.SM_Sakura_UserPermission.IX_SM_UserPermission_Username', 'IX_SM_Sakura_UserPermission_Username', 'INDEX';
END
GO

-- Trường hợp bảng chưa từng được tạo (môi trường mới) -> tạo thẳng với tên mới.
IF OBJECT_ID('dbo.SM_Sakura_UserPermission') IS NULL
BEGIN
    CREATE TABLE dbo.SM_Sakura_UserPermission (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        Username     NVARCHAR(50) NOT NULL,
        PasswordHash NVARCHAR(200) NOT NULL,
        CreatedAt    DATETIME NOT NULL
    );

    CREATE UNIQUE INDEX IX_SM_Sakura_UserPermission_Username ON dbo.SM_Sakura_UserPermission (Username);
END
GO

-- 2) Thêm Role/IsActive/DisplayName.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_UserPermission') AND name = 'Role')
BEGIN
    ALTER TABLE dbo.SM_Sakura_UserPermission ADD Role NVARCHAR(20) NOT NULL CONSTRAINT DF_SM_Sakura_UserPermission_Role DEFAULT ('Worker');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_UserPermission') AND name = 'IsActive')
BEGIN
    ALTER TABLE dbo.SM_Sakura_UserPermission ADD IsActive BIT NOT NULL CONSTRAINT DF_SM_Sakura_UserPermission_IsActive DEFAULT (1);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_UserPermission') AND name = 'DisplayName')
BEGIN
    ALTER TABLE dbo.SM_Sakura_UserPermission ADD DisplayName NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SM_Sakura_UserPermission_Role')
BEGIN
    ALTER TABLE dbo.SM_Sakura_UserPermission
        ADD CONSTRAINT CK_SM_Sakura_UserPermission_Role CHECK (Role IN (N'Worker', N'LineLeader', N'Admin'));
END
GO

-- 3) Seed / nâng quyền tài khoản.
IF NOT EXISTS (SELECT 1 FROM dbo.SM_Sakura_UserPermission WHERE Username = N'Admin')
BEGIN
    INSERT INTO dbo.SM_Sakura_UserPermission (Username, PasswordHash, CreatedAt, Role, IsActive)
    VALUES (N'Admin', N'100000.R5RXrSiRb+ZulN4DmGwHkw==.HprdY9bvSibcO1WZ+bdsq3gCAoVdRauu1FczQGAfeEw=', GETDATE(), N'Admin', 1);
END
ELSE
BEGIN
    UPDATE dbo.SM_Sakura_UserPermission SET Role = N'Admin' WHERE Username = N'Admin';
END
GO

-- Tài khoản Worker dùng chung tạm thời cho công nhân bình thường (chưa cần tách account
-- theo từng người). Đổi mật khẩu sau khi bàn giao cho vận hành — mật khẩu tạm là "worker123"
-- (đã hash sẵn PBKDF2/SHA256 bên dưới).
IF NOT EXISTS (SELECT 1 FROM dbo.SM_Sakura_UserPermission WHERE Username = N'worker')
BEGIN
    INSERT INTO dbo.SM_Sakura_UserPermission (Username, PasswordHash, CreatedAt, Role, IsActive)
    VALUES (N'worker', N'100000.zP5vJqzWkzUvOZ/8Aq0M0A==.NwYQEUqz1J8lCOKhBHfx1nO+wR6f9WM8puYFmQOZmzQ=', GETDATE(), N'Worker', 1);
END
GO

-- Thêm tài khoản khác sau này (LineLeader/Admin): KHÔNG insert mật khẩu dạng plaintext trực
-- tiếp — phải hash trước (PBKDF2/SHA256, xem PrintApp/Services/SimplePasswordHasher.cs), ví dụ:
-- INSERT INTO dbo.SM_Sakura_UserPermission (Username, PasswordHash, CreatedAt, Role, IsActive)
-- VALUES (N'lineleader1', N'100000.<saltBase64>.<hashBase64>', GETDATE(), N'LineLeader', 1);
