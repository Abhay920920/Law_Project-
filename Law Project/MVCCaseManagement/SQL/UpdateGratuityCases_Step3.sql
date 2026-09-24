USE MVCCaseDB;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'AppointmentDate_CA')
BEGIN
    ALTER TABLE GRA_CASES ADD AppointmentDate_CA DATE NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GRA_CASES]') AND name = 'RetirementDate_CA')
BEGIN
    ALTER TABLE GRA_CASES ADD RetirementDate_CA DATE NULL;
END

PRINT 'GRA_CASES table updated with ALC Appointment/Retirement columns.';
