IF OBJECT_ID(N'dbo.ToolCheckResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ToolCheckResult (
        ID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ToolCheckResult PRIMARY KEY,
        EMPLOYEE_NO varchar(20) NOT NULL,
        SFC varchar(50) NOT NULL,
        TOOL_ID varchar(50) NOT NULL,
        TOOL_SN varchar(50) NOT NULL,
        CheckResult varchar(10) NOT NULL,
        ImagePath nvarchar(500) NULL,
        DateTime datetime NOT NULL,
        IsSentTeams bit NOT NULL CONSTRAINT DF_ToolCheckResult_IsSentTeams DEFAULT 0,
        SentTeamsTime datetime NULL,
        SendErrorMessage nvarchar(1000) NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ToolCheckResult_PendingTeams'
      AND object_id = OBJECT_ID(N'dbo.ToolCheckResult', N'U')
)
BEGIN
    CREATE INDEX IX_ToolCheckResult_PendingTeams
    ON dbo.ToolCheckResult (CheckResult, IsSentTeams, DateTime)
    INCLUDE (EMPLOYEE_NO, SFC, TOOL_ID, TOOL_SN, ImagePath);
END;
GO
