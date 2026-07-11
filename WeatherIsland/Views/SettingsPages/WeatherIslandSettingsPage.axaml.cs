using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using WeatherIsland.Models;
using WeatherIsland.Services;
using Microsoft.Extensions.Logging; 
using ScottBrady.IdentityModel;


namespace WeatherIsland.Views.SettingsPages;

[SettingsPageInfo("QWeather.QWeatherSettings", "天气设置")]
public partial class WeatherIslandSettingsPage : SettingsPageBase
{
    private CancellationTokenSource? _autoCloseCts;
    // 暴露配置对象，供 XAML 绑定使用
    public Settings Settings => Plugin.Instance?.Settings ?? new Settings();

    public WeatherIslandSettingsPage()
    {;
        InitializeComponent();

        // 加载完成后初始化面板状态（与 Settings.ApiType 同步）
        Loaded += (s, e) => UpdatePanels(Settings.ApiType);
        Unloaded += (s, e) =>
        {
            _autoCloseCts?.Cancel();
            _autoCloseCts?.Dispose();
        };
    }

    private void CitySelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 判空保护
        if (ApiKey == null || JwtPanel == null)
            return;

        if (CitySelector.SelectedIndex == -1)
            return;
        UpdatePanels(CitySelector.SelectedIndex);
    }
    
    private void GetAPI_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _autoCloseCts?.Cancel();
        _autoCloseCts?.Dispose();
        _autoCloseCts = null;
        GetApiInfoBar.IsOpen = false;

        if (GetAPI.SelectedItem is ComboBoxItem selectedItem)
        {
            string selectedText = selectedItem.Content.ToString();

            bool isIncomplete = false;

            if (selectedText == "API KEY")
            {
                
                isIncomplete = string.IsNullOrEmpty(Plugin.Instance?.Settings?.ApiKey) ||
                               string.IsNullOrEmpty(Plugin.Instance?.Settings?.ApiAddress);
            }
            else if (selectedText == "JWT")
            {
                isIncomplete = string.IsNullOrEmpty(Plugin.Instance?.Settings?.PrivateKey) ||
                               string.IsNullOrEmpty(Plugin.Instance?.Settings?.Kid) ||
                               string.IsNullOrEmpty(Plugin.Instance?.Settings?.Sub) ||
                               string.IsNullOrEmpty(Plugin.Instance?.Settings?.ApiAddress);
            }

            // 3. 如果配置不完整，显示 InfoBar 并启动 3 秒后自动关闭
            if (isIncomplete)
            {
                GetApiInfoBar.Message = $"你的{selectedText}配置还没有填写完整，请及时填写，否则可能会导致无法访问天气API！";
                GetApiInfoBar.IsOpen = true;

                _autoCloseCts = new CancellationTokenSource();
                _ = AutoCloseInfoBarAsync(_autoCloseCts.Token);
            }
        }
    }
    private async Task AutoCloseInfoBarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(3000, cancellationToken);
            
            GetApiInfoBar.IsOpen = false;
        }
        catch (OperationCanceledException)
        {
        }
    }
    private void UpdatePanels(int selectedIndex)
    {
        ApiKey.IsVisible = (selectedIndex == 0);
        JwtPanel.IsVisible = (selectedIndex == 1);
    }

    private async void Tutorial_OnClick(object sender, RoutedEventArgs e)
    {
        GeneratedTeachingTip.IsOpen = true;
        string textToCopy = @"async function generateEd25519Pem() {
  const k = await crypto.subtle.generateKey({name:""Ed25519""},true,[""sign"",""verify""]);
  const p8 = await crypto.subtle.exportKey(""pkcs8"",k.privateKey);
  const spki = await crypto.subtle.exportKey(""spki"",k.publicKey);
  const pem = (d,t)=>{
    let b=btoa(String.fromCharCode(...new Uint8Array(d)));
    return`-----BEGIN ${t}-----\n${b.match(/.{1,64}/g).join(""\n"")}\n-----END ${t}-----`;
  };
  const priv=pem(p8,""PRIVATE KEY"");
  const pub=pem(spki,""PUBLIC KEY"");
  console.log(""私钥:\n"" + priv + ""\n\n公钥:\n"" + pub);
  return{priv,pub};
}
generateEd25519Pem();";
        try
        {
            await CopyToClipboardAsync(textToCopy);
        }
        catch (Exception ex)
        {
            var logger = IAppHost.GetService<ILogger<WeatherIslandSettingsPage>>();
            logger.LogError(ex , "复制时发生错误");
        }
    }

    private async Task CopyToClipboardAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var clipboard = topLevel.Clipboard;
        if (clipboard is not null)
        {
            try
            {
                await clipboard.SetTextAsync(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"复制失败: {ex.Message}");
            }
        }
    }
    
    private async void TestJwtButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var privateKey = Plugin.Instance?.Settings?.PrivateKey;
            var projectId = Plugin.Instance?.Settings?.Sub;
            var keyId = Plugin.Instance?.Settings?.Kid;

            // 检查配置是否完整
            if (string.IsNullOrEmpty(privateKey) || !privateKey.Contains("BEGIN PRIVATE KEY"))
            {
                ShowResult("私钥无效", "请粘贴完整的 PEM 格式私钥（包含 BEGIN 和 END 行）", false);
                return;
            }

            if (string.IsNullOrEmpty(projectId))
            {
                ShowResult("配置不完整", "请输入项目ID (Sub)", false);
                return;
            }

            if (string.IsNullOrEmpty(keyId))
            {
                ShowResult("配置不完整", "请输入凭据ID (Kid)", false);
                return;
            }

            // 生成 JWT
            var jwt = JwtGenerator.GenerateJwt(privateKey, projectId, keyId);
            
            // 显示结果
            JwtResultText.Text = jwt;
            JwtResultPanel.IsVisible = true;
            
            // 显示成功提示
            GeneratedTeachingTip.Title = "JWT 生成成功";
            GeneratedTeachingTip.Subtitle = "请点击下方「复制 JWT」按钮复制使用";
            GeneratedTeachingTip.IsOpen = true;
        }
        catch (Exception ex)
        {
            GeneratedTeachingTip.Title = "生成失败";
            GeneratedTeachingTip.Subtitle = ex.Message;
            GeneratedTeachingTip.IsOpen = true;
        }
    }

    private async void GetTestButton_OnClick(object sender, RoutedEventArgs e)
    {
        var logger = IAppHost.GetService<ILogger<WeatherIslandSettingsPage>>();

        // 获取当前选择的认证方式（0=JWT, 1=API KEY）
        int authMode = Settings.GetApiMode; // 假设 GetApiMode 是 int 类型
        if (authMode == 0) // JWT
        {
            // 验证 JWT 配置
            var privateKey = Plugin.Instance?.Settings?.PrivateKey;
            var projectId = Plugin.Instance?.Settings?.Sub;
            var keyId = Plugin.Instance?.Settings?.Kid;
            var apiAddress = Plugin.Instance?.Settings?.ApiAddress;

            if (string.IsNullOrEmpty(privateKey) || !privateKey.Contains("BEGIN PRIVATE KEY"))
            {
                ShowInfoBar("私钥不完整或无效", logger);
                return;
            }
            if (string.IsNullOrEmpty(projectId))
            {
                ShowInfoBar("未填写 ProjectId", logger);
                return;
            }
            if (string.IsNullOrEmpty(keyId))
            {
                ShowInfoBar("未填写 Kid", logger);
                return;
            }
            if (string.IsNullOrEmpty(apiAddress))
            {
                ShowInfoBar("未填写 API 地址", logger);
                return;
            }

            try
            {
                var locationId = (string)((dynamic)AppBase.Current).Settings.CityId;
                locationId = locationId.Split(':')[1];
                var jwt = JwtGenerator.GenerateJwt(privateKey, projectId, keyId);
                var weatherService = new WeatherIslandGet(apiAddress);
                string weatherJson = await weatherService.GetWeatherNowAsync(locationId, jwtToken: jwt);
                
                GetApiInfoBar.Message = "请求成功！";
                GetApiInfoBar.Severity = FluentAvalonia.UI.Controls.InfoBarSeverity.Success;
                GetApiInfoBar.IsOpen = true;
                // 自动关闭（可选）
                _autoCloseCts = new CancellationTokenSource();
                _ = AutoCloseInfoBarAsync(_autoCloseCts.Token);

                // 输出到日志
                logger.LogInformation("天气数据：{Weather}", weatherJson);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "请求天气失败");
                ShowInfoBar($"请求失败：{ex.Message}", logger);
            }
        }
        else if (authMode == 1) // API KEY
        {
            var apiKey = Plugin.Instance?.Settings?.ApiKey;
            var apiAddress = Plugin.Instance?.Settings?.ApiAddress;

            if (string.IsNullOrEmpty(apiKey))
            {
                ShowInfoBar("未填写 API Key", logger);
                return;
            }
            if (string.IsNullOrEmpty(apiAddress))
            {
                ShowInfoBar("未填写 API 地址", logger);
                return;
            }

            try
            {
                // 获取 locationId
                var locationId = (string)((dynamic)AppBase.Current).Settings.CityId;
                locationId = locationId.Split(':')[1];
                logger.LogInformation(locationId);
                

                var weatherService = new WeatherIslandGet(apiAddress);
                string weatherJson = await weatherService.GetWeatherNowAsync(locationId, apiKey: apiKey);

                GetApiInfoBar.Message = "请求成功！";
                GetApiInfoBar.Severity = FluentAvalonia.UI.Controls.InfoBarSeverity.Success;
                GetApiInfoBar.IsOpen = true;
                _autoCloseCts = new CancellationTokenSource();
                _ = AutoCloseInfoBarAsync(_autoCloseCts.Token);
                File.WriteAllText(@"C:\plugin_loaded.txt", DateTime.Now.ToString());

                logger.LogInformation("天气数据404：{Weather} ", weatherJson);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "请求天气失败");
                ShowInfoBar($"请求失败：{ex.Message}", logger);
            }
        }
        else
        {
            ShowInfoBar("未知的认证方式", logger);
        }
    }

    private void ShowInfoBar(string message, ILogger logger)
    {
        logger.LogError(message);
        GetApiInfoBar.Message = message;
        GetApiInfoBar.Severity = FluentAvalonia.UI.Controls.InfoBarSeverity.Warning;
        GetApiInfoBar.IsOpen = true;
        _autoCloseCts = new CancellationTokenSource();
        _ = AutoCloseInfoBarAsync(_autoCloseCts.Token);
    }

    private async void CopyJwtButton_OnClick(object sender, RoutedEventArgs e)
    {
        var jwt = JwtResultText.Text;
        if (string.IsNullOrEmpty(jwt))
            return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var clipboard = topLevel.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(jwt);
                    GeneratedTeachingTip.Title = "已复制到剪贴板";
                    GeneratedTeachingTip.Subtitle = "JWT 已复制，可以粘贴到 console.qweather.com/support/jwt-validation 检查";
                    GeneratedTeachingTip.IsOpen = true;
                }
            }
        }
        catch (Exception ex)
        {
            GeneratedTeachingTip.Title = "复制失败";
            GeneratedTeachingTip.Subtitle = ex.Message;
            GeneratedTeachingTip.IsOpen = true;
        }
    }

    private void ShowResult(string title, string subtitle, bool isSuccess)
    {
        GeneratedTeachingTip.Title = title;
        GeneratedTeachingTip.Subtitle = subtitle;
        GeneratedTeachingTip.IsOpen = true;
    }
}