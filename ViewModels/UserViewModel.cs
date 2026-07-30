using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VTStudioToolBox.Auth;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Models;

namespace VTStudioToolBox.ViewModels;

public sealed class UserViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;

    public UserViewModel(IAuthService authService)
    {
        _authService = authService;
        RefreshFromService();
    }

    // ── Bindable Properties ──

    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetField(ref _isLoggedIn, value);
    }

    private string _displayName = "";
    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    private string _avatarUrl = "";
    public string AvatarUrl
    {
        get => _avatarUrl;
        set => SetField(ref _avatarUrl, value);
    }

    private string _userId = "";
    public string UserId
    {
        get => _userId;
        set => SetField(ref _userId, value);
    }

    private string _provider = "";
    public string Provider
    {
        get => _provider;
        set => SetField(ref _provider, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    // ── Commands ──

    public async Task LoginAsync(AuthProvider provider)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var identity = await _authService.LoginAsync(provider);
            ApplyIdentity(identity);
        }
        catch (System.Exception ex)
        {
            Logger.Error("UserVM", $"Login failed for {provider}", ex);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        ClearIdentity();
    }

    // ── Internal ──

    private void RefreshFromService()
    {
        if (_authService.CurrentUser is { } user)
            ApplyIdentity(user);
        else
            ClearIdentity();
    }

    private void ApplyIdentity(UserIdentity user)
    {
        IsLoggedIn = true;
        DisplayName = user.DisplayName;
        AvatarUrl = user.AvatarUrl;
        UserId = user.UserId;
        Provider = user.Provider.ToString();
    }

    private void ClearIdentity()
    {
        IsLoggedIn = false;
        DisplayName = "";
        AvatarUrl = "";
        UserId = "";
        Provider = "";
    }

    // ── INotifyPropertyChanged ──

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }
}
