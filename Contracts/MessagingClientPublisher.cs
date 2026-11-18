using System;
using System.Threading.Tasks;
using MyCompany.Infrastructure.Messaging.Abstractions;

namespace ConcurrencyTesting.Contracts
{
    /// <summary>Adapts the real shared client (IMessagingPublishClient + IMessagingConnectable) to the test runner.</summary>
    public sealed class MessagingClientPublisher : IStringPublisher
    {
        private readonly IMessagingPublishClient _publishClient;
        private readonly IMessagingConnectable _connectable;
        private readonly IDisposable _disposable;

        public MessagingClientPublisher(IMessagingPublishClient publishClient, IMessagingConnectable connectable, IDisposable disposable)
        {
            _publishClient = publishClient;
            _connectable = connectable;
            _disposable = disposable;
        }

        public Task ConnectAsync() => _connectable.ConnectAsync();

        public Task PublishAsync(string message) => _publishClient.PublishAsync(message);

        public void Dispose() => _disposable.Dispose();
    }
}
