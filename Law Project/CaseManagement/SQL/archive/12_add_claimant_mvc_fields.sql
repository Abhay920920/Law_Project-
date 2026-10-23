-- Add Claimant Division and MVC fields to APPEAL_DETAILS table
-- Run this script to update the database schema

GO

-- Add new columns for Claimant Appeal details
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantDivisionID')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS]
    ADD ClaimantDivisionID INT NULL;
    
    PRINT 'Added ClaimantDivisionID column';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantMVCNumber')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS]
    ADD ClaimantMVCNumber NVARCHAR(50) NULL;
    
    PRINT 'Added ClaimantMVCNumber column';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantMVCYear')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS]
    ADD ClaimantMVCYear INT NULL;
    
    PRINT 'Added ClaimantMVCYear column';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[APPEAL_DETAILS]') AND name = 'ClaimantMVCCurrentStatus')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS]
    ADD ClaimantMVCCurrentStatus NVARCHAR(100) NULL;
    
    PRINT 'Added ClaimantMVCCurrentStatus column';
END
GO

-- Add foreign key constraint for ClaimantDivisionID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_APPEAL_DETAILS_ClaimantDivisionID')
BEGIN
    ALTER TABLE [dbo].[APPEAL_DETAILS]
    ADD CONSTRAINT FK_APPEAL_DETAILS_ClaimantDivisionID 
    FOREIGN KEY (ClaimantDivisionID) REFERENCES DIVISION_MASTER(DivisionID);
    
    PRINT 'Added foreign key constraint for ClaimantDivisionID';
END
GO

PRINT 'Schema update completed successfully!';
