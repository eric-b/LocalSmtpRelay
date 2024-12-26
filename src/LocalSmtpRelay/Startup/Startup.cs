using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmtpServer.Authentication;
using IL.FluentValidation.Extensions.Options;
using LocalSmtpRelay.Components;
using LocalSmtpRelay.Components.MediatrHandlers;
using LocalSmtpRelay.Components.AlertManager;
using System.Net.Http.Headers;
using LocalSmtpRelay.Components.Llm;

namespace LocalSmtpRelay.Startup
{
    static class Startup
    {
        public static void ConfigureServices(HostBuilderContext builderContext, IServiceCollection services)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SendRequestHandler>());

            services.AddHostedService<SmtpServerBackgroundService>();
            
            services.AddSingleton<SmtpForwarder>();
            services // important to configure from config section and not from Bind for IOptionsMonitor.
                .Configure<SmtpForwarderOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.SmtpForwarder))
                .AddOptions<SmtpForwarderOptions>().Validate<SmtpForwarderOptions, SmtpForwarderOptions.Validator>();

            services.AddSingleton<StartupPhase>();
            services.AddSingleton<StartupPhaseOptions>();
            services.Configure<SendMessageAtStartupOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.SendMessageAtStartup));

            services.AddSingleton<MessageStore>();
            services
                .Configure<MessageStoreOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.MessageStore))
                .AddOptions<MessageStoreOptions>().Validate<MessageStoreOptions, MessageStoreOptions.Validator>();

            services.AddSingleton<IUserAuthenticatorFactory, SmtpServerUserAuthenticator.Factory>();
            services.Configure<SmtpServerUserAuthenticatorOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.SmtpServer));

            services.AddSingleton<SmtpServer.SmtpServer>();
            services.AddSingleton<SmtpServer.Storage.IMessageStore>(s => s.GetRequiredService<MessageStore>());
            services.AddSingleton(s =>
            {
                var smtpBuilderOptions = new SmtpServerBuilderOptions();
                builderContext.Configuration.GetSection(AppSettings.Sections.SmtpServer).Bind(smtpBuilderOptions);
                var builder = new SmtpServer.SmtpServerOptionsBuilder();
                builder.ServerName(smtpBuilderOptions.Hostname ?? "localhost");
                var portDesc = smtpBuilderOptions.Ports ?? [25];
                var options = s.GetRequiredService<IOptions<SmtpServerUserAuthenticatorOptions>>().Value;
                bool requireAuth = options.Accounts?.Length > 0 && !options.AllowAnonymous;
                builder.MaxAuthenticationAttempts(2);
                foreach (var port in portDesc)
                {
                    builder.Endpoint(o =>
                    {
                        o.Port(port.Number, port.IsSecure);
                        o.AllowUnsecureAuthentication();
                        if (requireAuth)
                        {
                            o.AuthenticationRequired();
                        }
                    });
                }
                
                SmtpServer.ISmtpServerOptions smtpOptions = builder.Build();
                Console.WriteLine($"SMTP server: {smtpOptions.ServerName} - {smtpOptions.Endpoints[0].Endpoint.Address}:{smtpOptions.Endpoints[0].Endpoint.Port} auth: {smtpOptions.Endpoints[0].AuthenticationRequired} secure: {smtpOptions.Endpoints[0].IsSecure}");
                return smtpOptions;
            });

            services.AddSingleton<AlertManagerForwarder>();
            services.Configure<AlertManagerForwarderOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.AlertManagerForwarder));

            services.PostConfigure<AlertManagerForwarderOptions>(options =>
            {
                if (options.BaseUrl is null || options.BaseUrl?.IsAbsoluteUri != true)
                {
                    options.Disable = true;
                }
            });

            services
                .AddHttpClient()
                .AddHttpClient<AlertManagerClient>((svc, configureClient) =>
                {
                    var options = svc.GetRequiredService<IOptions<AlertManagerForwarderOptions>>();
                    var url = options.Value.BaseUrl;
                    if (url != null && url.IsAbsoluteUri && !options.Value.Disable)
                    {
                        if (!url.ToString().EndsWith('/'))
                            url = new Uri($"{url}/");
                        configureClient.BaseAddress = url;
                        configureClient.DefaultRequestHeaders.Accept.Clear();
                        configureClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    }
                });


            services.AddSingleton<LlmAlertSummarizer>();
            services.Configure<LlmAlertSummarizerOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.LlmClient));  // TODO: move settings to alertForwarder.

            services.AddSingleton<LlmSubjectHelper>();
            services.Configure<LlmSubjectHelperOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.SmtpForwarderLlmParameters));

            services.Configure<LlmChatClientOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.LlmClient));
            services.AddHttpClient<LlmChatClient>((svc, configureClient) =>
            {
                var options = svc.GetRequiredService<IOptions<LlmChatClientOptions>>();
                var url = options.Value.BaseUrl;
                if (url != null && url.IsAbsoluteUri)
                {
                    if (!url.ToString().EndsWith('/'))
                        url = new Uri($"{url}/");
                    configureClient.BaseAddress = url;
                }
                configureClient.DefaultRequestHeaders.Accept.Clear();
                configureClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrEmpty(options.Value.ApiKey))
                    configureClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

                if (options.Value.RequestTimeout.HasValue)
                    configureClient.Timeout = options.Value.RequestTimeout.Value;
            });
        }
    }
}
