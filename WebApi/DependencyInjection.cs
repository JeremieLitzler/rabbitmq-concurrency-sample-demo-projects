using System;
using System.Collections.Generic;
using System.Web.Http.Dependencies;
using ConcurrencyTesting.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ConcurrencyTesting.WebApi
{
    // Distinct types so the controller constructor can ask for each version (both implement the same IStringPublisher).
    public sealed class V1Publisher : IStringPublisher
    {
        private readonly IStringPublisher _inner;
        public V1Publisher(IStringPublisher inner) { _inner = inner; }
        public System.Threading.Tasks.Task ConnectAsync() => _inner.ConnectAsync();
        public System.Threading.Tasks.Task PublishAsync(string message) => _inner.PublishAsync(message);
        public void Dispose() => _inner.Dispose();
    }

    public sealed class V2Publisher : IStringPublisher
    {
        private readonly IStringPublisher _inner;
        public V2Publisher(IStringPublisher inner) { _inner = inner; }
        public System.Threading.Tasks.Task ConnectAsync() => _inner.ConnectAsync();
        public System.Threading.Tasks.Task PublishAsync(string message) => _inner.PublishAsync(message);
        public void Dispose() => _inner.Dispose();
    }

    /// <summary>Lets Web API 2 resolve controllers (and their constructor dependencies) from Microsoft.Extensions.DependencyInjection.</summary>
    public sealed class ServiceProviderResolver : IDependencyResolver
    {
        private readonly IServiceProvider _provider;
        private readonly IServiceScope _scope;

        public ServiceProviderResolver(IServiceProvider provider, IServiceScope scope = null)
        {
            _provider = provider;
            _scope = scope;
        }

        public object GetService(Type serviceType) => _provider.GetService(serviceType);

        public IEnumerable<object> GetServices(Type serviceType) => _provider.GetServices(serviceType);

        public IDependencyScope BeginScope()
        {
            IServiceScope scope = _provider.CreateScope();
            return new ServiceProviderResolver(scope.ServiceProvider, scope);
        }

        public void Dispose() => _scope?.Dispose();
    }
}
