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
            $"檢測時間：{Encode(checkedAt)}"
        };

        lines.AddRange(BuildImageLines(imagePath));

        return string.Join("<br>", lines);
    }

    private static IEnumerable<string> BuildImageLines(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            yield return "圖片路徑：";
            yield break;
        }

        yield return $"圖片路徑：{Encode(imagePath)}";

        var link = BuildImageLink(imagePath);
        if (!string.IsNullOrWhiteSpace(link))
        {
            yield return $"圖片連結：<a href=\"{Encode(link)}\">開啟圖片</a>";
        }

        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            yield return $"<img src=\"{Encode(uri.AbsoluteUri)}\" alt=\"Tool NG image\" />";
        }
    }

    private static string BuildImageLink(string imagePath)
    {
        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        if (imagePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return new Uri(imagePath).AbsoluteUri;
        }

        return string.Empty;
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
