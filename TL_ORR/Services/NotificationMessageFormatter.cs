using System.Globalization;
using System.Net;
using TL_ORR.Models;

namespace TL_ORR.Services;

public sealed class NotificationMessageFormatter : INotificationMessageFormatter
{
    public string Format(ToolCheckResult result, string imagePath)
    {
        var checkedAt = result.CheckedAt.ToString("yyyy/M/d HH:mm", CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            "【檢測異常通知】",
            string.Empty,
            $"員工編號：{Encode(result.EmployeeNo)}",
            $"SFC：{Encode(result.Sfc)}",
            $"Tool ID：{Encode(result.ToolId)}",
            $"Tool SN：{Encode(result.ToolSn)}",
            $"檢測結果：{Encode(result.CheckResult)}",
            $"檢測時間：{Encode(checkedAt)}",
            $"圖片路徑：{Encode(imagePath)}"
        };

        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            lines.Add($"<img src=\"{Encode(uri.AbsoluteUri)}\" alt=\"Tool NG image\" />");
        }

        return string.Join("<br>", lines);
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
