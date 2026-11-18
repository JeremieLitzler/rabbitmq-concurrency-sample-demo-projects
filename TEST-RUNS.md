# Test runs

Runs are numbered separately under each publisher. V1 runs 1 to 4 and V2 runs 1 to 3 used the net8.0 `ConsoleRunner` (runtime `.NET 8.0.34`); V1 runs 5 to 7 and V2 runs 4 to 6 used its net472 build (reported as `.NET Framework 4.8.10008.0`). All ran against a local RabbitMQ broker. Each message count ran once, with a fresh client per run. V1 is the original client (no publish lock). V2 is the client with the `SemaphoreSlim` publish lock.

Common setup: virtual host `winform-rabbit-tests`, exchange `test-exchange`, routing key `test-routing-key`, `guest`/`guest`, one thread per publish, 30 s timeout, `RabbitMQ.Client` 6.8.1. The "failed" counts below are all `Unable to add delivery tag N to the list of pending publishers.` and no run had hung calls.

## V1 Publisher

### Run 1: V1, lazy connect, all at once

- Implementation: V1 (no publish lock)
- Connection: lazy, so the first calls also race through the connect
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner v1 <count>`
- Failure message: always `delivery tag 1`

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 125          |
| 2         | 1         | 1      | 0    | 117          |
| 3         | 1         | 2      | 0    | 120          |
| 5         | 1         | 4      | 0    | 112          |
| 8         | 1         | 7      | 0    | 107          |
| 13        | 1         | 12     | 0    | 105          |
| 21        | 1         | 20     | 0    | 105          |
| 34        | 1         | 33     | 0    | 106          |
| 55        | 1         | 54     | 0    | 112          |
| 89        | 1         | 88     | 0    | 112          |
| 100       | 1         | 99     | 0    | 107          |

### Run 2: V1, pre-connected, all at once

- Implementation: V1 (no publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner v1 <count> preconnect`
- Result: pre-connecting did not change the outcome compared with V1 Run 1

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 48           |
| 2         | 1         | 1      | 0    | 32           |
| 3         | 1         | 2      | 0    | 31           |
| 5         | 1         | 4      | 0    | 31           |
| 8         | 1         | 7      | 0    | 31           |
| 13        | 1         | 12     | 0    | 42           |
| 21        | 1         | 20     | 0    | 31           |
| 34        | 1         | 33     | 0    | 41           |
| 55        | 1         | 54     | 0    | 33           |
| 89        | 1         | 88     | 0    | 34           |
| 100       | 1         | 99     | 0    | 35           |

### Run 3: V1, pre-connected, 100 messages, different spreads (smoke test of the spread mode)

- Implementation: V1 (no publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: each thread waits a random delay between 0 and the spread value after the gate opens
- Message count: 100
- Command: `ConsoleRunner v1 100 preconnect <spreadMs>`
- Caveat: `Thread.Sleep` wakes threads in clusters about 15 ms apart on Windows, so the arrivals are not uniform and V1 probably fails more than with truly spread traffic (not confirmed)

| Spread (ms) | Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| ----------- | --------- | --------- | ------ | ---- | ------------ |
| 0           | 100       | 1         | 99     | 0    | 48           |
| 20          | 100       | 1         | 99     | 0    | 39           |
| 100         | 100       | 55        | 45     | 0    | 119          |
| 500         | 100       | 45        | 55     | 0    | 516          |

### Run 4: V1, pre-connected, spread up to 1 s

- Implementation: V1 (no publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: random delay between 0 and 1000 ms per thread
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner v1 <count> preconnect 1000`
- Failures appear from 2 messages up and grow with load: 40 of 100 lost at the top size
- Each size ran once, so the small-count results (for example 2 failed, 3 passed) are noise

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 134          |
| 2         | 1         | 1      | 0    | 731          |
| 3         | 3         | 0      | 0    | 791          |
| 5         | 5         | 0      | 0    | 1008         |
| 8         | 7         | 1      | 0    | 707          |
| 13        | 11        | 2      | 0    | 991          |
| 21        | 20        | 1      | 0    | 1011         |
| 34        | 26        | 8      | 0    | 996          |
| 55        | 36        | 19     | 0    | 1008         |
| 89        | 49        | 40     | 0    | 992          |
| 100       | 60        | 40     | 0    | 976          |

### Run 5: V1 on .NET Framework 4.7.2 build, lazy connect, all at once

- Implementation: V1 (no publish lock), net472 build
- Runtime reported: `.NET Framework 4.8.10008.0`
- Connection: lazy
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner.exe v1 <count> lazy 0`
- Same outcome as V1 Run 1 (net8.0): one message confirmed per run, all failures on `delivery tag 1`

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 292          |
| 2         | 1         | 1      | 0    | 281          |
| 3         | 1         | 2      | 0    | 268          |
| 5         | 1         | 4      | 0    | 267          |
| 8         | 1         | 7      | 0    | 270          |
| 13        | 1         | 12     | 0    | 269          |
| 21        | 1         | 20     | 0    | 282          |
| 34        | 1         | 33     | 0    | 304          |
| 55        | 1         | 54     | 0    | 320          |
| 89        | 1         | 88     | 0    | 411          |
| 100       | 1         | 99     | 0    | 416          |

### Run 6: V1 on .NET Framework 4.7.2 build, pre-connected, all at once

- Implementation: V1 (no publish lock), net472 build
- Runtime reported: `.NET Framework 4.8.10008.0`
- Connection: opened before publishing (`preconnect`)
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner.exe v1 <count> preconnect 0`
- Same outcome as V1 Run 2 (net8.0): one message confirmed per run, all failures on `delivery tag 1`

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 121          |
| 2         | 1         | 1      | 0    | 117          |
| 3         | 1         | 2      | 0    | 117          |
| 5         | 1         | 4      | 0    | 118          |
| 8         | 1         | 7      | 0    | 117          |
| 13        | 1         | 12     | 0    | 118          |
| 21        | 1         | 20     | 0    | 127          |
| 34        | 1         | 33     | 0    | 144          |
| 55        | 1         | 54     | 0    | 158          |
| 89        | 1         | 88     | 0    | 161          |
| 100       | 1         | 99     | 0    | 175          |

### Run 7: V1 on .NET Framework 4.7.2 build, pre-connected, spread up to 1 s

- Implementation: V1 (no publish lock), net472 build
- Runtime reported: `.NET Framework 4.8.10008.0`
- Connection: opened before publishing (`preconnect`)
- Arrival: random delay between 0 and 1000 ms per thread
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner.exe v1 <count> preconnect 1000`
- Same pattern as V1 Run 4 (net8.0): failures from 3 messages up, growing with load, 41 of 100 lost at the top size
- Each size ran once, so the small-count results are noise

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 343          |
| 2         | 2         | 0      | 0    | 925          |
| 3         | 2         | 1      | 0    | 890          |
| 5         | 5         | 0      | 0    | 844          |
| 8         | 7         | 1      | 0    | 917          |
| 13        | 12        | 1      | 0    | 933          |
| 21        | 15        | 6      | 0    | 990          |
| 34        | 26        | 8      | 0    | 1010         |
| 55        | 41        | 14     | 0    | 985          |
| 89        | 59        | 30     | 0    | 1004         |
| 100       | 59        | 41     | 0    | 1039         |

## V2 Publisher

### Run 1: V2, lazy connect, all at once

- Implementation: V2 (publish lock)
- Connection: lazy
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner v2 <count>`

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 144          |
| 2         | 2         | 0      | 0    | 123          |
| 3         | 3         | 0      | 0    | 138          |
| 5         | 5         | 0      | 0    | 127          |
| 8         | 8         | 0      | 0    | 132          |
| 13        | 13        | 0      | 0    | 119          |
| 21        | 21        | 0      | 0    | 119          |
| 34        | 34        | 0      | 0    | 116          |
| 55        | 55        | 0      | 0    | 132          |
| 89        | 89        | 0      | 0    | 121          |
| 100       | 100       | 0      | 0    | 119          |

### Run 2: V2, pre-connected, all at once

- Implementation: V2 (publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner v2 <count> preconnect`

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 32           |
| 2         | 2         | 0      | 0    | 35           |
| 3         | 3         | 0      | 0    | 40           |
| 5         | 5         | 0      | 0    | 39           |
| 8         | 8         | 0      | 0    | 42           |
| 13        | 13        | 0      | 0    | 45           |
| 21        | 21        | 0      | 0    | 45           |
| 34        | 34        | 0      | 0    | 46           |
| 55        | 55        | 0      | 0    | 47           |
| 89        | 89        | 0      | 0    | 47           |
| 100       | 100       | 0      | 0    | 49           |

### Run 3: V2, pre-connected, spread up to 1 s

- Implementation: V2 (publish lock)
- Connection: opened before publishing (`preconnect`)
- Arrival: random delay between 0 and 1000 ms per thread
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner v2 <count> preconnect 1000`
- Elapsed is dominated by the spread delay, so the lock adds no visible slowdown

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 184          |
| 2         | 2         | 0      | 0    | 478          |
| 3         | 3         | 0      | 0    | 867          |
| 5         | 5         | 0      | 0    | 1005         |
| 8         | 8         | 0      | 0    | 961          |
| 13        | 13        | 0      | 0    | 1007         |
| 21        | 21        | 0      | 0    | 968          |
| 34        | 34        | 0      | 0    | 965          |
| 55        | 55        | 0      | 0    | 993          |
| 89        | 89        | 0      | 0    | 1015         |
| 100       | 100       | 0      | 0    | 1010         |

### Run 4: V2 on .NET Framework 4.7.2 build, lazy connect, all at once

- Implementation: V2 (publish lock), net472 build of the libraries and of `ConsoleRunner`
- Runtime reported: `.NET Framework 4.8.10008.0` (the net472 build ran on the installed .NET Framework 4.8 runtime, not on a 4.7.2 install)
- Connection: lazy
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner.exe v2 <count> lazy 0`
- Same outcome as V2 Run 1 (net8.0): every message confirmed

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 739          |
| 2         | 2         | 0      | 0    | 270          |
| 3         | 3         | 0      | 0    | 273          |
| 5         | 5         | 0      | 0    | 273          |
| 8         | 8         | 0      | 0    | 282          |
| 13        | 13        | 0      | 0    | 284          |
| 21        | 21        | 0      | 0    | 288          |
| 34        | 34        | 0      | 0    | 323          |
| 55        | 55        | 0      | 0    | 344          |
| 89        | 89        | 0      | 0    | 376          |
| 100       | 100       | 0      | 0    | 389          |

### Run 5: V2 on .NET Framework 4.7.2 build, pre-connected, all at once

- Implementation: V2 (publish lock), net472 build
- Runtime reported: `.NET Framework 4.8.10008.0`
- Connection: opened before publishing (`preconnect`)
- Arrival: all threads released at the same instant (spread 0)
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner.exe v2 <count> preconnect 0`
- Same outcome as V2 Run 2 (net8.0): every message confirmed

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 117          |
| 2         | 2         | 0      | 0    | 121          |
| 3         | 3         | 0      | 0    | 124          |
| 5         | 5         | 0      | 0    | 128          |
| 8         | 8         | 0      | 0    | 138          |
| 13        | 13        | 0      | 0    | 135          |
| 21        | 21        | 0      | 0    | 134          |
| 34        | 34        | 0      | 0    | 159          |
| 55        | 55        | 0      | 0    | 171          |
| 89        | 89        | 0      | 0    | 159          |
| 100       | 100       | 0      | 0    | 173          |

### Run 6: V2 on .NET Framework 4.7.2 build, pre-connected, spread up to 1 s

- Implementation: V2 (publish lock), net472 build
- Runtime reported: `.NET Framework 4.8.10008.0`
- Connection: opened before publishing (`preconnect`)
- Arrival: random delay between 0 and 1000 ms per thread
- Message counts: 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 100
- Command: `ConsoleRunner.exe v2 <count> preconnect 1000`
- Same outcome as V2 Run 3 (net8.0): every message confirmed. Elapsed is dominated by the random spread, so the short times for 1 and 3 messages are chance

| Requested | Confirmed | Failed | Hung | Elapsed (ms) |
| --------- | --------- | ------ | ---- | ------------ |
| 1         | 1         | 0      | 0    | 236          |
| 2         | 2         | 0      | 0    | 1015         |
| 3         | 3         | 0      | 0    | 141          |
| 5         | 5         | 0      | 0    | 989          |
| 8         | 8         | 0      | 0    | 886          |
| 13        | 13        | 0      | 0    | 934          |
| 21        | 21        | 0      | 0    | 979          |
| 34        | 34        | 0      | 0    | 992          |
| 55        | 55        | 0      | 0    | 1008         |
| 89        | 89        | 0      | 0    | 990          |
| 100       | 100       | 0      | 0    | 1015         |
