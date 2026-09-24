IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_EP_DETAILS') AND name = 'VehicleNo')
BEGIN
    ALTER TABLE MVC_EP_DETAILS ADD VehicleNo varchar(20);
    PRINT 'Added VehicleNo column to MVC_EP_DETAILS';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MVC_EP_DETAILS') AND name = 'AccidentDate')
BEGIN
    ALTER TABLE MVC_EP_DETAILS ADD AccidentDate datetime;
    PRINT 'Added AccidentDate column to MVC_EP_DETAILS';
END
