-- SQL Script to format MACT names: "MACTName, Location"
USE [Admin_Law];
GO

-- Update MACTName to append Location if it's not already there
UPDATE MACT_MASTER
SET MACTName = MACTName + ', ' + Location
WHERE IsActive = 1 
  AND Location IS NOT NULL 
  AND Location <> ''
  -- Ensure we don't append if the name already contains the location at the end
  AND MACTName NOT LIKE '%, ' + Location
  AND MACTName NOT LIKE '% ' + Location;

-- Verify the changes
SELECT MACTID, MACTName, Location 
FROM MACT_MASTER 
WHERE IsActive = 1
ORDER BY MACTID DESC;
GO
