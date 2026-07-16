using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using TL_ORR;
using TL_ORR.Options;
using TL_ORR.Services;

AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

var builder = Host.CreateApplicationBuilder(args);
const string ServiceName = "TL_ORR Teams NG Notify Service";

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ServiceName;
});

if (OperatingSystem.IsWindows())
{
    ConfigureWindowsEventLog(builder.Logging, ServiceName);
}

builder.Services.Configure<TeamsOptions>(builder.Configuration.GetSection("Teams"));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<FileShareOptions>(builder.Configuration.GetSection("FileShare"));
builder.Services.Configure<NotificationRecipientOptions>(builder.Configuration.GetSection("NotificationRecipients"));

builder.Services.AddSingleton<IToolCheckResultService, ToolCheckResultService>();
builder.Services.AddSingleton<INotificationRecipientService, NotificationRecipientService>();
builder.Services.AddSingleton<IUncPathConverter, UncPathConverter>();
builder.Services.AddSingleton<INotificationMessageFormatter, NotificationMessageFormatter>();
builder.Services.AddHttpClient<ITeamsNotifyService, TeamsNotifyService>((serviceProvider, httpClient) =>
{
    var teamsOptions = serviceProvider.GetRequiredService<IOptions<TeamsOptions>>().Value;
    httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, teamsOptions.HttpTimeoutSeconds));
});

builder.Services.AddHostedService<StartupConfigurationValidator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

[SupportedOSPlatform("windows")]
static void ConfigureWindowsEventLog(ILoggingBuilder loggingBuilder, string sourceName)
{
    loggingBuilder.AddEventLog(settings =>
    {
#pragma warning disable CA1416
        settings.SourceName = sourceName;
#pragma warning restore CA1416
    });
}
