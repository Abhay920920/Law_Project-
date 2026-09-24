IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE Name = N'OtherHighCourtBench' AND Object_ID = Object_ID(N'APPEAL_DETAILS')
)
BEGIN
    SELECT 'MISSING' as Status;
END
ELSE
BEGIN
    SELECT 'EXISTS' as Status;
END
