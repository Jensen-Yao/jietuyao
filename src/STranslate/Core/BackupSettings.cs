using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Core;

public partial class BackupSettings : ObservableObject
{
    [ObservableProperty] public partial BackupType Type { get; set; }

    [ObservableProperty] public partial string Address { get; set; } = string.Empty;

    [ObservableProperty] public partial string Username { get; set; } = string.Empty;

    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
}

public enum BackupType
{
    Local,
    WebDav,
}