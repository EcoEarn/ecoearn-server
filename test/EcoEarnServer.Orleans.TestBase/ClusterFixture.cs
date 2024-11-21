using System;
using System.Linq;
using AutoMapper;
using EcoEarnServer.Grains;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Orleans.TestingHost;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Reflection;

namespace EcoEarnServer;

public class ClusterFixture : IDisposable, ISingletonDependency
{
    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurations>();
        // builder.AddClientBuilderConfigurator<TestClientBuilderConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

    public TestCluster Cluster { get; private set; }


private class TestSiloConfigurations : ISiloConfigurator, IHostConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.ConfigureServices(services =>
        {
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddAutoMapper(typeof(EcoEarnServerGrainsModule).Assembly);
            services.AddSingleton(typeof(IDistributedCache), typeof(MemoryDistributedCache));
            services.AddSingleton(typeof(IDistributedCache<,>), typeof(DistributedCache<,>));

            services.Configure<AbpDistributedCacheOptions>(cacheOptions =>
            {
                cacheOptions.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(20);
            });
            services.OnExposing(onServiceExposingContext =>
            {
                var implementedTypes = ReflectionHelper.GetImplementedGenericTypes(
                    onServiceExposingContext.ImplementationType,
                    typeof(IObjectMapper<,>)
                );

                var serviceIdentifiers = implementedTypes.Select(type => new ServiceIdentifier(type)).ToList();

                onServiceExposingContext.ExposedTypes.AddRange(serviceIdentifiers);
            });

            services.AddTransient(typeof(IObjectMapper<>), typeof(DefaultObjectMapper<>));
            services.AddTransient(typeof(IObjectMapper), typeof(DefaultObjectMapper));
            services.AddTransient(typeof(IAutoObjectMappingProvider),
                typeof(AutoMapperAutoObjectMappingProvider));

            services.AddTransient(sp => new MapperAccessor()
            {
                Mapper = sp.GetRequiredService<IMapper>()
            });

            services.AddTransient<IMapperAccessor>(provider => provider.GetRequiredService<MapperAccessor>());
        });

        siloBuilder.AddMemoryGrainStorage("PubSubStore");
        siloBuilder.AddMemoryGrainStorageAsDefault();
    }

    public void Configure(IHostBuilder hostBuilder)
    {
    }
}


    public class MapperAccessor : IMapperAccessor
    {
        public IMapper Mapper { get; set; }
    }
}