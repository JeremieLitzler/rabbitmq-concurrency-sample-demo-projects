using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using MyCompany.Infrastructure.Messaging.Abstractions;
using MyCompany.Infrastructure.Messaging.RabbitMQ.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyCompany.Infrastructure.Messaging.Abstractions.Messages;

namespace MyCompany.Infrastructure.Messaging.RabbitMQ
{
	public class RabbitMQMessagingClient : IMessagingPublishClient, IMessagingConnectable, IDisposable
	{
		// public const string ConnectionFactoryOptionsName = "RabbitMQMessagingClient:ConnectionFactory";
		// public const string MessagePublishingOptionsName = "RabbitMQMessagingClient:MessagePublishOptions";

		private readonly string _instanceName;
		private readonly IConnectionFactory _connectionFactory;
		private readonly ILogger<RabbitMQMessagingClient> _logger;
		private readonly Dictionary<Type, RabbitMQMessagePublishingOptions> _messagePublishingOptions;

#if NET6_0_OR_GREATER
		private readonly ConcurrentDictionary<ulong, TaskCompletionSource> _pendingPublishers = new();
#else
		private readonly ConcurrentDictionary<ulong, TaskCompletionSource<object>> _pendingPublishers = new ConcurrentDictionary<ulong, TaskCompletionSource<object>>();
#endif

		private readonly JsonSerializerOptions _serializerOptions;

		private volatile bool _connected = false;
		private bool _disposed = false;
		private IConnection _connection;
		private readonly object _connectLock = new object();
		private IModel _model;

		// Serializes the steps that must not run concurrently on the shared channel: reading the next publish sequence number,
		// registering the pending confirm, and publishing.
		private readonly SemaphoreSlim _publishLock = new SemaphoreSlim(1, 1);

		public RabbitMQMessagingClient(
			string instanceName,
			IOptionsMonitor<ConnectionFactory> connectionFactoryOptions,
			ILogger<RabbitMQMessagingClient> logger,
			IOptionsMonitor<Dictionary<Type, RabbitMQMessagePublishingOptions>> messagePublishingOptions,
			IOptionsMonitor<JsonSerializerOptions> jsonSerializerOptions)
		{
			_connectionFactory = connectionFactoryOptions.Get($"{instanceName}:ConnectionFactory");
			_logger = logger;
			_messagePublishingOptions = messagePublishingOptions.Get($"{instanceName}:MessagePublishOptions");
			_serializerOptions = jsonSerializerOptions.Get(instanceName);
		}

		public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
		{
			EnsureConnected();

			await Policy
				.Handle<MessagingException>()
				.WaitAndRetryAsync(10, _ => TimeSpan.FromSeconds(2))
				.ExecuteAsync(
					action: (_, stoppingToken) => PublishAsyncInternal(message, _serializerOptions, stoppingToken),
					contextData: new Dictionary<string, object>
					{
						[nameof(ILogger)] = _logger
					},
					cancellationToken)
				.ConfigureAwait(false);
		}

		private async Task PublishAsyncInternal<TMessage>(TMessage message, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
		{
			if (_messagePublishingOptions.TryGetValue(typeof(TMessage), out RabbitMQMessagePublishingOptions publishingOptions))
			{
				IBasicProperties properties;
				ulong deliveryTag;
#if NET6_0_OR_GREATER
				TaskCompletionSource tcs = new();
#else
				TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
#endif

				// Reading the sequence number, registering the pending confirm and publishing happen as one unit.
				// Otherwise two concurrent publishers can read the same sequence number (reproduced in the demo projects),
				// and RabbitMQ's publishers guide says concurrent publishing on a shared channel is not supported by client libraries.
				// Waiting for the broker confirm happens outside the lock, so publishers are not serialized on the broker round trip.
				await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
				try
				{
					properties = _model.CreateBasicProperties();
					properties.Persistent = true;
					properties.CorrelationId = Guid.NewGuid().ToString();

					if (message is IPriorityMessage priorityMessage)
						properties.Priority = priorityMessage.Priority;

					deliveryTag = _model.NextPublishSeqNo;
					if (!_pendingPublishers.TryAdd(deliveryTag, tcs))
						throw new Exception($"Unable to add delivery tag {deliveryTag} to the list of pending publishers.");

					try
					{
						_model.BasicPublish(
							exchange: publishingOptions.ExchangeName,
							routingKey: publishingOptions.RoutingKey,
							mandatory: true,
							basicProperties: properties,
							body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, serializerOptions)
						));
					}
					catch
					{
						// Do not leave a pending confirm that will never arrive: the next publisher would get the same sequence number.
						_pendingPublishers.TryRemove(deliveryTag, out _);
						throw;
					}
				}
				finally
				{
					_publishLock.Release();
				}

				using (_logger.BeginScope(new Dictionary<string, object>
				{
					["CorrelationId"] = properties.CorrelationId,
					["DeliveryTag"] = deliveryTag,
					["MessageType"] = typeof(TMessage).Name
				}))
				{
					_logger.LogTrace("Publishing message of type {messageType} with correlationId={correlationId} and delivery tag={deliveryTag}...", typeof(TMessage).Name, properties.CorrelationId, deliveryTag);
					await tcs.Task.ConfigureAwait(false);
					_logger.LogTrace("Message of type {messageType} with correlationId={correlationId} and delivery tag={deliveryTag} published successfully.", typeof(TMessage).Name, properties.CorrelationId, deliveryTag);
				}
			}
			else
				throw new InvalidOperationException($"No publishing options have been setuped for messages of type {typeof(TMessage).Name}.");
		}

		/// <summary>
		/// Opens the connection and the publishing channel if they are not open yet, without blocking the calling thread.
		/// </summary>
		public Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			return Task.Run(EnsureConnected, cancellationToken);
		}

		/// <summary>
		/// Closes the publishing channel and the connection.
		/// </summary>
		public void Dispose()
		{
			lock (_connectLock)
			{
				if (_disposed)
					return;

				_disposed = true;
				_connected = false;

				try
				{
					_model?.Close();
					_connection?.Close();
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error while closing the RabbitMQ connection.");
				}
				finally
				{
					_model?.Dispose();
					_connection?.Dispose();
				}
			}
		}

		private void EnsureConnected()
		{
			if (_connected)
				return;

			lock (_connectLock)
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RabbitMQMessagingClient));

				ConnectCore();
			}
		}

		private void ConnectCore()
		{
			if (_connected)
				return;

			IConnection connection = Policy
				.Handle<BrokerUnreachableException>()
				.WaitAndRetry(5, (_, ctx) => TimeSpan.FromSeconds(2), (ex, _, retryCount, ctx) =>
				{
					ILogger logger = (ILogger)ctx[nameof(ILogger)];
					logger.LogWarning(ex,
						"Unable to connect to the RabbitMQ broker. Trying reconnect... ({retryCount} times)",
						retryCount);
				})
				.Execute(_ => _connectionFactory.CreateConnection(), new Dictionary<string, object>
				{
					[nameof(ILogger)] = _logger
				});

			_connection = connection;
			_model = connection.CreateModel();
			_model.ConfirmSelect();
			_model.BasicAcks += (sender, e) => OnMessageConfirm(e.DeliveryTag, e.Multiple, true);
			_model.BasicNacks += (sender, e) => OnMessageConfirm(e.DeliveryTag, e.Multiple, false);
			_model.ModelShutdown += OnModelShutdown;
			_connected = true;
		}

		private void OnModelShutdown(object sender, ShutdownEventArgs e)
		{
			if (_disposed)
				return;

			_logger.LogError("Channel closed unexpectedly by {initiator} with reason: {reason}.", e.Initiator, e.ReplyText);
			foreach (var pair in _pendingPublishers)
			{
				pair.Value.SetException(new Exception("Publishing failed due to channel exception."));
			}
		}

		private void OnMessageConfirm(ulong sequenceNumber, bool multiple, bool confirm)
		{
			if (multiple)
			{
				var confirmedPublishers = _pendingPublishers.Where(p => p.Key <= sequenceNumber).ToList();
				foreach (var entry in confirmedPublishers)
				{
#if NET6_0_OR_GREATER
					if (_pendingPublishers.TryRemove(entry.Key, out TaskCompletionSource? tcs))
#else
					if (_pendingPublishers.TryRemove(entry.Key, out TaskCompletionSource<object> tcs))
#endif
					{
						if (confirm)
#if NET6_0_OR_GREATER
							tcs.SetResult();
#else
							tcs.SetResult(null);
#endif

						else
							tcs.SetException(new MessagingException("Message could not be sent to the broker."));
					}
				}
			}
			else
			{
#if NET6_0_OR_GREATER
				if (_pendingPublishers.TryRemove(sequenceNumber, out TaskCompletionSource? tcs))
#else
				if (_pendingPublishers.TryRemove(sequenceNumber, out TaskCompletionSource<object> tcs))
#endif
				{
					if (confirm)
#if NET6_0_OR_GREATER
						tcs.SetResult();
#else
						tcs.SetResult(null);
#endif
					else
						tcs.SetException(new MessagingException("Message could not be sent to the broker."));
				}
			}
		}
	}
}
