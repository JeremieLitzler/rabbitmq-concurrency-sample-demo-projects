using System.Web.Http;
using ConcurrencyTesting.Contracts;
using ConcurrencyTesting.WebApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Owin;

namespace ConcurrencyTesting.WebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(new PublisherSettings());
            serviceCollection.AddSingleton(sp => new V1Publisher(Publisher.V1.ClientPublisherFactory.Create(sp.GetRequiredService<PublisherSettings>())));
            serviceCollection.AddSingleton(sp => new V2Publisher(Publisher.V2.ClientPublisherFactory.Create(sp.GetRequiredService<PublisherSettings>())));
            serviceCollection.RegisterControllers<Startup>();

            var config = new HttpConfiguration { DependencyResolver = new ServiceProviderResolver(serviceCollection.BuildServiceProvider()) };
            config.MapHttpAttributeRoutes();
            app.UseWebApi(config);
        }
    }
}
