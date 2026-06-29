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
FROM dbo.ProductIns
ORDER BY DateTime DESC;
GO
