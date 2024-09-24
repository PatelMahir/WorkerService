using Microsoft.AspNet.SignalR.Hosting;

namespace WorkerService1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();



            var host = builder.Build();
            host.Run();
        }
    }
}