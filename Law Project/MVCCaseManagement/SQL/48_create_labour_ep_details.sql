CREATE TABLE [admin_Law].[LABOUR_EP_DETAILS] (
    [EPID] INT IDENTITY(1,1) PRIMARY KEY,
    [CaseID] INT NOT NULL,
    [DivisionID] INT NOT NULL DEFAULT(0),
    [EPNumber] NVARCHAR(50) NULL,
    [EPYear] INT NULL,
    [EPCourt] NVARCHAR(200) NULL,
    
    [ArisingFromCaseNo] NVARCHAR(100) NULL,
    [ArisingFromCaseYear] INT NULL,
    [ArisingFromCourt] NVARCHAR(200) NULL,
    
    [OriginalCaseStatus] NVARCHAR(50) NULL,
    [EPStatus] NVARCHAR(50) NULL,
    [NextHearingDate] DATE NULL,
    [AsPerECourts] NVARCHAR(200) NULL,
    
    [IsSentToAccounts] BIT NOT NULL DEFAULT 0,
    [DateSentToAccounts] DATE NULL,
    
    [AwardAmount] DECIMAL(18,2) NULL,
    [InterestRate] DECIMAL(5,2) NULL,
    [LiabilityPercentage] DECIMAL(5,2) NULL,
    [PetitionDate] DATE NULL,
    
    [PettyBillDate] DATE NULL,
    [PettyBillAmount] DECIMAL(18,2) NULL,
    [ChequeNumber] NVARCHAR(50) NULL,
    [ChequeDate] DATE NULL,
    [AmountPaid] DECIMAL(18,2) NULL,
    
    [ComplianceStatus] NVARCHAR(50) NULL,
    [DateOfCompliance] DATE NULL,
    [Remarks] NVARCHAR(MAX) NULL,
    
    [CreatedBy] INT NULL,
    [CreatedDate] DATETIME DEFAULT GETDATE(),
    [ModifiedBy] INT NULL,
    [ModifiedDate] DATETIME NULL
);
GO

CREATE TABLE [admin_Law].[LABOUR_EP_PAYMENTS] (
    [PaymentID] INT IDENTITY(1,1) PRIMARY KEY,
    [EPID] INT NOT NULL FOREIGN KEY REFERENCES [admin_Law].[LABOUR_EP_DETAILS](EPID),
    [Amount] DECIMAL(18,2) NOT NULL,
    [PaymentDate] DATE NOT NULL,
    [ChequeNumber] NVARCHAR(50) NULL,
    [ChequeDate] DATE NULL,
    [Remarks] NVARCHAR(MAX) NULL
);
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[admin_Law].[LABOUR_CASES]') AND name = 'IsEPFiled')
BEGIN
    ALTER TABLE [admin_Law].[LABOUR_CASES] ADD [IsEPFiled] BIT NOT NULL DEFAULT 0;
END
GO
