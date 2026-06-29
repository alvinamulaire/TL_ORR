# Teams NG 檢測通知系統開發規則

## 目標

建立 .NET Worker Service，定期從 MS SQL Server 讀取檢測資料。當資料符合 `CheckResult = 'NG'` 且 `IsSentTeams = 0` 時，發送 Microsoft Teams 異常通知給指定使用者，並依發送結果回寫 SQL 狀態。

## 資料規則

- 資料表使用 `dbo.ToolCheckResult`。
- 每輪只查詢 `CheckResult = 'NG'` 且 `IsSentTeams = 0` 的資料。
- 每批最多查詢 `Worker:BatchSize` 筆，預設 100。
- 查詢順序依 `DateTime ASC`，先發送較早發生的異常。
- 成功發送後必須更新：
  - `IsSentTeams = 1`
  - `SentTeamsTime = GETDATE()`
  - `SendErrorMessage = NULL`
- 發送失敗後必須更新 `SendErrorMessage`，不得將 `IsSentTeams` 設為 1。
- 單筆資料失敗不得中斷同批其他資料處理。

## 訊息規則

- Teams 訊息標題固定為 `【檢測異常通知】`。
- 訊息內容必須包含：
  - 員工編號
  - SFC
  - Tool ID
  - Tool SN
  - 檢測結果
  - 圖片路徑
  - 檢測時間
- `ImagePath` 必須嘗試由本機路徑轉成 UNC 網路路徑。
- UNC 轉換規則：
  - 移除本機磁碟根目錄，例如 `C:\`
  - 以 `\\{FileShare:ServerIP}\{FileShare:ShareName}\` 作為根路徑
  - 若轉換失敗，保留原始路徑並寫入 log

## Microsoft Graph 規則

- Phase 1 預設使用 `Teams:SendMode = Console`，只將 Teams 訊息寫入 log，不呼叫 Graph。
- Phase 2 才切換為 `Teams:SendMode = Graph` 並啟用真實 Teams 發送。
- 使用 Client Credentials Flow 取得 Graph access token。
- 使用 `Teams:TargetUserEmail` 查詢目標使用者。
- 建立或取得與目標使用者的 1:1 chat。
- 透過 Graph `chats/{chat-id}/messages` 發送訊息。
- 設定不完整時，不應假裝發送成功；需丟出明確錯誤並讓 Worker 寫入 `SendErrorMessage`。

## 背景服務規則

- 輪詢間隔使用 `Worker:IntervalSeconds`，預設 60 秒。
- Phase 1 可使用 `Worker:RunOnce = true`，跑完一輪後停止 host，方便驗收。
- 每輪流程：
  1. 查詢尚未通知的 NG 資料。
  2. 逐筆轉換圖片路徑。
  3. 組 Teams 訊息。
  4. 發送 Teams。
  5. 成功更新通知狀態，失敗寫入錯誤訊息。
  6. 等待下一輪。
- SQL 連線失敗、Graph token 失敗、Teams 發送失敗皆需寫入 log，下一輪重試。
- 服務必須支援 cancellation token，方便 Windows Service 或容器停止。

## 設定規則

- 連線字串放在 `ConnectionStrings:DefaultConnection`。
- Teams 設定放在 `Teams` 區段：
  - `SendMode`：Phase 1 使用 `Console`，Phase 2 使用 `Graph`
  - `TenantId`
  - `ClientId`
  - `ClientSecret`
  - `TargetUserEmail`
- Worker 設定放在 `Worker` 區段：
  - `IntervalSeconds`
  - `BatchSize`
  - `RunOnce`
- FileShare 設定放在 `FileShare` 區段：
  - `ServerIP`
  - `ShareName`

## 驗收條件

- 可定期讀取 MS SQL Server。
- 只發送 `CheckResult = 'NG'` 的資料。
- 不重複發送 `IsSentTeams = 1` 的資料。
- Teams 可收到完整檢測資訊。
- 圖片路徑為可點擊的 UNC 網路路徑。
- 發送成功會更新 `IsSentTeams = 1`。
- 發送失敗會記錄錯誤訊息。
- 服務可長時間背景執行。
