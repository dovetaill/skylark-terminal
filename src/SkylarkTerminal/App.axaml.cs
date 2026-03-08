using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.Views;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SkylarkTerminal;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();
            RuntimeLogger.Info("app-start", $"App starting. log={RuntimeLogger.CurrentLogFilePath}");

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAssetCatalogService, MockAssetCatalogService>();
        services.AddSingleton<IAppDialogService, AppDialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ISshConnectionService, SshConnectionService>();
        services.AddSingleton<ISftpService, MockSftpService>();
        services.AddSingleton<ISftpNavigationService>(_ => new SftpNavigationService("/"));
        services.AddSingleton<IWorkspaceLayoutService, WorkspaceLayoutService>();
        services.AddSingleton<IDragSessionService, DragSessionService>();
        services.AddSingleton<ISessionRegistryService, SessionRegistryService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>(provider => new MainWindow
        {
            DataContext = provider.GetRequiredService<MainWindowViewModel>(),
        });
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataValidators is safe to access at runtime")]
    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
