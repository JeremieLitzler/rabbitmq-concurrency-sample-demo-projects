using System.Threading;
using System.Threading.Tasks;

namespace MyCompany.Infrastructure.Messaging.Abstractions
{
	/// <summary>
	/// A messaging client that can open its broker connection on demand, so that an application can fail fast at startup
	/// instead of discovering an unreachable broker on the first publish.
	/// </summary>
	public interface IMessagingConnectable
	{
		/// <summary>
		/// Opens the connection and the publishing channel if they are not open yet. Safe to call several times and from several threads.
		/// </summary>
		Task ConnectAsync(CancellationToken cancellationToken = default);
	}
}
