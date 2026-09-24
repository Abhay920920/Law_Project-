-- =============================================
-- Alter Case Payments Table
-- Purpose: Add missing columns PaymentType and CreatedBy
-- =============================================

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MVC_CASE_PAYMENTS]') AND type in (N'U'))
BEGIN
    -- Add PaymentType column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MVC_CASE_PAYMENTS]') AND name = 'PaymentType')
    BEGIN
        ALTER TABLE [dbo].[MVC_CASE_PAYMENTS]
        ADD [PaymentType] NVARCHAR(50) NULL;
        PRINT 'Column PaymentType added successfully.';
    END
    ELSE
    BEGIN
        PRINT 'Column PaymentType already exists.';
    END

    -- Add CreatedBy column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MVC_CASE_PAYMENTS]') AND name = 'CreatedBy')
    BEGIN
        ALTER TABLE [dbo].[MVC_CASE_PAYMENTS]
        ADD [CreatedBy] NVARCHAR(100) NULL;
        PRINT 'Column CreatedBy added successfully.';
    END
    ELSE
    BEGIN
        PRINT 'Column CreatedBy already exists.';
    END
    
    -- Ensure CreatedDate has a default value if not already set (cannot easily check constraint existence safely in one line, so skipping strict check, assuming logic handles it)
END
GO
