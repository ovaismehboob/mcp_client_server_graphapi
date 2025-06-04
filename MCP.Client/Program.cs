using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Authentication.WebAssembly.Msal;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using MCP.Client;
using MCP.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Add authentication services
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Application.Read.All");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/User.Read");
});

// Configure HTTP client for the MCP server
builder.Services.AddHttpClient("MCP.ServerAPI", client => 
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Add scoped HTTP client factory
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("MCP.ServerAPI"));

// Add chat service with configuration
builder.Services.AddScoped<ChatService>();

// Add Semantic Kernel service
builder.Services.AddScoped<SemanticKernelService>();
builder.Services.AddScoped<ChatService>(sp => 
    new ChatService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("MCP.ServerAPI"),
        sp.GetRequiredService<IConfiguration>()
    ));

await builder.Build().RunAsync();
