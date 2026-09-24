-- Run these commands in your SQL Server to update the Petty Bill tables
-- Add missing columns to PETTY_BILLS
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILLS]') AND name = 'AwardType')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILLS] ADD [AwardType] NVARCHAR(50) NULL DEFAULT 'Lower Court';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILLS]') AND name = 'BaseAwardAmount')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILLS] ADD [BaseAwardAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
END

-- Add missing columns to PETTY_BILL_PAYMENTS
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILL_PAYMENTS]') AND name = 'IsCancelled')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILL_PAYMENTS] ADD [IsCancelled] BIT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PETTY_BILL_PAYMENTS]') AND name = 'Remarks')
BEGIN
    ALTER TABLE [dbo].[PETTY_BILL_PAYMENTS] ADD [Remarks] NVARCHAR(200) NULL;
END
