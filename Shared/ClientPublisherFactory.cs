// Linked into both Publisher.V1 and Publisher.V2 (the V1 constant picks the namespace).
// Builds the real RabbitMQMessagingClient with hard-coded options and no real logger.
using System;
using System.Collections.Generic;
using System.Text.Json;
using ConcurrencyTesting.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyCompany.Infrastructure.Messaging.RabbitMQ;
using MyCompany.Infrastructure.Messaging.RabbitMQ.Options;
using RabbitMQ.Client;

#if V1
namespace ConcurrencyTesting.Publisher.V1
#else
namespace ConcurrencyTesting.Publisher.V2
#endif
{
    public static class ClientPublisherFactory
    {
        private const string InstanceName = "Internal";

        public static IStringPublisher Create(PublisherSettings settings)
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                VirtualHost = settings.VirtualHost,
                UserName = settings.UserName,
                Password = settings.Password
            };

            var publishOptions = new Dictionary<Type, RabbitMQMessagePublishingOptions>
            {
                [typeof(string)] = new RabbitMQMessagePublishingOptions
                {
                    ExchangeName = settings.Exchange,
                    RoutingKey = settings.RoutingKey
                }
            };

            var client = new RabbitMQMessagingClient(
                InstanceName,
                new FixedOptionsMonitor<ConnectionFactory>(connectionFactory),
                NullLogger<RabbitMQMessagingClient>.Instance,
                new FixedOptionsMonitor<Dictionary<Type, RabbitMQMessagePublishingOptions>>(publishOptions),
                new FixedOptionsMonitor<JsonSerializerOptions>(new JsonSerializerOptions()));

            return new MessagingClientPublisher(client, client, client);
        }

        private sealed class FixedOptionsMonitor<T> : IOptionsMonitor<T>
        {
            private readonly T _value;
            public FixedOptionsMonitor(T value) { _value = value; }
            public T CurrentValue => _value;
            public T Get(string name) => _value;
            public IDisposable OnChange(Action<T, string> listener) => null;
        }
    }
}
