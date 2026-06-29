SELECT TOP 20
    ID,
    EMPLOYEE_NO,
    SFC,
    TOOL_ID,
    TOOL_SN,
    CheckResult,
    ImagePath,
    DateTime,
    IsSentTeams,
    SentTeamsTime,
    SendErrorMessage
FROM dbo.ToolCheckResult
ORDER BY ID DESC;
GO
