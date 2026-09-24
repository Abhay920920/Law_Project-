-- Add Interest Payment columns to GRA_CASES table and create GRA_INTEREST_PAYMENTS table
-- Date: 2026-02-09
-- Description: Adding Interest Payment feature with multiple cheques support

-- Add IsInterestPayable column
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'GRA_CASES' 
    AND COLUMN_NAME = 'IsInterestPayable'
)
BEGIN
    ALTER TABLE GRA_CASES 
    ADD IsInterestPayable BIT NOT NULL DEFAULT 0;
    
    PRINT 'Column IsInterestPayable added to GRA_CASES table successfully.';
END
ELSE
BEGIN
    PRINT 'Column IsInterestPayable already exists in GRA_CASES table.';
END

-- Add InterestRate column
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'GRA_CASES' 
    AND COLUMN_NAME = 'InterestRate'
)
BEGIN
    ALTER TABLE GRA_CASES 
    ADD InterestRate DECIMAL(5,2) NULL;
    
    PRINT 'Column InterestRate added to GRA_CASES table successfully.';
END
ELSE
BEGIN
    PRINT 'Column InterestRate already exists in GRA_CASES table.';
END

-- Add InterestRemarks column (if not already added)
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'GRA_CASES' 
    AND COLUMN_NAME = 'InterestRemarks'
)
BEGIN
    ALTER TABLE GRA_CASES 
    ADD InterestRemarks NVARCHAR(500) NULL;
    
    PRINT 'Column InterestRemarks added to GRA_CASES table successfully.';
END
ELSE
BEGIN
    PRINT 'Column InterestRemarks already exists in GRA_CASES table.';
END

-- Create GRA_INTEREST_PAYMENTS table for multiple cheques
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GRA_INTEREST_PAYMENTS')
BEGIN
    CREATE TABLE GRA_INTEREST_PAYMENTS (
        InterestPaymentID INT IDENTITY(1,1) PRIMARY KEY,
        CaseID INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
        ChequeNumber NVARCHAR(50) NULL,
        ChequeDate DATE NULL,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_GRA_INTEREST_PAYMENTS_CaseID FOREIGN KEY (CaseID) REFERENCES GRA_CASES(CaseID) ON DELETE CASCADE
    );
    
    CREATE INDEX IX_GRA_INTEREST_PAYMENTS_CaseID ON GRA_INTEREST_PAYMENTS(CaseID);
    
    PRINT 'Table GRA_INTEREST_PAYMENTS created successfully.';
END
ELSE
BEGIN
    PRINT 'Table GRA_INTEREST_PAYMENTS already exists.';
END
