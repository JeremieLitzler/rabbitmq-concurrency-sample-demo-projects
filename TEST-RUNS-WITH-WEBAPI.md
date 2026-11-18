# Test runs with the Web API

These runs repeat the `TEST-RUNS.md` scenarios, but the publishers sit behind a real HTTP endpoint instead of being called in-process. The `WebApi` project (ASP.NET Web API 2, OWIN self-host, .NET Framework 4.8.1 target, reported as `.NET Framework 4.8.10008.0`) exposes one endpoint per publisher version. The `ConsoleRunner` (net8.0 build, `.NET 8.0.34`) is the load generator: it sends one HTTP request per message, and the existing thread-per-call gate and spread logic decide when each request is sent.

## Setup

- Start the server: `WebApi\bin\Debug\net481\ConcurrencyTesting.WebApi.exe [baseUrl]` (default `http://localhost:5080/`)
- Run the load: `ConsoleRunner <v1|v2> <count> <preconnect|lazy> <spreadMs> http://localhost:5080/`
- Endpoints: `POST api/v1/publish` and `POST api/v2/publish` (JSON string body), plus `POST api/v1/connect` and `POST api/v2/connect` (the preconnect mode)
- Dependency injection: `Startup.cs` registers one singleton publisher per version (`V1Publisher`, `V2Publisher`) in `Microsoft.Extensions.DependencyInjection`, and `PublishController` takes both in its constructor through an `IDependencyResolver` adapter (`WebApi/DependencyInjection.cs`). Web API 2 has no `AddControllers()`, so `serviceCollection.RegisterControllers<Startup>()` (an extension method in `WebApi/Extensions/ServiceCollectionExtensions.cs`, copied from production) registers every `IHttpController` of the assembly as scoped
- Random delay: each `publish` call waits a random 0 to 199 ms before publishing, to simulate request work
- Fresh client per run: the WebApi process is stopped and started again before every run, so the singleton publisher is new and the server is cold (first requests include JIT and thread pool warm-up)
- Failures: the endpoint answers 500 with the exception text; the runner counts each 500 as failed
- Broker settings are the same as `TEST-RUNS.md`: virtual host `winform-rabbit-tests`, exchange `test-exchange`, routing key `test-routing-key`, `guest`/`guest`, `RabbitMQ.Client` 6.8.1, 30 s timeout
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100. Each count ran once per scenario
- Elapsed includes the random 0 to 199 ms endpoint delay and, on a cold server, the start-up cost of the first request

Every failure in the tables below is `Unable to add delivery tag N to the list of pending publishers.` (494 failures in total, all on V1). Hung requests only appear in the lazy-connect runs.

## V1 Publisher

### Run 1: V1, lazy connect, all at once

- Implementation: V1 (no publish lock)
- Connection: lazy, so the first requests also race through the connect
- Arrival: all requests released at the same instant (spread 0)
- Command: `ConsoleRunner v1 <count> lazy 0 http://localhost:5080/`
- From 2 requests up, only 1 message is confirmed and the others fail on a delivery tag, as in the in-process Console run
- From 13 requests up, elapsed grows by about 0.75 s per extra request, and from 55 requests up every request hung (see the thread pool note below)

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 661          |
| 2         | 1         | 1      | 0    | 647          |
| 3         | 1         | 2      | 0    | 598          |
| 5         | 1         | 4      | 0    | 657          |
| 8         | 1         | 7      | 0    | 638          |
| 13        | 1         | 12     | 0    | 6353         |
| 21        | 1         | 20     | 0    | 15874        |
| 34        | 1         | 33     | 0    | 25409        |
| 55        | 0         | 0      | 55   | 30002        |
| 89        | 0         | 0      | 89   | 30004        |
| 100       | 0         | 0      | 100  | 29994        |

### Run 2: V1, pre-connected, all at once

- Implementation: V1 (no publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: all requests released at the same instant (spread 0)
- Command: `ConsoleRunner v1 <count> preconnect 0 http://localhost:5080/`
- No hangs and fast runs; failures from 2 requests up, 89 of 100 lost at the top size

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 303          |
| 2         | 1         | 1      | 0    | 412          |
| 3         | 1         | 2      | 0    | 381          |
| 5         | 1         | 4      | 0    | 396          |
| 8         | 1         | 7      | 0    | 464          |
| 13        | 1         | 12     | 0    | 327          |
| 21        | 4         | 17     | 0    | 382          |
| 34        | 7         | 27     | 0    | 380          |
| 55        | 11        | 44     | 0    | 357          |
| 89        | 8         | 81     | 0    | 346          |
| 100       | 11        | 89     | 0    | 348          |

### Run 3: V1, pre-connected, spread up to 1 s

- Implementation: V1 (no publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: random delay between 0 and 1000 ms per request, plus the 0 to 199 ms endpoint delay
- Command: `ConsoleRunner v1 <count> preconnect 1000 http://localhost:5080/`
- Failures start at 13 requests and grow with load: 50 of 100 lost at the top size (the in-process Console run lost 40 of 100 in the same scenario)
- Each size ran once, so small-count differences are noise

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 436          |
| 2         | 2         | 0      | 0    | 651          |
| 3         | 3         | 0      | 0    | 724          |
| 5         | 5         | 0      | 0    | 1038         |
| 8         | 8         | 0      | 0    | 937          |
| 13        | 7         | 6      | 0    | 948          |
| 21        | 16        | 5      | 0    | 1180         |
| 34        | 23        | 11     | 0    | 1114         |
| 55        | 40        | 15     | 0    | 1076         |
| 89        | 45        | 44     | 0    | 1142         |
| 100       | 50        | 50     | 0    | 1192         |

## V2 Publisher

### Run 1: V2, lazy connect, all at once

- Implementation: V2 (publish lock)
- Connection: lazy
- Arrival: all requests released at the same instant (spread 0)
- Command: `ConsoleRunner v2 <count> lazy 0 http://localhost:5080/`
- No message is lost up to 34 requests, but the timing is as bad as V1 Run 1: elapsed grows by about 0.75 s per extra request from 13 requests up, and from 55 requests up every request hung

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 726          |
| 2         | 2         | 0      | 0    | 754          |
| 3         | 3         | 0      | 0    | 725          |
| 5         | 5         | 0      | 0    | 694          |
| 8         | 8         | 0      | 0    | 681          |
| 13        | 13        | 0      | 0    | 5833         |
| 21        | 21        | 0      | 0    | 14829        |
| 34        | 34        | 0      | 0    | 25312        |
| 55        | 0         | 0      | 55   | 30004        |
| 89        | 0         | 0      | 89   | 30014        |
| 100       | 0         | 0      | 100  | 29991        |

### Run 2: V2, pre-connected, all at once

- Implementation: V2 (publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: all requests released at the same instant (spread 0)
- Command: `ConsoleRunner v2 <count> preconnect 0 http://localhost:5080/`
- All messages confirmed at every size

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 342          |
| 2         | 2         | 0      | 0    | 306          |
| 3         | 3         | 0      | 0    | 341          |
| 5         | 5         | 0      | 0    | 307          |
| 8         | 8         | 0      | 0    | 343          |
| 13        | 13        | 0      | 0    | 541          |
| 21        | 21        | 0      | 0    | 1005         |
| 34        | 34        | 0      | 0    | 603          |
| 55        | 55        | 0      | 0    | 363          |
| 89        | 89        | 0      | 0    | 330          |
| 100       | 100       | 0      | 0    | 319          |

### Run 3: V2, pre-connected, spread up to 1 s

- Implementation: V2 (publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: random delay between 0 and 1000 ms per request, plus the 0 to 199 ms endpoint delay
- Command: `ConsoleRunner v2 <count> preconnect 1000 http://localhost:5080/`
- All messages confirmed at every size, and the lock adds no visible slowdown: elapsed is dominated by the spread delay, as with the in-process runs

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 968          |
| 2         | 2         | 0      | 0    | 380          |
| 3         | 3         | 0      | 0    | 1064         |
| 5         | 5         | 0      | 0    | 1001         |
| 8         | 8         | 0      | 0    | 968          |
| 13        | 13        | 0      | 0    | 1175         |
| 21        | 21        | 0      | 0    | 1147         |
| 34        | 34        | 0      | 0    | 1136         |
| 55        | 55        | 0      | 0    | 1091         |
| 89        | 89        | 0      | 0    | 1150         |
| 100       | 100       | 0      | 0    | 1206         |

## Conclusion

- V1 loses messages as soon as requests overlap, in every scenario (from 38 to 89 of 100 at the top size across the two runs of this study).
- V2 confirmed every message in the pre-connected runs (22 of 22). In the lazy-connect runs it confirmed every message up to 34 requests and hung from 55 requests up, so V2 does not protect a cold lazy-connect server under burst load.
- Probable cause of the lazy-connect slowdown and hangs (not verified): the first publish connects through `Task.Run(EnsureConnected)`, which blocks on a `lock (_connectLock)`. A burst of requests on a cold server parks thread pool threads on that lock, and the pool only adds about one thread per 0.5 to 1 s, which matches the roughly 0.75 s per extra request. Setting a higher `ThreadPool.SetMinThreads` or connecting at start-up should show it, but I did not test either.
- The first version of this study (publisher created in the controller, warm server, `reset` endpoint between runs) showed less severe numbers: V1 lost 38 to 54 of 100, and V2 hung once in lazy mode (89 requests, not reproduced in 9 repeats). The cold-server protocol above replaced it because it mirrors the DI singleton wiring.
- The progressive load (counts 1 to 100) is a step-by-step ramp of independent runs, not one continuous ramp.
