# TL_ORR - Teams NG 檢測通知系統

## Phase 1 範圍

Phase 1 先完成背景服務主流程，不實際呼叫 Microsoft Graph：

- 建立 .NET Worker Service。
- 連線 MS SQL Server。
- 查詢 `CheckResult = 'NG'` 且 `IsSentTeams = 0` 的資料。
- 將 Teams 訊息寫入 console/log 模擬發送。
- 實作 `ImagePath` 轉 UNC 網路路徑。
- 模擬發送成功後回寫 `IsSentTeams = 1`、`SentTeamsTime = GETDATE()`。

## SQL Schema

```sql
CREATE TABLE dbo.ToolCheckResult (
    ID int IDENTITY(1,1) PRIMARY KEY,
    EMPLOYEE_NO varchar(20) NOT NULL,
    SFC varchar(50) NOT NULL,
    TOOL_ID varchar(50) NOT NULL,
    TOOL_SN varchar(50) NOT NULL,
    CheckResult varchar(10) NOT NULL,
    ImagePath nvarchar(500) NULL,
    DateTime datetime NOT NULL,
    IsSentTeams bit NOT NULL DEFAULT 0,
    SentTeamsTime datetime NULL,
    SendErrorMessage nvarchar(1000) NULL
);
```

## Phase 1 測試資料

```sql
INSERT INTO dbo.ToolCheckResult
(
    EMPLOYEE_NO,
    SFC,
    TOOL_ID,
    TOOL_SN,
    CheckResult,
    ImagePath,
    DateTime
)
VALUES
(
    '1234567',
    '123456789',
    'ZE01-25',
    'Z1307695',
    'NG',
    N'C:\ImageBackup\2026\06\26\0123456789\NG\Z1307695_ZE01-25_NG4.jpg',
    GETDATE()
);
```

## appsettings.json

Phase 1 請保持 `Teams:SendMode` 為 `Console`。

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  },
  "Teams": {
    "SendMode": "Console",
    "TargetUserEmail": "alvint@amulaire.com"
  },
  "Worker": {
    "IntervalSeconds": 60,
    "BatchSize": 100
  },
  "FileShare": {
    "ServerIP": "192.168.1.100",
    "ShareName": "ImageBackup"
  }
}
```

## 執行

```powershell
dotnet run --project .\TL_ORR\TL_ORR.csproj
```

## Phase 1 驗收

- Console/log 出現 `Phase 1 Teams message simulation`。
- 訊息內容包含員工編號、SFC、Tool ID、Tool SN、檢測結果、圖片路徑與檢測時間。
- 圖片路徑由 `C:\ImageBackup\...` 轉成 `\\192.168.1.100\ImageBackup\...`。
- SQL 該筆資料更新為 `IsSentTeams = 1` 且 `SentTeamsTime` 有值。
