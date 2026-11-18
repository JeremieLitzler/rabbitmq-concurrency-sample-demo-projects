using System.Threading;
using System.Threading.Tasks;

namespace MyCompany.Infrastructure.Messaging.Abstractions
{
	public interface IMessagingPublishClient
	{
		Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default);
	}
	
	// Types of IMessagingPublishClient used. One by RabbitMQ.
	public interface IInternalMessagingPublishClient : IMessagingPublishClient { }
	public interface IExternalMessagingPublishClient : IMessagingPublishClient { }
	
}