-- SM_UserPermission — tài khoản đăng nhập để mở khoá tab Reprint ở trang SnLabel
-- (/sakura/snlabel). PasswordHash lưu dạng "{iterations}.{saltBase64}.{hashBase64}"
-- (PBKDF2/SHA256) — xem PrintApp/Services/SimplePasswordHasher.cs. KHÔNG bao giờ lưu
-- mật khẩu dạng plaintext vào cột này.
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_UserPermission'))
BEGIN
    CREATE TABLE dbo.SM_UserPermission (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        Username     NVARCHAR(50) NOT NULL,
        PasswordHash NVARCHAR(200) NOT NULL,
        CreatedAt    DATETIME NOT NULL
    );

    CREATE UNIQUE INDEX IX_SM_UserPermission_Username ON dbo.SM_UserPermission (Username);
END
GO

-- Seed tài khoản đầu tiên: Admin / 123 (tạm thời, đổi lại sau khi bàn giao cho vận hành).
-- PasswordHash bên dưới đã hash sẵn (PBKDF2/SHA256, 100000 vòng) — KHÔNG bao giờ insert
-- mật khẩu dạng plaintext trực tiếp.
IF NOT EXISTS (SELECT 1 FROM dbo.SM_UserPermission WHERE Username = N'Admin')
BEGIN
    INSERT INTO dbo.SM_UserPermission (Username, PasswordHash, CreatedAt)
    VALUES (N'Admin', N'100000.R5RXrSiRb+ZulN4DmGwHkw==.HprdY9bvSibcO1WZ+bdsq3gCAoVdRauu1FczQGAfeEw=', GETDATE());
END
GO

-- Thêm tài khoản khác sau này: KHÔNG insert mật khẩu dạng plaintext trực tiếp — phải nhờ
-- hash trước (PBKDF2/SHA256, xem PrintApp/Services/SimplePasswordHasher.cs), rồi insert
-- PasswordHash đã hash vào đây, ví dụ:
-- INSERT INTO dbo.SM_UserPermission (Username, PasswordHash, CreatedAt)
-- VALUES (N'reprint_admin', N'100000.<saltBase64>.<hashBase64>', GETDATE());
