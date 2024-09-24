using System.Threading.Channels;
namespace WorkerService
{
    public class TimedHostedService : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private readonly ILogger<TimedHostedService> _logger;
        private Timer? _timer = null;
        public TimedHostedService(ILogger<TimedHostedService> logger)
        {
            _logger = logger;
        }
        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("timed hosted service running");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }
        private void DoWork(object? state)
        {
            var count = Interlocked.Increment(ref executionCount);
            _logger.LogInformation("timed hosted service is working.count:{count}", count);
        }
        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("timed hosted service is stopping");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
    internal interface IScopedProcessingService
    {
        Task DoWork(CancellationToken stoppingToken);
    }
    internal class ScopedProcessingService : IScopedProcessingService
    {
        private int executionCount = 0;
        private readonly ILogger _logger;
        public ScopedProcessingService(ILogger<ScopedProcessingService> logger)
        {
            _logger = logger;
        }
        public async Task DoWork(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                executionCount++;
                _logger.LogInformation(
                    "Scoped Processing Service is working. Count: {Count}", executionCount);
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
    public class ConsumeScopedServiceHostedService : BackgroundService
    {
        private readonly ILogger<ConsumeScopedServiceHostedService> _logger;
        public ConsumeScopedServiceHostedService(IServiceProvider services, ILogger<ConsumeScopedServiceHostedService> logger)
        {
            Services = services;
            _logger = logger;
        }
        public IServiceProvider Services { get; }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("consume scoped service hosted service running");
            await DoWork(stoppingToken);
        }
        private async Task DoWork(CancellationToken stoppingToken)
        {
            _logger.LogInformation("consume scoped service is working");
            using (var scope = Services.CreateScope())
            {
                var scopedProcessingService = scope.ServiceProvider.GetRequiredService<IScopedProcessingService>();
                await scopedProcessingService.DoWork(stoppingToken);
            }
        }
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("consume scoped service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
    public interface IBackgroundTaskQueue
    {
        ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem);
        ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
    }
    public class backgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<CancellationToken, ValueTask>> _queue;
        public backgroundTaskQueue(int capacity)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
        }
        public async ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }
            await _queue.Writer.WriteAsync(workItem);
        }
        public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellation)
        {
            var workItem = await _queue.Reader.ReadAsync(cancellation);
            return workItem;
        }
    }
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        public QueuedHostedService(IBackgroundTaskQueue taskQueue, ILogger<QueuedHostedService> logger)
        {
            _logger = logger;
            TaskQueue = taskQueue;
        }
        public IBackgroundTaskQueue TaskQueue { get; }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Queued hosted service is running.{Environment.NewLine}" + $"{Environment.NewLine}Tap W to add a work item to the " +
            $"background queue.{Environment.NewLine}");
            await BackgroundProcessing(stoppingToken);
        }
        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(stoppingToken);
                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error occured executing {WorkItem}", nameof(workItem));
                }
            }
        }
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("queued service is stopped");
            await base.StopAsync(stoppingToken);
        }
    }
    public class MonitorLoop
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger _logger;
        private readonly CancellationToken _cancellationToken;
        public MonitorLoop(IBackgroundTaskQueue taskQueue, ILogger<MonitorLoop> logger, IHostApplicationLifetime applicationLifetime)
        {
            _taskQueue = taskQueue;
            _logger = logger;
            _cancellationToken = applicationLifetime.ApplicationStopping;
        }
        public void StartMonitorLoop()
        {
            _logger.LogInformation("monitor async is started");
            Task.Run(async () => await MonitorAsync());
        }
        private async ValueTask MonitorAsync()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var keyStroke = Console.ReadKey();
                if (keyStroke.Key == ConsoleKey.W)
                {
                    await _taskQueue.QueueBackgroundWorkItemAsync(BuildWorkItem);
                }
            }
        }
        private async ValueTask BuildWorkItem(CancellationToken token)
        {
            int delayLoop = 0;
            var guid = Guid.NewGuid().ToString();
            _logger.LogInformation("background task {Guid} is starting", guid);
            while (!token.IsCancellationRequested && delayLoop < 3)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (OperationCanceledException)
                {

                }
                delayLoop++;
                _logger.LogInformation("delayed", guid, delayLoop);
            }
            if (delayLoop == 3)
            {
                _logger.LogInformation("completed", guid);
            }
            else
            {
                _logger.LogInformation("cancelled", guid);
            }
        }
    }
}