-- Add MedicalExpenseOther and missing injury fields to MVC_CASE_ADVERSE_DETAILS
ALTER TABLE MVC_CASE_ADVERSE_DETAILS ADD 
    MedicalExpenseOther DECIMAL(18, 2) NULL,
    PainSufferings DECIMAL(18, 2) NULL,
    ConveyanceAttendant DECIMAL(18, 2) NULL,
    LossOfFutureIncome DECIMAL(18, 2) NULL,
    LossOfIncomeLaidUp DECIMAL(18, 2) NULL,
    LossOfAmenities DECIMAL(18, 2) NULL,
    FutureMedicalExpenses DECIMAL(18, 2) NULL;
GO
