-- Đổi tên Role "Supervisor" -> "LineLeader" trong SM_Sakura_UserPermission (theo yêu cầu đổi
-- thuật ngữ). Chỉ cần chạy nếu bạn đã từng chạy SM_UserPermission_RenameToSakuraAndAddRole.sql
-- bản cũ (dùng tên role "Supervisor") — bản mới của script đó đã dùng thẳng "LineLeader" rồi.
-- Run against svn_pentaho.
--
-- ⚠️ Sau khi chạy script này, các tài khoản đang có Role=Supervisor phải ĐĂNG XUẤT rồi ĐĂNG
-- NHẬP LẠI — cookie đăng nhập hiện tại vẫn mang claim Role cũ ("Supervisor") cho tới khi
-- login lại, AccountController mới đọc lại Role mới từ DB.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_Sakura_UserPermission'))
BEGIN
    RAISERROR('SM_Sakura_UserPermission chưa tồn tại — chạy SM_UserPermission_RenameToSakuraAndAddRole.sql trước.', 16, 1);
    RETURN;
END
GO

-- 1) Gỡ CHECK constraint cũ (nếu có) để UPDATE không bị chặn.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SM_Sakura_UserPermission_Role')
BEGIN
    ALTER TABLE dbo.SM_Sakura_UserPermission DROP CONSTRAINT CK_SM_Sakura_UserPermission_Role;
END
GO

-- 2) Đổi dữ liệu.
UPDATE dbo.SM_Sakura_UserPermission SET Role = N'LineLeader' WHERE Role = N'Supervisor';
GO

-- 3) Thêm lại CHECK constraint với tên role mới.
ALTER TABLE dbo.SM_Sakura_UserPermission
    ADD CONSTRAINT CK_SM_Sakura_UserPermission_Role CHECK (Role IN (N'Worker', N'LineLeader', N'Admin'));
GO
