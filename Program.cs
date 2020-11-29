using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace EFCoreMigrationDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var host = CreateHostBuilder(args).Build();
            var autoMigraion= host.Services.GetService<AutoMigration>();
            autoMigraion.Migrate();
            host.Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
#if DEBUG
                  .UseEnvironment(Environments.Development)
#endif
                  .ConfigureAppConfiguration((hostingContext, config) =>
                  {
                        config.AddJsonFile("appsettings.json");
                  })
                  .ConfigureServices((context, services) =>
                  {
                        services.AddDbContext<NewDbContext>(builder => builder.UseSqlServer(context.Configuration.GetConnectionString("ConnectionString")));
                      services.AddSingleton<AutoMigration>();
                  });
        }
    }
}
