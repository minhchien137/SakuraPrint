-- ============================================================================
-- Backfill_TimezoneToChina_UTC8.sql
--
-- MỤC ĐÍCH: mọi timestamp DO ỨNG DỤNG TỰ GHI (không phải người dùng chọn tay) trong hệ thống
-- giờ được lưu theo giờ Trung Quốc (UTC+8) thay vì giờ Việt Nam (UTC+7) — xem VietnamNow()
-- .AddHours(1) rải khắp SakuraController.cs / SakuraService.cs / BackPanelController.cs /
-- MiddleController.cs / ExternalPrintController.cs / ToastSerialService.cs.
--
-- Script này CỘNG THÊM 1 TIẾNG vào TOÀN BỘ dữ liệu LỊCH SỬ đã lưu TRƯỚC khi đổi sang giờ
-- Trung Quốc, để khớp với dữ liệu MỚI (đã là giờ Trung Quốc) — tránh 1 điểm "nhảy" 1 tiếng
-- giữa dữ liệu cũ/mới khi xem History.
--
-- ⚠️ CHỈ CHẠY ĐÚNG 1 LẦN, VÀ CHỈ CHẠY TRƯỚC KHI RESTART APP (tức là lúc DB vẫn còn 100% dữ
-- liệu giờ Việt Nam, chưa có dòng nào mới ghi giờ Trung Quốc). Nếu app đã restart và đã có dữ
-- liệu mới xen lẫn, script này sẽ cộng nhầm thêm 1 tiếng vào cả những dòng đã đúng rồi.
-- ⚠️ NÊN BACKUP DATABASE (hoặc ít nhất backup từng bảng bên dưới ra bảng tạm) TRƯỚC KHI CHẠY.
-- ⚠️ Mỗi bảng chạy trong 1 batch riêng (ngăn bởi GO) — 1 bảng lỗi không rollback các bảng khác.
--
-- Run against svn_pentaho.
-- ============================================================================

-- ── SM_SNLabelScanLog.Timeline ──────────────────────────────────────────────
IF OBJECT_ID('dbo.SM_SNLabelScanLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_SNLabelScanLog SET Timeline = DATEADD(HOUR, 1, Timeline);
    PRINT CONCAT('SM_SNLabelScanLog: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_BackPanelLaserLog.Timeline ───────────────────────────────────────────
IF OBJECT_ID('dbo.SM_BackPanelLaserLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_BackPanelLaserLog SET Timeline = DATEADD(HOUR, 1, Timeline);
    PRINT CONCAT('SM_BackPanelLaserLog: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_MiddleLog.Timeline ───────────────────────────────────────────────────
IF OBJECT_ID('dbo.SM_MiddleLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_MiddleLog SET Timeline = DATEADD(HOUR, 1, Timeline);
    PRINT CONCAT('SM_MiddleLog: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_LabelPvidEANLog.Timeline (bảng mới — thường chưa có dữ liệu cũ, chạy cho chắc) ───────
IF OBJECT_ID('dbo.SM_LabelPvidEANLog') IS NOT NULL
BEGIN
    UPDATE dbo.SM_LabelPvidEANLog SET Timeline = DATEADD(HOUR, 1, Timeline);
    PRINT CONCAT('SM_LabelPvidEANLog: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_SNLabelPrint: PrintedAt (hiển thị), LastReprintedAt (nullable), ProductionDate ───────
-- ProductionDate cũng được đổi theo yêu cầu (chấp nhận rủi ro tem in lúc 23h-23h59 VN bị tính
-- nhầm sang lô serial của ngày hôm sau — xem trao đổi trước đó).
IF OBJECT_ID('dbo.SM_SNLabelPrint') IS NOT NULL
BEGIN
    UPDATE dbo.SM_SNLabelPrint
    SET PrintedAt = DATEADD(HOUR, 1, PrintedAt),
        ProductionDate = DATEADD(HOUR, 1, ProductionDate),
        LastReprintedAt = CASE WHEN LastReprintedAt IS NULL THEN NULL ELSE DATEADD(HOUR, 1, LastReprintedAt) END;
    PRINT CONCAT('SM_SNLabelPrint: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_Sakura_CartonLabel_Data: ScanDate, LastReprintAt, LastPalletReprintAt (nullable) ─────
IF OBJECT_ID('dbo.SM_Sakura_CartonLabel_Data') IS NOT NULL
BEGIN
    UPDATE dbo.SM_Sakura_CartonLabel_Data
    SET ScanDate = DATEADD(HOUR, 1, ScanDate),
        LastReprintAt = CASE WHEN LastReprintAt IS NULL THEN NULL ELSE DATEADD(HOUR, 1, LastReprintAt) END,
        LastPalletReprintAt = CASE WHEN LastPalletReprintAt IS NULL THEN NULL ELSE DATEADD(HOUR, 1, LastPalletReprintAt) END;
    PRINT CONCAT('SM_Sakura_CartonLabel_Data: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_Sakura_PalletInfoTemplate.UpdatedAt ──────────────────────────────────
IF OBJECT_ID('dbo.SM_Sakura_PalletInfoTemplate') IS NOT NULL
BEGIN
    UPDATE dbo.SM_Sakura_PalletInfoTemplate SET UpdatedAt = DATEADD(HOUR, 1, UpdatedAt);
    PRINT CONCAT('SM_Sakura_PalletInfoTemplate: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_Sakura_ZplTemplate.UpdatedAt ──────────────────────────────────────────
IF OBJECT_ID('dbo.SM_Sakura_ZplTemplate') IS NOT NULL
BEGIN
    UPDATE dbo.SM_Sakura_ZplTemplate SET UpdatedAt = DATEADD(HOUR, 1, UpdatedAt);
    PRINT CONCAT('SM_Sakura_ZplTemplate: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SVN_Toast_Serial_Info: FCTStatusDatetime, FQCStatusDatetime (nullable) ──────────────────
IF OBJECT_ID('dbo.SVN_Toast_Serial_Info') IS NOT NULL
BEGIN
    UPDATE dbo.SVN_Toast_Serial_Info
    SET FCTStatusDatetime = CASE WHEN FCTStatusDatetime IS NULL THEN NULL ELSE DATEADD(HOUR, 1, FCTStatusDatetime) END,
        FQCStatusDatetime = CASE WHEN FQCStatusDatetime IS NULL THEN NULL ELSE DATEADD(HOUR, 1, FQCStatusDatetime) END;
    PRINT CONCAT('SVN_Toast_Serial_Info: ', @@ROWCOUNT, ' rows updated.');
END
GO

-- ── SM_ExternalPrint_Queue: CreatedAt, ClaimedAt (nullable), CompletedAt (nullable) ─────────
IF OBJECT_ID('dbo.SM_ExternalPrint_Queue') IS NOT NULL
BEGIN
    UPDATE dbo.SM_ExternalPrint_Queue
    SET CreatedAt = DATEADD(HOUR, 1, CreatedAt),
        ClaimedAt = CASE WHEN ClaimedAt IS NULL THEN NULL ELSE DATEADD(HOUR, 1, ClaimedAt) END,
        CompletedAt = CASE WHEN CompletedAt IS NULL THEN NULL ELSE DATEADD(HOUR, 1, CompletedAt) END;
    PRINT CONCAT('SM_ExternalPrint_Queue: ', @@ROWCOUNT, ' rows updated.');
END
GO
