using System.IO;
using System.Reflection;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherIsland.Models;
using WeatherIsland.Views.SettingsPages;

namespace WeatherIsland;

[PluginEntrance]
public class Plugin : PluginBase
{
    public static Plugin? Instance { get; private set; }

    public Settings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        
        services.AddSettingsPage<WeatherIslandSettingsPage>();
        Instance = this;
    
        var configPath = Path.Combine(PluginConfigFolder, "Settings.json");
        Settings = ConfigureFileHelper.LoadConfig<Settings>(configPath);

        Settings.PropertyChanged += (_, _) =>
        {
            ConfigureFileHelper.SaveConfig(configPath, Settings);
        };
        var locationId = (string)((dynamic)AppBase.Current).Settings.CityId;
        locationId = locationId.Split(':')[1];
        for (int i = 0; i <= 3; i++)
        {
            Console.WriteLine("地区代码"+locationId);
        }
        
    }
}