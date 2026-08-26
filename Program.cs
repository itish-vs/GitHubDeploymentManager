using System;
using System.Windows.Forms;
using GitHubDeploymentManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitHubDeploymentManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        var services = ConfigureServices();
        var logger = services.GetRequiredService<IAppLogger>();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                logger.Error("Unhandled application exception.", ex);
            else
                logger.Error($"Unhandled non-Exception object: {args.ExceptionObject}");
        };

        Application.ThreadException += (_, args) =>
        {
            logger.Error("Unhandled UI thread exception.", args.Exception);
        };

        ApplicationConfiguration.Initialize();
        Application.Run(services.GetRequiredService<MainForm>());
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AppConfiguration>();
        services.AddSingleton<IAppLogger>(sp => new FileLogger(sp.GetRequiredService<AppConfiguration>().LogFolderPath));
        services.AddSingleton<IDeploymentService, DeploymentService>();
        services.AddSingleton<IGitHubServiceFactory, GitHubServiceFactory>();
        services.AddSingleton<MainFormController>();
        services.AddTransient<MainForm>();
        return services.BuildServiceProvider();
    }
}
