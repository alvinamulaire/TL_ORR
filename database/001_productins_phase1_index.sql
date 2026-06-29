IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ProductIns_PendingTeams'
      AND object_id = OBJECT_ID(N'dbo.ProductIns', N'U')
)
BEGIN
    CREATE INDEX IX_ProductIns_PendingTeams
    ON dbo.ProductIns (CheckResult, IsSentTeams, DateTime)
    INCLUDE (EMPLOYEE_NO, SFC, TOOL_ID, TOOL_SN, ImagePath);
END;
GO
