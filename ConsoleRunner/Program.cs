using System;
using System.Threading.Tasks;
using ConcurrencyTesting.Contracts;

// Usage: ConsoleRunner [v1|v2|both] [count] [preconnect|lazy] [spreadMs] [webApiBaseUrl]
// Without webApiBaseUrl: calls the libraries in-process against the local broker.
// With it (e.g. http://localhost:5080/): sends each message as an HTTP request to the WebApi project instead.
string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "both";
int count = args.Length > 1 ? int.Parse(args[1]) : 100;
bool preConnect = args.Length > 2 && args[2].Equals("preconnect", StringComparison.OrdinalIgnoreCase);
int spreadMs = args.Length > 3 ? int.Parse(args[3]) : 0; // 0 = all at the same instant
string webApiUrl = args.Length > 4 ? args[4] : null;
var settings = new PublisherSettings();
var timeout = TimeSpan.FromSeconds(30);

var runs = new System.Collections.Generic.List<Task<RunResult>>();
if (mode == "v1" || mode == "both")
    runs.Add(ConcurrencyRunner.RunAsync("V1", () => webApiUrl != null ? new ConcurrencyTesting.ConsoleRunner.HttpStringPublisher(webApiUrl, "v1") : ConcurrencyTesting.Publisher.V1.ClientPublisherFactory.Create(settings), count, timeout, preConnect, spreadMs));
if (mode == "v2" || mode == "both")
    runs.Add(ConcurrencyRunner.RunAsync("V2", () => webApiUrl != null ? new ConcurrencyTesting.ConsoleRunner.HttpStringPublisher(webApiUrl, "v2") : ConcurrencyTesting.Publisher.V2.ClientPublisherFactory.Create(settings), count, timeout, preConnect, spreadMs));

foreach (RunResult result in await Task.WhenAll(runs))
    Console.Write(result);
