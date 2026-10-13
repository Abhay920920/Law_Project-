-- Script 59: Add Dy CLO and LO roles and users
USE Admin_Law;

-- 1. Ensure ROLES exist
IF NOT EXISTS (SELECT 1 FROM ROLE_MASTER WHERE RoleName = 'LO')
BEGIN
    INSERT INTO ROLE_MASTER (RoleName, IsActive) VALUES ('LO', 1);
END

IF NOT EXISTS (SELECT 1 FROM ROLE_MASTER WHERE RoleName = 'Dy CLO')
BEGIN
    INSERT INTO ROLE_MASTER (RoleName, IsActive) VALUES ('Dy CLO', 1);
END

-- Get Role IDs
DECLARE @LoRoleID INT = (SELECT RoleID FROM ROLE_MASTER WHERE RoleName = 'LO');
DECLARE @DyCloRoleID INT = (SELECT RoleID FROM ROLE_MASTER WHERE RoleName = 'Dy CLO');

-- Default Password: 123456 (standard application password hash)
DECLARE @DefaultPasswordHash NVARCHAR(256) = '8D969EEF6ECAD3C29A3A629280E686CF0C3F5D5A86AFF3CA12020C923ADC6C92';

-- 2. Add 'dy_clo' User (DivisionID 5 = Central Office)
IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'dy_clo')
BEGIN
    INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
    VALUES ('dy_clo', @DefaultPasswordHash, 'Deputy Chief Law Officer', 'dyclo@nwkrtc.in', '9876543210', @DyCloRoleID, 5, 1, GETDATE());
END
ELSE
BEGIN
    UPDATE USERS 
    SET RoleID = @DyCloRoleID, DivisionID = 5, IsActive = 1, PasswordHash = @DefaultPasswordHash
    WHERE Username = 'dy_clo';
END

-- Also add 'dyclo' without underscore for convenient login
IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'dyclo')
BEGIN
    INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
    VALUES ('dyclo', @DefaultPasswordHash, 'Deputy Chief Law Officer', 'dyclo@nwkrtc.in', '9876543210', @DyCloRoleID, 5, 1, GETDATE());
END
ELSE
BEGIN
    UPDATE USERS 
    SET RoleID = @DyCloRoleID, DivisionID = 5, IsActive = 1, PasswordHash = @DefaultPasswordHash
    WHERE Username = 'dyclo';
END

-- 3. Add 'lo' User (DivisionID 5 = Central Office)
IF NOT EXISTS (SELECT 1 FROM USERS WHERE Username = 'lo')
BEGIN
    INSERT INTO USERS (Username, PasswordHash, FullName, Email, Mobile, RoleID, DivisionID, IsActive, CreatedDate)
    VALUES ('lo', @DefaultPasswordHash, 'Law Officer', 'lo@nwkrtc.in', '9876543211', @LoRoleID, 5, 1, GETDATE());
END
ELSE
BEGIN
    UPDATE USERS 
    SET RoleID = @LoRoleID, DivisionID = 5, IsActive = 1, PasswordHash = @DefaultPasswordHash
    WHERE Username = 'lo';
END
