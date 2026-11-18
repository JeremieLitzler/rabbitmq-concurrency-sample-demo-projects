// Minimal stand-ins for the types the shared client needs that live elsewhere in the production solution.
using System;

namespace MyCompany.Infrastructure.Messaging.Abstractions
{
    public class MessagingException : Exception
    {
        public MessagingException(string message) : base(message) { }
    }
}

namespace MyCompany.Infrastructure.Messaging.Abstractions.Messages
{
    public interface IPriorityMessage
    {
        byte Priority { get; }
    }
}

namespace MyCompany.Infrastructure.Messaging.RabbitMQ.Options
{
    public class RabbitMQMessagePublishingOptions
    {
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
    }
}
