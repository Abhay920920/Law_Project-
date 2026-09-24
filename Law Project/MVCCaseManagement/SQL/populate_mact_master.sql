-- SQL Script to populate MACT_MASTER table
USE [Admin_Law];
GO

-- Create a temporary table to hold the new data
CREATE TABLE #NewMACTs (
    MACTName NVARCHAR(255),
    Location NVARCHAR(255)
);

-- Insert data into temporary table
INSERT INTO #NewMACTs (MACTName, Location) VALUES
(N'Prl. District & Sessions Judge & MACT', N'Bengaluru City'),
(N'Judge, Court of Small Causes & MACT (SCCH-1)', N'Bengaluru City'),
(N'Judge, Court of Small Causes & MACT (SCCH-2)', N'Bengaluru City'),
(N'Judge, Court of Small Causes & MACT (SCCH-3)', N'Bengaluru City'),
(N'Judge, Court of Small Causes & MACT (SCCH-4)', N'Bengaluru City'),
(N'Prl. District & Sessions Judge & MACT', N'Bengaluru Rural'),
(N'Prl. Senior Civil Judge & MACT', N'Devanahalli'),
(N'Prl. Senior Civil Judge & MACT', N'Doddaballapura'),
(N'Senior Civil Judge & MACT', N'Nelamangala'),
(N'Prl. District & Sessions Judge & MACT', N'Ramanagara'),
(N'Senior Civil Judge & MACT', N'Kanakapura'),
(N'Prl. District & Sessions Judge & MACT', N'Mysuru'),
(N'Prl. District & Sessions Judge & MACT', N'Mandya'),
(N'Prl. District & Sessions Judge & MACT', N'Hassan'),
(N'Senior Civil Judge & MACT', N'Sakleshpur'),
(N'Prl. District & Sessions Judge & MACT', N'Chikkamagaluru'),
(N'Prl. District & Sessions Judge & MACT', N'Tumakuru'),
(N'Prl. District & Sessions Judge & MACT', N'Kolar'),
(N'Prl. District & Sessions Judge & MACT', N'Chikkaballapura'),
(N'Prl. District & Sessions Judge & MACT', N'Davanagere'),
(N'Senior Civil Judge & MACT', N'Channagiri'),
(N'Senior Civil Judge & MACT', N'Jagalur'),
(N'Prl. District & Sessions Judge & MACT', N'Shivamogga'),
(N'Prl. District & Sessions Judge & MACT', N'Ballari'),
(N'Senior Civil Judge & MACT', N'Sandur'),
(N'Prl. District & Sessions Judge & MACT', N'Koppal'),
(N'Prl. District & Sessions Judge & MACT', N'Raichur'),
(N'Prl. District & Sessions Judge & MACT', N'Kalaburagi'),
(N'Prl. District & Sessions Judge & MACT', N'Bidar'),
(N'Prl. District & Sessions Judge & MACT', N'Vijayapura'),
(N'Prl. District & Sessions Judge & MACT', N'Yadgir'),
(N'Prl. District & Sessions Judge & MACT', N'Chitradurga'),
(N'Prl. District & Sessions Judge & MACT', N'Kodagu (Madikeri)'),
(N'Prl. District & Sessions Judge & MACT', N'Dakshina Kannada (Mangaluru)'),
(N'Senior Civil Judge & MACT', N'Bantwal'),
(N'Prl. District & Sessions Judge & MACT', N'Udupi'),
(N'Prl. District & Sessions Judge & MACT', N'Chamarajanagara'),
(N'Prl. District & Sessions Judge & MACT', N'Gadag');

-- Insert from temp table to MACT_MASTER where not already exists
INSERT INTO MACT_MASTER (MACTCode, MACTName, Location, IsActive)
SELECT 
    'M' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS NVARCHAR(4)) as MACTCode,
    n.MACTName,
    n.Location,
    1 as IsActive
FROM #NewMACTs n
WHERE NOT EXISTS (
    SELECT 1 FROM MACT_MASTER m 
    WHERE m.MACTName = n.MACTName AND m.Location = n.Location
);

-- Drop temp table
DROP TABLE #NewMACTs;

-- Verify
SELECT COUNT(*) as RowsAdded FROM MACT_MASTER WHERE IsActive = 1;
GO
