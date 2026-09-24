-- Force add columns
ALTER TABLE [dbo].[MVC_CASE_PAYMENTS] ADD [PaymentType] NVARCHAR(50) NULL;
GO
ALTER TABLE [dbo].[MVC_CASE_PAYMENTS] ADD [CreatedBy] NVARCHAR(100) NULL;
GO
PRINT 'Columns added.';
