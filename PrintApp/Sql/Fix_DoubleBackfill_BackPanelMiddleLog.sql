-- ============================================================================
-- Fix_DoubleBackfill_BackPanelMiddleLog.sql
--
-- Backfill_TimezoneToChina_UTC8.sql lỡ bị chạy 2 LẦN trên TOÀN BỘ các bảng -> mọi bảng đang bị
-- cộng nhầm +2 tiếng thay vì +1 tiếng. Script này trừ lại đúng 1 tiếng trên TỪNG bảng/cột y hệt
-- danh sách trong Backfill_TimezoneToChina_UTC8.sql, để đưa tất cả về đúng giờ Trung Quốc
-- (UTC+8) — chỉ lệch +1h so với giờ Việt Nam gốc, không phải +2h.
--
-- ⚠️ CHỈ chạy đúng 1 lần (đúng 1 lần cho đúng số lần bảng đó đã bị chạy dư ra).
-- ⚠️ Nếu 1 bảng nào đó KHÔNG bị chạy dư (chỉ đúng 1 lần từ đầu), ĐỪNG chạy phần tương ứng của
--    bảng đó trong script này — sẽ trừ nhầm 1 tiếng vào dữ liệu đã đúng.
--
-- Run against svn_pentaho.
-- ============================================================================

-- ── SM_SNLabelScanLog.Timeline ──────────────────────────────────────────────
IF OBJECT_ID('dbo.SM_SNLabelScanLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_SNLabelScanLog SET Timeline = DATEADD(HOUR, -1, Timeline);
    PRINT CONCAT('SM_SNLabelScanLog: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_BackPanelLaserLog.Timeline ───────────────────────────────────────────
IF OBJECT_ID('dbo.SM_BackPanelLaserLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_BackPanelLaserLog SET Timeline = DATEADD(HOUR, -1, Timeline);
    PRINT CONCAT('SM_BackPanelLaserLog: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_MiddleLog.Timeline ───────────────────────────────────────────────────
IF OBJECT_ID('dbo.SM_MiddleLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_MiddleLog SET Timeline = DATEADD(HOUR, -1, Timeline);
    PRINT CONCAT('SM_MiddleLog: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_LabelPvidEANLog.Timeline ──────────────────────────────────────────────
IF OBJECT_ID('dbo.SM_LabelPvidEANLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_LabelPvidEANLog SET Timeline = DATEADD(HOUR, -1, Timeline);
    PRINT CONCAT('SM_LabelPvidEANLog: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_SNLabelPrint: PrintedAt, ProductionDate, LastReprintedAt (nullable) ──────────────────
IF OBJECT_ID('dbo.SM_SNLabelPrint') IS NOT NULL
BEGIN
    UPDATE dbo.SM_SNLabelPrint
    SET PrintedAt = DATEADD(HOUR, -1, PrintedAt),
        ProductionDate = DATEADD(HOUR, -1, ProductionDate),
        LastReprintedAt = CASE WHEN LastReprintedAt IS NULL THEN NULL ELSE DATEADD(HOUR, -1, LastReprintedAt) END;
    PRINT CONCAT('SM_SNLabelPrint: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_Sakura_CartonLabel_Data: ScanDate, LastReprintAt, LastPalletReprintAt (nullable) ─────
IF OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') IS NOT NULL
BEGIN
    UPDATE dbo.SM_Sakura_CartonLabel_Data
    SET ScanDate = DATEADD(HOUR, -1, ScanDate),
        LastReprintAt = CASE WHEN LastReprintAt IS NULL THEN NULL ELSE DATEADD(HOUR, -1, LastReprintAt) END,
        LastPalletReprintAt = CASE WHEN LastPalletReprintAt IS NULL THEN NULL ELSE DATEADD(HOUR, -1, LastPalletReprintAt) END;
    PRINT CONCAT('SM_Sakura_CartonLabel_Data: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_Sakura_PalletInfoTemplate.UpdatedAt ──────────────────────────────────
IF OBJECT_ID('dbo.SM_Sakura_PalletInfoTemplate') IS NOT NULL
BEGIN
    UPDATE dbo.SM_Sakura_PalletInfoTemplate SET UpdatedAt = DATEADD(HOUR, -1, UpdatedAt);
    PRINT CONCAT('SM_Sakura_PalletInfoTemplate: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_Sakura_ZplTemplate.UpdatedAt ──────────────────────────────────────────
IF OBJECT_ID('dbo.SM_Sakura_ZplTemplate') IS NOT NULL
BEGIN
    UPDATE dbo.SM_Sakura_ZplTemplate SET UpdatedAt = DATEADD(HOUR, -1, UpdatedAt);
    PRINT CONCAT('SM_Sakura_ZplTemplate: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SVN_Toast_Serial_Info: FCTStatusDatetime, FQCStatusDatetime (nullable) ──────────────────
IF OBJECT_ID('dbo.SVN_Toast_Serial_Info') IS NOT NULL
BEGIN
    UPDATE dbo.SVN_Toast_Serial_Info
    SET FCTStatusDatetime = CASE WHEN FCTStatusDatetime IS NULL THEN NULL ELSE DATEADD(HOUR, -1, FCTStatusDatetime) END,
        FQCStatusDatetime = CASE WHEN FQCStatusDatetime IS NULL THEN NULL ELSE DATEADD(HOUR, -1, FQCStatusDatetime) END;
    PRINT CONCAT('SVN_Toast_Serial_Info: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO

-- ── SM_ExternalPrint_Queue: CreatedAt, ClaimedAt (nullable), CompletedAt (nullable) ─────────
IF OBJECT_ID('dbo.SM_ExternalPrint_Queue') IS NOT NULL
BEGIN
    UPDATE dbo.SM_ExternalPrint_Queue
    SET CreatedAt = DATEADD(HOUR, -1, CreatedAt),
        ClaimedAt = CASE WHEN ClaimedAt IS NULL THEN NULL ELSE DATEADD(HOUR, -1, ClaimedAt) END,
        CompletedAt = CASE WHEN CompletedAt IS NULL THEN NULL ELSE DATEADD(HOUR, -1, CompletedAt) END;
    PRINT CONCAT('SM_ExternalPrint_Queue: ', @@ROWCOUNT, ' rows corrected (-1 hour).');
END
GO
