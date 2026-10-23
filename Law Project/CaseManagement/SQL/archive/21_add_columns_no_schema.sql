-- Add columns to the table in the default schema (likely admin_Law)
ALTER TABLE MVC_CASE_PAYMENTS ADD [PaymentType] NVARCHAR(50) NULL;
GO
ALTER TABLE MVC_CASE_PAYMENTS ADD [CreatedBy] NVARCHAR(100) NULL;
GO
PRINT 'Columns added to default schema table.';
