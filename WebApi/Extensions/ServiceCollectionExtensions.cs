using System.Linq;
using System.Reflection;
using System.Web.Http.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace ConcurrencyTesting.WebApi.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterControllers<TAssemblyType>(this IServiceCollection services)
            => services.RegisterControllersInAssemblies(typeof(TAssemblyType).Assembly);

        public static IServiceCollection RegisterControllersInAssemblies(this IServiceCollection services,
            params Assembly[] assemblies)
            => assemblies
                .SelectMany(t => t.GetExportedTypes())
                .Where(typeof(IHttpController).IsAssignableFrom)
                .Aggregate(services, (collection, controller) => collection.AddScoped(controller));
    }
}
