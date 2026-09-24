IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HIGH_COURT_ADVOCATES]') AND type in (N'U'))
BEGIN
    CREATE TABLE HIGH_COURT_ADVOCATES (
        AdvocateID INT IDENTITY(1,1) PRIMARY KEY,
        AdvocateName NVARCHAR(200) NOT NULL,
        Bench NVARCHAR(50) NOT NULL,
        IsActive BIT DEFAULT 1
    );
END
GO

-- Clear existing data if re-running (optional, but safe for idempotent script)
-- TRUNCATE TABLE HIGH_COURT_ADVOCATES; -- Commented out to prevent accidental data loss if run on prod without check

-- Insert Data
INSERT INTO HIGH_COURT_ADVOCATES (AdvocateName, Bench) VALUES 
('M K SOUDAGAR', 'DWR'),
('S C BHUTI', 'DWR'),
('M M KHANNUR', 'DWR'),
('C R MENSINKAI', 'DWR'),
('P R BENTUR', 'DWR'),
('I C PATIL', 'DWR'),
('VAISHALI K', 'DWR'),
('M B KANAVI', 'DWR'),
('M C HUKKERI', 'DWR'),
('PRAKASH HOSMANE', 'DWR'),
('C B PATIL', 'DWR'),
('PRASHANT HOSMANI', 'DWR'),
('LINGARAJ MARADI', 'DWR'),
('M A KARIGENNAVAR', 'DWR'),
('S G RAMPUR', 'DWR'),
('R H SAYED', 'DWR'),
('S N KINI', 'DWR'),
('J S SHETTY', 'DWR'),
('S S DESAI', 'DWR'),
('V M SHEELVANTH', 'DWR'),
('ROHIT PATIL', 'DWR'),
('P P HIREMATH', 'DWR'),
('NAGARAJ K', 'BNG'),
('D VIJAYKUMAR', 'BNG'),
('H R RENUKA', 'BNG'),
('F S DABALI', 'BNG'),
('HARISH BHANDARY', 'BNG'),
('B L SANJEEV', 'BNG'),
('P D SURANA', 'BNG'),
('DEEPAK BARAD', 'GLB'),
('S S MALLAPUR', 'GLB'),
('S M PATIL', 'GLB'),
('SANGEETA BHADRASHETTY', 'GLB'),
('S H MANNUR', 'GLB'),
('VENKATREDDY (HYDERABAD)', 'ANDRA'),
('C M LOKKESHAPPA', 'Mumbai'),
('Dhananjay Ranware', 'Kolhapur'),
('T THYGARAJAN (CHENNAI)', 'TN'),
('P VINAYAKSWAMY(HYDERABAD)', 'ANDRA'),
('SHANKARGOUDA PATIL', 'DELHI'),
('T S SHANTI', 'DELHI');
GO
