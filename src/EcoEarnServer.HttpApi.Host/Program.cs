using System;
using System.Threading;
using System.Threading.Tasks;
using EcoEarnServer.Extension;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EcoEarnServer
{
    public class Program
    {
        public async static Task<int> Main(string[] args)
        {
            ThreadPool.SetMinThreads(300, 300);
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            try
            {
                Log.Information("Starting EcoEarnServer.HttpApi.Host");

                var builder = WebApplication.CreateBuilder(args);
                builder.Configuration.AddJsonFile("apollo.appsettings.json");
                builder.Host.AddAppSettingsSecretsJson()
                    .UseApollo()
                    .UseAutofac()
                    .UseSerilog();

                await builder.AddApplicationAsync<EcoEarnServerHttpApiHostModule>();
                var app = builder.Build();
                await app.InitializeApplicationAsync();
                await app.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly!");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}