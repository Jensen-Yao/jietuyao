using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace STranslate.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    [RelayCommand]
    private void Cancel(Window window) => window.Close();
}