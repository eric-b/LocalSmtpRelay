using System;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LocalSmtpRelay
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine($"Program version: {Assembly.GetExecutingAssembly().GetName().Version}");
                CreateHostBuilder(args).Build().Run();
            }
            catch (OptionsValidationException optionValidationEx)
            {
                Console.WriteLine($"Configuration error in {optionValidationEx.OptionsType.Name}: {optionValidationEx.Message}");
                throw;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host
                .CreateDefaultBuilder(args)
                .ConfigureServices(Startup.Startup.ConfigureServices);
        }
    }
}
