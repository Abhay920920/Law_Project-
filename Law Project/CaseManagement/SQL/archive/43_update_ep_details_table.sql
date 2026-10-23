-- Add missing columns to MVC_EP_DETAILS table (using admin_Law schema)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[admin_Law].[MVC_EP_DETAILS]') AND name = 'NextHearingDate')
BEGIN
    ALTER TABLE [admin_Law].[MVC_EP_DETAILS] ADD [NextHearingDate] DATE NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[admin_Law].[MVC_EP_DETAILS]') AND name = 'VehicleNo')
BEGIN
    ALTER TABLE [admin_Law].[MVC_EP_DETAILS] ADD [VehicleNo] NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[admin_Law].[MVC_EP_DETAILS]') AND name = 'AccidentDate')
BEGIN
    ALTER TABLE [admin_Law].[MVC_EP_DETAILS] ADD [AccidentDate] DATE NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[admin_Law].[MVC_EP_DETAILS]') AND name = 'EPYear')
BEGIN
    ALTER TABLE [admin_Law].[MVC_EP_DETAILS] ADD [EPYear] INT NULL;
END
GO
