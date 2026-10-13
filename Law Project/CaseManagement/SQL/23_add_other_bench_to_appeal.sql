IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE Name = N'OtherHighCourtBench' AND Object_ID = Object_ID(N'APPEAL_DETAILS')
)
BEGIN
    ALTER TABLE APPEAL_DETAILS
    ADD OtherHighCourtBench NVARCHAR(100) NULL;
END
GO
