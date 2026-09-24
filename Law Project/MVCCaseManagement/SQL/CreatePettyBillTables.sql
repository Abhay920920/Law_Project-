-- =============================================
-- Create Petty Bill Tables
-- Purpose: Store petty bill data for updates and regeneration
-- =============================================

-- Main Petty Bills Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILLS]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PETTY_BILLS] (
        [BillID] INT IDENTITY(1,1) PRIMARY KEY,
        [CaseID] INT NULL, -- FK to MVC_CASES (nullable for flexibility)
        [MVCNo] NVARCHAR(50) NULL,
        [MVCYear] INT NULL,
        [MACTName] NVARCHAR(200) NULL,
        [VehicleNo] NVARCHAR(50) NULL,
        [AccidentDate] DATE NULL,
        [PetitionerName] NVARCHAR(200) NULL,
        [AppealNumber] NVARCHAR(100) NULL,
        [DivisionName] NVARCHAR(100) NULL,
        [AwardType] NVARCHAR(50) NULL DEFAULT 'Lower Court', -- NEW: Lower Court, High Court (Enhancement), High Court (Reduction)
        [BaseAwardAmount] DECIMAL(18,2) NOT NULL DEFAULT 0, -- NEW: The original award amount from lower court
        [EnhancedAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CourtCost] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TDSPercentage] DECIMAL(5,2) NOT NULL DEFAULT 10,
        [TDSAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [FileRefNo] NVARCHAR(100) NULL,
        [ApprovalLetterNo] NVARCHAR(100) NULL,
        [ApprovalDate] DATE NULL,
        [BillDate] DATE NULL,
        [NetPayable] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [GrossPayable] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TotalDeductions] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NOT NULL DEFAULT GETDATE()
    );
    
    PRINT 'Table PETTY_BILLS created successfully.';
END
ELSE
BEGIN
    PRINT 'Table PETTY_BILLS already exists.';
END
GO

-- Petty Bill Payments Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILL_PAYMENTS]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PETTY_BILL_PAYMENTS] (
        [PaymentID] INT IDENTITY(1,1) PRIMARY KEY,
        [BillID] INT NOT NULL,
        [ChequeNumber] NVARCHAR(100) NULL,
        [ChequeDate] DATE NULL,
        [Amount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [IsSelected] BIT NOT NULL DEFAULT 1,
        [IsCancelled] BIT NOT NULL DEFAULT 0, -- NEW: Track cancelled cheques/payments
        [Remarks] NVARCHAR(200) NULL,
        CONSTRAINT FK_PettyBillPayments_Bill FOREIGN KEY ([BillID]) 
            REFERENCES [dbo].[PETTY_BILLS]([BillID]) ON DELETE CASCADE
    );
    
    PRINT 'Table PETTY_BILL_PAYMENTS created successfully.';
END
ELSE
BEGIN
    PRINT 'Table PETTY_BILL_PAYMENTS already exists.';
END
GO

-- Petty Bill Interest Entries Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILL_INTEREST]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PETTY_BILL_INTEREST] (
        [InterestID] INT IDENTITY(1,1) PRIMARY KEY,
        [BillID] INT NOT NULL,
        [FromDate] DATE NOT NULL,
        [ToDate] DATE NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_PettyBillInterest_Bill FOREIGN KEY ([BillID]) 
            REFERENCES [dbo].[PETTY_BILLS]([BillID]) ON DELETE CASCADE
    );
    
    PRINT 'Table PETTY_BILL_INTEREST created successfully.';
END
ELSE
BEGIN
    PRINT 'Table PETTY_BILL_INTEREST already exists.';
END
GO

PRINT 'All Petty Bill tables created/verified successfully.';
