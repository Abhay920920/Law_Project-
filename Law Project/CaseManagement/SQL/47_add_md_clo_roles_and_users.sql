-- SQL script to add MD and CLO roles and users
USE MVCCaseDB;

-- Add ROLE_MASTER entries if they don't exist
IF NOT EXISTS (SELECT 1 FROM ROLE_MASTER WHERE RoleName = 'MD')
BEGIN
    INSERT INTO ROLE_MASTER (RoleName, IsActive) VALUES ('MD', 1);
END

IF NOT EXISTS (SELECT 1 FROM ROLE_MASTER WHERE RoleName = 'CLO')
BEGIN
    INSERT INTO ROLE_MASTER (RoleName, IsActive) VALUES ('CLO', 1);
END

-- Get Role IDs
DECLARE @MdRoleID INT = (SELECT RoleID FROM ROLE_MASTER WHERE RoleName = 'MD');
DECLARE @CloRoleID INT = (SELECT RoleID FROM ROLE_MASTER WHERE RoleName = 'CLO');

-- Add Users if they don't exist
-- Using legacy SHA256 hash for 'Admin@123'
DECLARE @DefaultPasswordHash NVARCHAR(256) = '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918';

-- DivisionID 5 is Central Office
IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'md')
BEGIN
    INSERT INTO USERS (Username, PasswordHash, FullName, RoleID, DivisionID, IsActive, CreatedDate)
    VALUES ('md', @DefaultPasswordHash, 'Managing Director', @MdRoleID, 5, 1, GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'clo')
BEGIN
    INSERT INTO USERS (Username, PasswordHash, FullName, RoleID, DivisionID, IsActive, CreatedDate)
    VALUES ('clo', @DefaultPasswordHash, 'Chief Law Officer', @CloRoleID, 5, 1, GETDATE());
END
