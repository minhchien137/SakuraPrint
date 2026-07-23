-- SM_Sakura_CartonLabel_Data — 1 dòng cho mỗi CARTON đã IN THÀNH CÔNG (không phải 1 dòng/serial).
-- Serial chứa TOÀN BỘ serial trên carton đó, nối bằng dấu phẩy (VD "RM15A...00,RM15A...01,...");
-- CountSerial là số lượng serial trong chuỗi đó (10 nếu đủ hộp, hoặc phần dư nếu lẻ hộp).
-- Dùng để chặn 1 serial bị in trùng vào 2 carton khác nhau + tính số lượng đã in/còn lại
-- của 1 Work Order (đủ hộp vs lẻ hộp). Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data'))
BEGIN
    CREATE TABLE dbo.SM_Sakura_CartonLabel_Data (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        Serial          NVARCHAR(400) NOT NULL, -- toàn bộ serial của carton này, nối bằng dấu phẩy
        ScanDate        DATETIME NOT NULL,
        CountSerial     INT NOT NULL, -- số lượng serial trong cột Serial ở trên (10, hoặc phần dư nếu lẻ hộp)
        WorkOrder       NVARCHAR(50) NOT NULL,
        CartonNumber    NVARCHAR(30) NOT NULL,
        Color           NVARCHAR(20) NULL,
        Condition       NVARCHAR(10) NULL
    );

    CREATE INDEX IX_SM_Sakura_CartonLabel_Data_WorkOrder ON dbo.SM_Sakura_CartonLabel_Data (WorkOrder);
END
GO

-- Bảng đã được tạo TỪ TRƯỚC KHI đổi sang lưu 1 dòng/carton (Serial dạng CSV) sẽ vẫn còn
-- Serial NVARCHAR(20) + unique index cũ — insert 1 chuỗi CSV dài (~170 ký tự) vào đó sẽ lỗi
-- "String or binary data would be truncated". Chạy lại file này để tự nới cột + bỏ index cũ.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'Serial' AND max_length / 2 < 400
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SM_Sakura_CartonLabel_Data_Serial' AND object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data'))
        DROP INDEX IX_SM_Sakura_CartonLabel_Data_Serial ON dbo.SM_Sakura_CartonLabel_Data;

    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ALTER COLUMN Serial NVARCHAR(400) NOT NULL;
END
GO

-- ── Print Pallet (Main Pallet Label) — gom nhiều carton đã in vào 1 Pallet ID, sinh Pallet
-- Number tự động lúc "chốt"/in tem. 3 cột mới đều NULL, không backfill dữ liệu cũ.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'PalletId')
    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ADD PalletId NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'PalletNumber')
    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ADD PalletNumber NVARCHAR(30) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND name = 'Date')
    ALTER TABLE dbo.SM_Sakura_CartonLabel_Data ADD [Date] INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SM_Sakura_CartonLabel_Data_PalletId' AND object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data'))
    CREATE INDEX IX_SM_Sakura_CartonLabel_Data_PalletId ON dbo.SM_Sakura_CartonLabel_Data (PalletId);
GO

-- Lúc đầu tạo UNIQUE index ở đây để bắt race-condition khi 2 request cùng sinh trùng 1 Pallet
-- Number (mỗi dòng = 1 pallet). Nhưng từ khi 1 Pallet Number được gán cho NHIỀU carton của cùng
-- 1 pallet (mỗi carton 1 dòng riêng, tất cả dùng CHUNG 1 Pallet Number — xem
-- SakuraService.RecordCartonScanAsync/ScanCartonIntoPalletAsync) thì UNIQUE khiến carton thứ 2
-- trở đi của cùng pallet KHÔNG LƯU ĐƯỢC ("Cannot insert duplicate key" — 500, mất dữ liệu carton
-- đó dù ZPL vẫn in/preview bình thường). Đổi lại thành index thường (không unique) — an toàn
-- concurrency lúc SINH SỐ MỚI đã dựa vào transaction Serializable + retry (xem
-- GenerateAndAssignPalletNumberAsync), không cần unique index làm lớp chặn nữa.
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_SM_Sakura_CartonLabel_Data_PalletNumber' AND object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') AND is_unique = 1
)
    DROP INDEX IX_SM_Sakura_CartonLabel_Data_PalletNumber ON dbo.SM_Sakura_CartonLabel_Data;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SM_Sakura_CartonLabel_Data_PalletNumber' AND object_id = OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data'))
    CREATE INDEX IX_SM_Sakura_CartonLabel_Data_PalletNumber ON dbo.SM_Sakura_CartonLabel_Data (PalletNumber) WHERE PalletNumber IS NOT NULL;
GO
