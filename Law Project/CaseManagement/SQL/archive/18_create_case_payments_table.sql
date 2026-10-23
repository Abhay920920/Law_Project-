-- =============================================
-- Create Case Payments Table
-- Purpose: Store advance payment records for MVC cases
-- =============================================

-- Create MVC_CASE_PAYMENTS table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MVC_CASE_PAYMENTS]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MVC_CASE_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) PRIMARY KEY,
        [CaseID] INT NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATE NULL,
        [PaymentType] NVARCHAR(50) NULL, -- 'Interim', 'Advance', 'Partial Settlement'
        [Remarks] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        CONSTRAINT FK_CasePayments_Case FOREIGN KEY ([CaseID]) 
            REFERENCES [dbo].[MVC_CASES]([CaseID]) ON DELETE CASCADE
    );
    
    PRINT 'Table MVC_CASE_PAYMENTS created successfully.';
END
ELSE
BEGIN
    PRINT 'Table MVC_CASE_PAYMENTS already exists.';
END
GO

-- Create index for faster lookups by CaseID
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CasePayments_CaseID' AND object_id = OBJECT_ID('MVC_CASE_PAYMENTS'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CasePayments_CaseID
    ON [dbo].[MVC_CASE_PAYMENTS] ([CaseID])
    INCLUDE ([Amount], [CreatedDate]);
    
    PRINT 'Index IX_CasePayments_CaseID created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_CasePayments_CaseID already exists.';
END
GO

PRINT 'Case payments table setup completed successfully.';
