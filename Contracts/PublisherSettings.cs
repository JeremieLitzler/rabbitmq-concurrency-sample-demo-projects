namespace ConcurrencyTesting.Contracts
{
    public class PublisherSettings
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "winform-rabbit-tests";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string Exchange { get; set; } = "test-exchange";
        public string RoutingKey { get; set; } = "test-routing-key";
    }
}
