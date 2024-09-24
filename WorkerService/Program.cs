using Microsoft.AspNet.SignalR.Hosting;
using WorkerService;

namespace WorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();
            builder.Services.AddHostedService<TimedHostedService>();
            builder.Services.AddHostedService<ConsumeScopedServiceHostedService>();
            builder.Services.AddScoped<IScopedProcessingService, ScopedProcessingService>();
            builder.Services.AddSingleton<MonitorLoop>();
            builder.Services.AddHostedService<QueuedHostedService>();
            builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
            {
                if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
                    queueCapacity = 100;
                return new backgroundTaskQueue(queueCapacity);
            });
            var host = builder.Build();
            var monitorLoop = host.Services.GetRequiredService<MonitorLoop>();
            monitorLoop.StartMonitorLoop();
            host.Run();
        }
    }
}

//using Microsoft.AspNet.SignalR.Hosting;
//using WorkerService;
//var builder = Host.CreateApplicationBuilder(args);
//var host1 = builder.Build();

//builder.Services.AddHostedService<Worker>();
//builder.Services.AddHostedService<TimedHostedService>();
//builder.Services.AddHostedService<ConsumeScopedServiceHostedService>();
//builder.Services.AddScoped<IScopedProcessingService, ScopedProcessingService>();

//builder.Services.AddSingleton<MonitorLoop>();
//builder.Services.AddHostedService<QueuedHostedService>();
//builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
//{
//    if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
//        queueCapacity = 100;
//    return new backgroundTaskQueue(queueCapacity);
//});

//var monitorLoop = host1.Services.GetRequiredService<MonitorLoop>();
//monitorLoop.StartMonitorLoop();


// Old

//builder.Services.AddHostedService<Worker>();
//builder.Services.AddHostedService<TimedHostedService>();
//builder.Services.AddHostedService<ConsumeScopedServiceHostedService>();
//builder.Services.AddScoped<IScopedProcessingService, ScopedProcessingService>();
//builder.Services.AddSingleton<MonitorLoop>();
//builder.Services.AddHostedService<QueuedHostedService>();

//var host = builder.Build();

//builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
//{

//    //if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
//        //queueCapacity = 100;
//    return new backgroundTaskQueue(100);
//});


//var monitorLoop = host.Services.GetRequiredService<MonitorLoop>();
//monitorLoop.StartMonitorLoop();
//host1.Run();