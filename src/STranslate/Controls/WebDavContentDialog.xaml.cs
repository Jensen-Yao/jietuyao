using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Extensions.Logging;
using STranslate.Core;
using STranslate.Plugin;
using STranslate.Services;
using System.Collections.ObjectModel;
using System.IO;
using WebDav;

namespace STranslate.Controls;

public partial class WebDavContentDialog
{
    private readonly ILogger<WebDavContentDialog> _logger = Ioc.Default.GetRequiredService<ILogger<WebDavContentDialog>>();
    private readonly ISnackbar _snackbar = Ioc.Default.GetRequiredService<ISnackbar>();
    private readonly Internationalization _i18n = Ioc.Default.GetRequiredService<Internationalization>();
    private readonly WebDavClient _webDavClient;
    private readonly string _absolutePath;

    public ObservableCollection<WebDavResult> Collections { get; }

    public string FilePath { get; private set; } = string.Empty;

    public WebDavContentDialog(
        WebDavClient webDavClient,
        string absolutePath,
        ObservableCollection<WebDavResult> collections)
    {
        InitializeComponent();

        DataContext = this;

        _webDavClient = webDavClient;
        _absolutePath = absolutePath;
        Collections = collections;
    }

    private ContentDialogResult _result = ContentDialogResult.None;

    public new async Task<ContentDialogResult> ShowAsync()
    {
        await base.ShowAsync();
        return _result;
    }

    [RelayCommand]
    private async Task DownloadAsync(string fullName)
    {
        // 下载逻辑
        if (_absolutePath == null || _webDavClient == null)
            return;

        try
        {
            var path = $"{_absolutePath.TrimEnd('/')}/{fullName}";
            if (File.Exists(fullName))
            {
                File.Delete(fullName);
            }
            FilePath = fullName;

            using var response = await _webDavClient.GetRawFile(path);
            if (response.IsSuccessful && response.StatusCode == 200)
            {
                using var fileStream = File.Create(fullName);
                await response.Stream.CopyToAsync(fileStream);

                _snackbar.ShowSuccess(_i18n.GetTranslation("DownloadSuccess"));

                _result = ContentDialogResult.Primary;
                Hide();
            }
            else
            {
                var message = string.Format(_i18n.GetTranslation("DownloadFailed"), response.StatusCode, response.Description);
                _snackbar.ShowError(message);
                _logger.LogError(message);
            }
        }
        catch (Exception ex)
        {
            var message = string.Format(_i18n.GetTranslation("DownloadException"), ex.Message);
            _snackbar.ShowError(message);
            _logger.LogError(ex, message);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(string fullName)
    {
        if (_absolutePath == null || _webDavClient == null)
            return;

        var path = $"{_absolutePath.TrimEnd('/')}/{fullName}";
        var response = await _webDavClient.Delete(path);
        if (response.IsSuccessful && response.StatusCode == 204)
            Collections.Remove(Find(fullName));
        else
        {
            var message = string.Format(_i18n.GetTranslation("DeleteFailed"), response.StatusCode, response.Description);
            _snackbar.ShowError(message);
            _logger.LogError(message);
        }
    }

    [RelayCommand]
    private async Task UpdateTextAsync(ValueTuple<string, string> valueTuple)
    {
        if (_absolutePath == null || _webDavClient == null)
            return;

        var originFullName = valueTuple.Item1;
        var targetFullname = valueTuple.Item2;

        try
        {
            if (!targetFullname.EndsWith(".zip"))
            {
                targetFullname += ".zip";
                Collections.First(x => x.FullName == valueTuple.Item1).FullName = targetFullname;
            }

            if (originFullName == targetFullname)
                return;

            //webdav operate
            var source = $"{_absolutePath.TrimEnd('/')}/{originFullName}";
            var target = $"{_absolutePath.TrimEnd('/')}/{targetFullname}";

            var response = await _webDavClient.Move(source, target);

            if (!response.IsSuccessful || response.StatusCode != 201)
            {
                Collections.First(x => x.FullName == targetFullname).FullName = originFullName;
                var message = string.Format(_i18n.GetTranslation("RenameFailed"), response.StatusCode, response.Description);
                _snackbar.ShowError(message);
                _logger.LogError(message);
            }
        }
        catch (Exception ex)
        {
            // 关闭页面
            _result = ContentDialogResult.None;
            Hide();

            var message = string.Format(_i18n.GetTranslation("RenameException"), ex.Message);
            _snackbar.ShowError(message);
            _logger.LogError(ex, message);
        }
    }

    private WebDavResult Find(string fullName)
    {
        return Collections.First(x => x.FullName == fullName);
    }
}
