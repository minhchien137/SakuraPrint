-- SM_SNLabelPrint — Reprint by Serial tracking columns (Sakura project)
-- Run against svn_pentaho.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'ReprintCount')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD ReprintCount INT NOT NULL DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'LastReprintedAt')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD LastReprintedAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'LastReprintedBy')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD LastReprintedBy NVARCHAR(100) NULL;
END
GO

-- Quét EAN + Serial (Process: Check EAN -> Check Color & Serial Number -> Print Label)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'Ean')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD Ean NVARCHAR(30) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'Status')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD Status NVARCHAR(10) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SM_SNLabelPrint') AND name = 'FailedStep')
BEGIN
    ALTER TABLE dbo.SM_SNLabelPrint ADD FailedStep INT NULL;
END
GO
