using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Navigation;
using OpenShock.LocalRelay;
using OpenShock.LocalRelay.Config;
using OpenShock.LocalRelay.Services;
using OpenShock.LocalRelay.Ui.Pages.Dash.Tabs;

[assembly:DesktopModule(typeof(Intiface2OpenshockModule), "OpenShock.Intiface2Openshock", "Intiface2Openshock")]

namespace OpenShock.LocalRelay;

public class Intiface2OpenshockModule : DesktopModuleBase
{
    public override string IconPath => "OpenShock/Intiface2Openshock/Resources/Intiface2Openshock-Icon.png";

    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } =
    [
        new()
        {
            Name = "Settings",
            ComponentType = typeof(SettingsTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Hub)
        },
        new()
        {
            Name = "Serial",
            ComponentType = typeof(SerialTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.VoiceChat)
        }
    ];

    public override async Task Setup()
    {
        var config = await ModuleInstanceManager.GetModuleConfig<Intiface2OpenshockConfig>();
        ModuleServiceProvider = BuildServices(config);
        
    }

    private ServiceProvider BuildServices(IModuleConfig<Intiface2OpenshockConfig> config)
    {
        var loggerFactory = ModuleInstanceManager.AppServiceProvider.GetRequiredService<ILoggerFactory>();
        
        var services = new ServiceCollection();

        services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddSingleton(config);

        services.AddSingleton(ModuleInstanceManager.OpenShock);
        
        services.AddSingleton<FlowManager>();
        services.AddSingleton<SerialService>();
        
        return services.BuildServiceProvider();
    }   

    public override async Task Start()
    {
        await ModuleServiceProvider.GetRequiredService<FlowManager>().LoadConfigAndStart();
    }
}