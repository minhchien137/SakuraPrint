-- SM_CartonUnpack_Log — audit trail của trạm Unpack/Repack (/sakura/rework): mỗi lần gỡ 1
-- Serial ra khỏi 1 Carton Number để rework insert 1 dòng MỚI (Status=UNPACKED). Dòng đó được
-- UPDATE tại chỗ (không insert dòng mới) khi serial qua bước Rework (Status=REWORKED, gắn
-- ReworkWorkOrder) rồi khi carton được đóng lại/in lại tem (Status=REPACKED) — mỗi dòng theo
-- dõi trọn vòng đời 1 lần rework của 1 serial. Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SM_CartonUnpack_Log'))
BEGIN
    CREATE TABLE dbo.SM_CartonUnpack_Log (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        CartonNumber    NVARCHAR(30) NOT NULL,
        SerialNumber    NVARCHAR(20) NOT NULL,
        WorkOrder       NVARCHAR(50) NOT NULL,  -- WO gốc của carton lúc unpack
        ReworkWorkOrder NVARCHAR(50) NULL,      -- gắn lúc serial qua bước Rework (SN Label reprint)
        Status          NVARCHAR(10) NOT NULL,  -- UNPACKED / REWORKED / REPACKED
        UnpackedAt      DATETIME NOT NULL,
        ReworkedAt      DATETIME NULL,
        RepackedAt      DATETIME NULL
    );

    CREATE INDEX IX_SM_CartonUnpack_Log_CartonNumber ON dbo.SM_CartonUnpack_Log (CartonNumber);
    CREATE INDEX IX_SM_CartonUnpack_Log_SerialNumber ON dbo.SM_CartonUnpack_Log (SerialNumber);
END
GO
