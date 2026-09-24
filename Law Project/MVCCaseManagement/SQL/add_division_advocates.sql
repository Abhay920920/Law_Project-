IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MVC_DIVISION_ADVOCATES]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MVC_DIVISION_ADVOCATES](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [DivisionName] [nvarchar](100) NULL,
        [AdvocateName] [nvarchar](255) NULL,
        CONSTRAINT [PK_MVC_DIVISION_ADVOCATES] PRIMARY KEY CLUSTERED ([Id] ASC)
    )
END
GO

-- Insert Data for Dharwad Division
INSERT INTO [dbo].[MVC_DIVISION_ADVOCATES] (DivisionName, AdvocateName) VALUES 
('Dharwad Division', N'Sri G H Naik'),
('Dharwad Division', N'Sri M G Patil'),
('Dharwad Division', N'V T Kulkarni'),
('Dharwad Division', N'Sri J M Mulla'),
('Dharwad Division', N'Sri S B Rotti'),
('Dharwad Division', N'Krishnamurty'),
('Dharwad Division', N'Sri V G Biradar Patil'),
('Dharwad Division', N'Smt M P Ronad'),
('Dharwad Division', N'Sri S M Sangreshkop'),
('Dharwad Division', N'Sri A G Huddar'),
('Dharwad Division', N'Sri S T Kamanhalli'),
('Dharwad Division', N'Sri L M Heggannavar'),
('Dharwad Division', N'Sri S M Deshabhandari'),
('Dharwad Division', N'Sri P B Madiwalar'),
('Dharwad Division', N'Sri G V Joshi'),
('Dharwad Division', N'Sri Mohd Sultan'),
('Dharwad Division', N'Smt Veena Gasti'),
('Dharwad Division', N'Smt Sumangala Kognur'),
('Dharwad Division', N'Sri Srinivas Aital'),
('Dharwad Division', N'Sri Mahadevaiah'),
('Dharwad Division', N'Sri Deepak Banarji'),
('Dharwad Division', N'Sri D S Bongale'),
('Dharwad Division', N'Sri G V Bhagavat'),
('Dharwad Division', N'Sri M A Hudali'),
('Dharwad Division', N'Sri C V Turumari'),
('Dharwad Division', N'Sri M B Patil'),
('Dharwad Division', N'Sri M Pampangouda'),
('Dharwad Division', N'Sri B N Patil'),
('Dharwad Division', N'S P Kamate'),
('Dharwad Division', N'Sri S A Sanganal'),
('Dharwad Division', N'Sri V B Dhavaleshwar'),
('Dharwad Division', N'Sri M M Yaligar'),
('Dharwad Division', N'Sri S S Pattar'),
('Dharwad Division', N'Sri S G Topannavar'),
('Dharwad Division', N'Sri F I Sidlingannavar'),
('Dharwad Division', N'Sri S B Venkatgiri'),
('Dharwad Division', N'Sri Pradeep G Kori'),
('Dharwad Division', N'Sri C V Badri');
GO
