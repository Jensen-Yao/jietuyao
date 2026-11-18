using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using STranslate.Core;
using System.Windows;
using System.Windows.Controls;

namespace STranslate.ViewModels;

public partial class SearchViewModelBase : ObservableObject, IDisposable
{
    protected List<string> SettingItems { get; set; } = [];
    private readonly Internationalization _i18n;
    private readonly Action _languageChangedHandler;
    private bool _disposed;

    public SearchViewModelBase(Internationalization i18n, string prefix)
    {
        _i18n = i18n;
        _languageChangedHandler = () => UpdateItems(prefix);
        _i18n.OnLanguageChanged += _languageChangedHandler;

        UpdateItems(prefix);
    }

    protected void UpdateItems(string prefix)
    {
        // 可能会有多条资源字典，找最后一条包含 IdentifiedLanguageLabel 的即为当前应用语言资源
        var dict = Application.Current.Resources.MergedDictionaries
            .LastOrDefault(x => x.Contains("IdentifiedLanguageLabel"));

        if (dict == null) return;

        SettingItems.Clear();

        var newItems = dict.Keys
            .OfType<string>()
            .Where(key => key.StartsWith(prefix))
            .Select(key => dict[key]?.ToString())
            .Where(value => !string.IsNullOrEmpty(value) && !SettingItems.Contains(value))
            .Select(value => value!);

        SettingItems.AddRange(newItems);

        // 切换语言时清空建议列表
        _autoSuggestBox?.ItemsSource = null;
    }

    private AutoSuggestBox? _autoSuggestBox;

    [RelayCommand]
    private void TextChanged(Tuple<object, EventArgs> tuple)
    {
        if (tuple.Item1 is not AutoSuggestBox sender || tuple.Item2 is not AutoSuggestBoxTextChangedEventArgs args)
            return;

        // 缓存 AutoSuggestBox 实例以便在切换语言时清空建议列表
        _autoSuggestBox ??= sender;

        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        var suggestions = new List<string>();
        var splitText = sender.Text.ToLower().Split(' ');
        foreach (var item in SettingItems)
        {
            var found = splitText.All((key) =>
            {
                return item.Contains(key, StringComparison.OrdinalIgnoreCase);
            });
            if (found)
            {
                suggestions.Add(item);
            }
        }
        if (suggestions.Count == 0)
        {
            suggestions.Add(_i18n.GetTranslation("NoResultsFound"));
        }

        sender.ItemsSource = suggestions;
    }

    [RelayCommand]
    private void SuggestionChosen(Tuple<object, EventArgs> tuple)
    {
        if (tuple.Item1 is not AutoSuggestBox sender || tuple.Item2 is not AutoSuggestBoxSuggestionChosenEventArgs args)
            return;

        sender.Text = args.SelectedItem.ToString();
    }

    [RelayCommand]
    private void QuerySubmitted(Tuple<object, EventArgs> tuple)
    {
        if (tuple.Item1 is not ScrollViewer sender || tuple.Item2 is not AutoSuggestBoxQuerySubmittedEventArgs args)
            return;

        void LocateAction(string content)
        {
            var element = Utilities.FindSettingElementByContent(sender, content);
            if (element == null) return;
            Utilities.BringIntoViewAndHighlight(element);
        }
        if (args.ChosenSuggestion != null && args.ChosenSuggestion is string queryText)
        {
            LocateAction(queryText);
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText) && SettingItems.Contains(args.QueryText))
        {
            LocateAction(args.QueryText);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _i18n.OnLanguageChanged -= _languageChangedHandler;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
