using System;
using System.Threading.Tasks;

namespace ConcurrencyTesting.Contracts
{
    /// <summary>A minimal publisher that sends a string and completes when the broker confirms it.</summary>
    public interface IStringPublisher : IDisposable
    {
        /// <summary>Opens the connection and channel without publishing.</summary>
        Task ConnectAsync();

        /// <summary>Publishes the message and completes when the broker acks it.</summary>
        Task PublishAsync(string message);
    }
}
