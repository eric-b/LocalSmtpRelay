using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MediatR;
using SmtpServer.Authentication;
using IL.FluentValidation.Extensions.Options;
using LocalSmtpRelay.Components;

namespace LocalSmtpRelay.Startup
{
    static class Startup
    {
        public static void ConfigureServices(HostBuilderContext builderContext, IServiceCollection services)
        {
            services.AddMediatR(typeof(Startup));

            services.AddHostedService<SmtpServerBackgroundService>();
            
            services.AddSingleton<SmtpForwarder>();
            services // important to configure from config section and not from Bind for IOptionsMonitor.
                .Configure<SmtpForwarderOptions>(builderContext.Configuration.GetSection(AppSettings.Sections.SmtpForward))
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
                var portDesc = smtpBuilderOptions.Ports ?? new SmtpServerBuilderOptions.Port[] { 25 };
                bool requireAuth = s.GetRequiredService<IOptions<SmtpServerUserAuthenticatorOptions>>().Value.Accounts?.Length > 0;
                builder.MaxAuthenticationAttempts(2);
                foreach (var port in portDesc)
                {
                    builder.Endpoint(o =>
                    {
                        o.Port(port.Number, port.IsSecure);
                        if (requireAuth)
                        {
                            o.AllowUnsecureAuthentication();
                            o.AuthenticationRequired();
                        }
                    });
                }
                    
                //TODO: support secure auth with certificate.
                
                SmtpServer.ISmtpServerOptions smtpOptions = builder.Build();
                Console.WriteLine($"SMTP server: {smtpOptions.ServerName} - {smtpOptions.Endpoints[0].Endpoint.Address}:{smtpOptions.Endpoints[0].Endpoint.Port} auth: {smtpOptions.Endpoints[0].AuthenticationRequired} secure: {smtpOptions.Endpoints[0].IsSecure}");
                return smtpOptions;
            });
        }
    }
}
