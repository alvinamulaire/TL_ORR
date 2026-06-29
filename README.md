# TL_ORR - Teams NG 檢測通知系統

## Phase 1 範圍

Phase 1 先完成背景服務主流程，不實際呼叫 Microsoft Graph：

- 建立 .NET Worker Service。
- 連線 MS SQL Server。
- 查詢 `CheckResult = 'NG'` 且 `IsSentTeams = 0` 的資料。
- 將 Teams 訊息寫入 console/log 模擬發送。
- 實作 `ImagePath` 轉 UNC 網路路徑。
- 模擬發送成功後回寫 `IsSentTeams = 1`、`SentTeamsTime = GETDATE()`。
- Development 環境預設 `Worker:RunOnce = true`，方便單次驗收。

## SQL Scripts

請依序執行：

- `database/001_create_tool_check_result.sql`：建立資料表與 pending index。
- `database/002_insert_phase1_sample.sql`：新增一筆 NG 測試資料。
- `database/003_phase1_acceptance_check.sql`：驗收 Worker 回寫結果。

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
    "BatchSize": 100,
    "RunOnce": true
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
- Development 環境跑完一輪後會自動停止。
