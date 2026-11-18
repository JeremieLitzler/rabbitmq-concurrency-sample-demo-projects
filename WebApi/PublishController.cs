using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using ConcurrencyTesting.Contracts;

namespace ConcurrencyTesting.WebApi
{
    /// <summary>One endpoint per publisher version; the singleton publishers come from the DI container (see Program.cs).</summary>
    [RoutePrefix("api")]
    public class PublishController : ApiController
    {
        private const int MaxDelayMs = 200;
        private static readonly Random Rng = new Random();

        private readonly V1Publisher _v1;
        private readonly V2Publisher _v2;

        public PublishController(V1Publisher v1, V2Publisher v2)
        {
            _v1 = v1;
            _v2 = v2;
        }

        /// <summary>Waits a random 0-199 ms (simulated work), then publishes the body and waits for the broker confirm.</summary>
        [HttpPost, Route("v1/publish")]
        public Task<HttpResponseMessage> PublishV1([FromBody] string message) => Publish(_v1, message);

        [HttpPost, Route("v2/publish")]
        public Task<HttpResponseMessage> PublishV2([FromBody] string message) => Publish(_v2, message);

        /// <summary>Opens the connection and channel without publishing (the "preconnect" mode).</summary>
        [HttpPost, Route("v1/connect")]
        public async Task<IHttpActionResult> ConnectV1() { await _v1.ConnectAsync().ConfigureAwait(false); return Ok(); }

        [HttpPost, Route("v2/connect")]
        public async Task<IHttpActionResult> ConnectV2() { await _v2.ConnectAsync().ConfigureAwait(false); return Ok(); }

        private async Task<HttpResponseMessage> Publish(IStringPublisher publisher, string message)
        {
            int delay;
            lock (Rng) delay = Rng.Next(MaxDelayMs);
            await Task.Delay(delay).ConfigureAwait(false);
            try
            {
                await publisher.PublishAsync(message).ConfigureAwait(false);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                // The runner groups failures by this text, same as the Console runs.
                Exception b = ex.GetBaseException();
                return Request.CreateResponse(HttpStatusCode.InternalServerError, b.GetType().Name + ": " + b.Message);
            }
        }
    }
}
