using TL_ORR;
using TL_ORR.Options;
using TL_ORR.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<TeamsOptions>(builder.Configuration.GetSection("Teams"));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<FileShareOptions>(builder.Configuration.GetSection("FileShare"));

builder.Services.AddSingleton<IToolCheckResultService, ToolCheckResultService>();
builder.Services.AddSingleton<IUncPathConverter, UncPathConverter>();
builder.Services.AddSingleton<INotificationMessageFormatter, NotificationMessageFormatter>();
builder.Services.AddHttpClient<ITeamsNotifyService, TeamsNotifyService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
