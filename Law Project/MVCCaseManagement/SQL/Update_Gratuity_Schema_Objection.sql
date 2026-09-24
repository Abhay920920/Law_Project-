
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'ObjectionOutwardNo')
BEGIN
    ALTER TABLE [dbo].[GRA_CASES] ADD [ObjectionOutwardNo] NVARCHAR(50) NULL;
    PRINT 'Column ObjectionOutwardNo added to GRA_CASES table.';
END
ELSE
BEGIN
    PRINT 'Column ObjectionOutwardNo already exists in GRA_CASES table.';
END

-- Update Forwarding Status for consistency with new UI options
UPDATE [dbo].[GRA_CASES]
SET [ForwardingStatus] = 'Appeal Before Competent Authority'
WHERE [ForwardingStatus] = 'Appeal';

PRINT 'Updated ForwardingStatus values from "Appeal" to "Appeal Before Competent Authority".';

-- Verify schema
SELECT TOP 1 * FROM [dbo].[GRA_CASES];
