using System;
using System.IO;
using System.Linq;
using AutoResponseWrapper;
using EcoEarnServer.Grains;
using EcoEarnServer.Middleware;
using EcoEarnServer.MongoDB;
using EcoEarnServer.Options;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Orleans;
using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring.Aliyun;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Threading;
using Volo.Abp.VirtualFileSystem;

namespace EcoEarnServer
{
    [DependsOn(
        typeof(EcoEarnServerHttpApiModule),
        typeof(AbpAutofacModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(EcoEarnServerApplicationModule),
        typeof(EcoEarnServerMongoDbModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpSwashbuckleModule),
        typeof(AbpEventBusRabbitMqModule),
        typeof(AbpBlobStoringAliyunModule)
    )]
    public class EcoEarnServerHttpApiHostModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            Configure<ChainOption>(configuration.GetSection("ChainOption"));
            Configure<ProjectItemOptions>(configuration.GetSection("ProjectItems"));
            Configure<ProjectKeyPairInfoOptions>(configuration.GetSection("ProjectKeyPairInfo"));
            Configure<TokenPoolIconsOptions>(configuration.GetSection("TokenPoolIcons"));
            Configure<PoolTextWordOptions>(configuration.GetSection("PoolTextWord"));
            Configure<LpPoolRateOptions>(configuration.GetSection("LpPoolRate"));
            Configure<EcoEarnContractOptions>(configuration.GetSection("EcoEarnContract"));
            Configure<SecurityServerOptions>(configuration.GetSection("SecurityServer"));
            Configure<HamsterServerOptions>(configuration.GetSection("HamsterServer"));
            Configure<PoolInfoOptions>(configuration.GetSection("PoolInfo"));
            Configure<CoinMarketCapServerOptions>(configuration.GetSection("CoinMarketCapServer"));
            Configure<SymbolMarketCapOptions>(configuration.GetSection("SymbolMarketCap"));
            Configure<DappHighRewardsOptions>(configuration.GetSection("DappHighRewards"));

            ConfigureConventionalControllers();
            // ConfigureAuthentication(context, configuration);
            ConfigureLocalization();
            ConfigureCache(configuration);
            ConfigureVirtualFileSystem(context);
            ConfigureRedis(context, configuration, hostingEnvironment);
            ConfigureCors(context, configuration);
            ConfigureSwaggerServices(context, configuration);
            ConfigureOrleans(context, configuration);
            ConfigureGraphQl(context, configuration);
            context.Services.AddAutoResponseWrapper();
        }

        private void ConfigureCache(IConfiguration configuration)
        {
            Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "EcoEarnServer:"; });
        }

        private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();

            if (hostingEnvironment.IsDevelopment())
            {
                Configure<AbpVirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<EcoEarnServerDomainSharedModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}EcoEarnServer.Domain.Shared"));
                    options.FileSets.ReplaceEmbeddedByPhysical<EcoEarnServerDomainModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}EcoEarnServer.Domain"));
                    options.FileSets.ReplaceEmbeddedByPhysical<EcoEarnServerApplicationContractsModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}EcoEarnServer.Application.Contracts"));
                    options.FileSets.ReplaceEmbeddedByPhysical<EcoEarnServerApplicationModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}EcoEarnServer.Application"));
                });
            }
        }

        private void ConfigureConventionalControllers()
        {
            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options.ConventionalControllers.Create(typeof(EcoEarnServerHttpApiModule).Assembly);
            });
        }


        private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAbpSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "EcoEarnServer API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Scheme = "bearer",
                    Description = "Specify the authorization token.",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        new string[] { }
                    }
                });
            });
        }

        private void ConfigureLocalization()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
                options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
                options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
                options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish"));
                options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
                options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi", "in"));
                options.Languages.Add(new LanguageInfo("it", "it", "Italian", "it"));
                options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
                options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
                options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
                options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak"));
                options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
                options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
                options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch", "de"));
                options.Languages.Add(new LanguageInfo("es", "es", "Español", "es"));
            });
        }

        private void ConfigureRedis(
            ServiceConfigurationContext context,
            IConfiguration configuration,
            IWebHostEnvironment hostingEnvironment)
        {
            if (!hostingEnvironment.IsDevelopment())
            {
                var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
                context.Services
                    .AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, "EcoEarnServer-Protection-Keys");
            }
        }

        private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        private static void ConfigureOrleans(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddSingleton<IClusterClient>(o =>
            {
                return new ClientBuilder()
                    .ConfigureDefaults()
                    .UseMongoDBClient(configuration["Orleans:MongoDBClient"])
                    .UseMongoDBClustering(options =>
                    {
                        options.DatabaseName = configuration["Orleans:DataBase"];
                        options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                    })
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = configuration["Orleans:ClusterId"];
                        options.ServiceId = configuration["Orleans:ServiceId"];
                    })
                    .ConfigureApplicationParts(parts =>
                        parts.AddApplicationPart(typeof(EcoEarnServerGrainsModule).Assembly).WithReferences())
                    .ConfigureLogging(builder => builder.AddProvider(o.GetService<ILoggerProvider>()))
                    .Build();
            });
        }

        private void ConfigureGraphQl(ServiceConfigurationContext context,
            IConfiguration configuration)
        {
            context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
                new NewtonsoftJsonSerializer()));
            context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCorrelationId();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();

            app.UseAbpRequestLocalization();
            app.UseAuthorization();

            // if (env.IsDevelopment())
            // {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support APP API"); });
            // }

            app.UseMiddleware<DeviceInfoMiddleware>();
            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseUnitOfWork();
            app.UseConfiguredEndpoints();

            StartOrleans(context.ServiceProvider);
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            StopOrleans(context.ServiceProvider);
        }

        private static void StartOrleans(IServiceProvider serviceProvider)
        {
            var client = serviceProvider.GetRequiredService<IClusterClient>();
            AsyncHelper.RunSync(async () => await client.Connect());
        }

        private static void StopOrleans(IServiceProvider serviceProvider)
        {
            var client = serviceProvider.GetRequiredService<IClusterClient>();
            AsyncHelper.RunSync(client.Close);
        }
    }
}