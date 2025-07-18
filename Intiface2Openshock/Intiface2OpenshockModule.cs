using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.ModuleBase.Navigation;
using Intiface2Openshock.Config;
using Intiface2Openshock.Services;
using Intiface2Openshock;
using Intiface2Openshock.Ui.Pages.Dash.Tabs;

[assembly:DesktopModule(typeof(Intiface2OpenshockModule), "Intiface2Openshock", "Intiface2Openshock")]

namespace Intiface2Openshock;

public class Intiface2OpenshockModule : DesktopModuleBase
{
    public override string IconPath => "Intiface2Openshock/Resources/Intiface2Openshock-Icon.png";

    public override IReadOnlyCollection<NavigationItem> NavigationComponents { get; } =
    [
        new()
        {
            Name = "Shocker Settings",
            ComponentType = typeof(ShockerSettingsTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.ElectricBolt)
        },
        new()
        {
            Name = "Shocker Connection",
            ComponentType = typeof(ShockerConnectionTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Cable)
        },
        new ()
        {
            Name = "Intiface Connection",
            ComponentType = typeof(IntifaceConnectionTab),
            Icon = IconOneOf.FromSvg(Icons.Material.Filled.Link)
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