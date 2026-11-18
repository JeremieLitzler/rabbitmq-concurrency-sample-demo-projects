using System;
using ConcurrencyTesting.WebApi;
using Microsoft.Owin.Hosting;

// Usage: WebApi [baseUrl]   (default http://localhost:5080/)
// Self-hosted ASP.NET Web API 2 on .NET Framework 4.8.1: one endpoint per publisher version. DI is set up in Startup.cs.
string baseUrl = args.Length > 0 ? args[0] : "http://localhost:5080/";

using (WebApp.Start<Startup>(baseUrl))
{
    Console.WriteLine($"WebApi listening on {baseUrl} ({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}). Press Enter to stop.");
    Console.ReadLine();
}
