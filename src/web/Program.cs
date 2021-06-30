using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddHttpClient("todoapi", client => client.BaseAddress = new Uri(builder.Configuration["todoapi:uri"]))
                            .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthorizationMessageHandler>()
                                                           .ConfigureHandler(authorizedUrls: new[] { builder.Configuration["todoapi:uri"] },
                                                                             scopes: new[] { builder.Configuration["todoapi:scope"] }));

            builder.Services.AddMsalAuthentication(options =>
            {
                options.ProviderOptions.AdditionalScopesToConsent.Add(builder.Configuration["todoapi:scope"]);
                builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
            });

            await builder.Build().RunAsync();
        }
    }
}
