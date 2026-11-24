SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO

/* Existing FineAmount values are overdue fines; preserve them separately before new returns start recording compensation. */
IF COL_LENGTH(N'dbo.Returns', N'OverdueFine') IS NULL
    ALTER TABLE dbo.Returns ADD OverdueFine decimal(12,2) NULL;
GO
IF COL_LENGTH(N'dbo.Returns', N'CompensationAmount') IS NULL
    ALTER TABLE dbo.Returns ADD CompensationAmount decimal(12,2) NULL;
GO

UPDATE dbo.Returns
SET OverdueFine = FineAmount,
    CompensationAmount = 0
WHERE OverdueFine IS NULL OR CompensationAmount IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Returns') AND name=N'OverdueFine' AND is_nullable=1)
    ALTER TABLE dbo.Returns ALTER COLUMN OverdueFine decimal(12,2) NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Returns') AND name=N'CompensationAmount' AND is_nullable=1)
    ALTER TABLE dbo.Returns ALTER COLUMN CompensationAmount decimal(12,2) NOT NULL;
GO
