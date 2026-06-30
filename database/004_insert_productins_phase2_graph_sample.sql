INSERT INTO dbo.ProductIns
(
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
)
VALUES
(
    '1234567',
    N'$(Sfc)',
    'ZE01-25',
    'Z1307695',
    'NG',
    N'C:\ImageBackup\2026\06\26\PHASE2-GRAPH-TEST\NG\Z1307695_ZE01-25_NG_PHASE2.jpg',
    GETDATE(),
    0,
    NULL,
    NULL
);
GO

SELECT TOP (1)
    ID,
    SFC,
    IsSentTeams,
    SentTeamsTime,
    SendErrorMessage
FROM dbo.ProductIns
WHERE SFC = N'$(Sfc)'
ORDER BY ID DESC;
GO
