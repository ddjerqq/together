﻿using System.Reflection;
using Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public abstract class ConfigurationBase
{
    public abstract void ConfigureServices(IServiceCollection services);

    protected static bool IsDevelopment => "ASPNETCORE_ENVIRONMENT".FromEnv("Development") == "Development";

    /// <summary>
    /// Configures the configurations from all the assembly names.
    /// </summary>
    public static void ConfigureServicesFromAssemblies(IServiceCollection services, IEnumerable<string> assemblies)
        => ConfigureServicesFromAssemblies(services, assemblies.Select(Assembly.Load));

    /// <summary>
    /// Configures the configurations from all the assemblies and configuration types.
    /// </summary>
    public static void ConfigureServicesFromAssemblies(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(ConfigurationBase).IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Select(type => (ConfigurationBase)Activator.CreateInstance(type)!)
            .ToList()
            .ForEach(hostingStartup =>
            {
                var name = hostingStartup.GetType().Name.Replace("Configure", "");
                Console.WriteLine($"[{DateTime.UtcNow:s}.000 INF] Configured {name}");
                hostingStartup.ConfigureServices(services);
            });
    }
}

public static class ConfigExt
{
    /// <summary>
    /// Configures the configurations from all the assemblies and configuration types.
    /// </summary>
    public static void ConfigureAssemblies(this IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(ConfigurationBase).IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Select(type => (ConfigurationBase)Activator.CreateInstance(type)!)
            .ToList()
            .ForEach(hostingStartup =>
            {
                var name = hostingStartup.GetType().Name.Replace("Configure", "");
                Console.WriteLine($"[{DateTime.UtcNow:s}.000 INF] Configured {name}");
                hostingStartup.ConfigureServices(services);
            });
    }
}