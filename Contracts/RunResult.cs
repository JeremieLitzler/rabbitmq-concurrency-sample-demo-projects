using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConcurrencyTesting.Contracts
{
    public class RunResult
    {
        public string Name { get; set; }
        public string Runtime { get; set; }
        public int Count { get; set; }
        public int Succeeded { get; set; }
        public int Hung { get; set; }
        public TimeSpan Elapsed { get; set; }
        public Dictionary<string, int> Failures { get; } = new Dictionary<string, int>();
        public int Failed => Failures.Values.Sum();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{Name}] runtime={Runtime} requested={Count} confirmed={Succeeded} failed={Failed} hung={Hung} elapsed={Elapsed.TotalMilliseconds:F0} ms");
            foreach (var pair in Failures.OrderByDescending(p => p.Value))
                sb.AppendLine($"    {pair.Value} x {pair.Key}");
            return sb.ToString();
        }
    }
}
