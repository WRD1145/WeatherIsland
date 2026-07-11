using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WeatherIsland.Models;

public class Settings : INotifyPropertyChanged
{
    private string? _privateKey;
    private string? _apiAddress;
    private int _apiType; // 0=API地址, 1=JWT
    private string? _kid;
    private string? _sub;
    private string? _apiKey;
    private int _getApiMode;
    public string? PrivateKey
    {
        get => _privateKey;
        set
        {
            if (_privateKey != value)
            {
                _privateKey = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ApiAddress
    {
        get => _apiAddress;
        set
        {
            if (_apiAddress != value)
            {
                _apiAddress = value;
                OnPropertyChanged();
            }
        }
    }

    public int GetApiMode
    {
        get => _getApiMode;
        set
        {
            if (_getApiMode != value)
            { 
                _getApiMode = value;
                OnPropertyChanged();
            }
        }
    }

    public int ApiType
    {
        get => _apiType;
        set
        {
            if (_apiType != value)
            {
                _apiType = value;
                OnPropertyChanged();
            }
        }
    }
    public string Kid //凭据ID
    {
        get => _kid;
        set
        {
            if (_kid != value)
            {
                _kid = value;
                OnPropertyChanged();
            }
        }
    }

    public string Sub //项目ID
    {
        get => _sub;
        set
        {
            if (_sub != value)
            {
                _sub = value;
                OnPropertyChanged();
            }
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (_apiKey != value)
            {
                _apiKey = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}