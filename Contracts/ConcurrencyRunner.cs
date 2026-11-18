using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConcurrencyTesting.Contracts
{
    public static class ConcurrencyRunner
    {
        /// <summary>
        /// Creates one publisher, then starts <paramref name="count"/> dedicated threads that all wait on a gate and call
        /// PublishAsync at the same instant (or spread randomly over <paramref name="spreadMs"/> ms). Without <paramref name="preConnect"/>, the first calls also race through the lazy connect.
        /// </summary>
        public static async Task<RunResult> RunAsync(
            string name,
            Func<IStringPublisher> factory,
            int count,
            TimeSpan timeout,
            bool preConnect,
            int spreadMs = 0)
        {
            var result = new RunResult
            {
                Name = name,
                Count = count,
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            };

            using (IStringPublisher publisher = factory())
            {
                if (preConnect)
                    await publisher.ConnectAsync().ConfigureAwait(false);

                var outcomes = new Task<Exception>[count];
                var ready = new CountdownEvent(count);
                var gate = new ManualResetEventSlim(false);

                for (int i = 0; i < count; i++)
                {
                    var tcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
                    outcomes[i] = tcs.Task;
                    string message = name + "-" + i + "-" + DateTime.Now.ToString("HH:mm:ss.fff");

                    // With spreadMs > 0, each caller starts after a random delay in [0, spreadMs) ms, like requests arriving over time.
                    int delayMs = spreadMs > 0 ? new Random(Guid.NewGuid().GetHashCode()).Next(spreadMs) : 0;

                    var thread = new Thread(() =>
                    {
                        ready.Signal();
                        gate.Wait();
                        if (delayMs > 0)
                            Thread.Sleep(delayMs);
                        try
                        {
                            publisher.PublishAsync(message).ContinueWith(
                                t => tcs.TrySetResult(t.IsFaulted ? t.Exception.GetBaseException() : (t.IsCanceled ? new OperationCanceledException() : null)),
                                TaskContinuationOptions.ExecuteSynchronously);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetResult(ex);
                        }
                    });
                    thread.IsBackground = true;
                    thread.Start();
                }

                await Task.Run(() => ready.Wait()).ConfigureAwait(false);

                var watch = System.Diagnostics.Stopwatch.StartNew();
                gate.Set();

                await Task.WhenAny(Task.WhenAll(outcomes), Task.Delay(timeout)).ConfigureAwait(false);
                watch.Stop();
                result.Elapsed = watch.Elapsed;

                foreach (Task<Exception> outcome in outcomes)
                {
                    if (!outcome.IsCompleted)
                    {
                        result.Hung++;
                        continue;
                    }

                    Exception ex = outcome.Result;
                    if (ex == null)
                    {
                        result.Succeeded++;
                        continue;
                    }

                    string key = ex.GetType().Name + ": " + ex.Message;
                    result.Failures.TryGetValue(key, out int n);
                    result.Failures[key] = n + 1;
                }
            }

            return result;
        }
    }
}
