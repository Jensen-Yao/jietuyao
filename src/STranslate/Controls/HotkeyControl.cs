using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using STranslate.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace STranslate.Controls;

public class HotkeyControl : Button
{
    private readonly Internationalization _i18n;
    private bool _isDialogOpening;

    static HotkeyControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyControl),
            new FrameworkPropertyMetadata(typeof(HotkeyControl)));
    }

    public HotkeyControl()
    {
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
    }

    public string WindowTitle
    {
        get => (string)GetValue(WindowTitleProperty);
        set => SetValue(WindowTitleProperty, value);
    }
    public static readonly DependencyProperty WindowTitleProperty = DependencyProperty.Register(
        nameof(WindowTitle),
        typeof(string),
        typeof(HotkeyControl),
        new PropertyMetadata(string.Empty)
    );

    public bool ValidateKeyGesture
    {
        get => (bool)GetValue(ValidateKeyGestureProperty);
        set => SetValue(ValidateKeyGestureProperty, value);
    }
    public static readonly DependencyProperty ValidateKeyGestureProperty = DependencyProperty.Register(
        nameof(ValidateKeyGesture),
        typeof(bool),
        typeof(HotkeyControl),
        new PropertyMetadata(true)
    );

    public string DefaultHotkey
    {
        get => (string)GetValue(DefaultHotkeyProperty);
        set => SetValue(DefaultHotkeyProperty, value);
    }

    public static readonly DependencyProperty DefaultHotkeyProperty = DependencyProperty.Register(
        nameof(DefaultHotkey),
        typeof(string),
        typeof(HotkeyControl),
        new PropertyMetadata(string.Empty)
    );

    public ICommand? ChangeHotkey
    {
        get => (ICommand)GetValue(ChangeHotkeyProperty);
        set => SetValue(ChangeHotkeyProperty, value);
    }
    public static readonly DependencyProperty ChangeHotkeyProperty = DependencyProperty.Register(
        nameof(ChangeHotkey),
        typeof(ICommand),
        typeof(HotkeyControl),
        new PropertyMetadata(default(ICommand))
    );

    public bool IsRegistered
    {
        get => (bool)GetValue(IsRegisteredProperty);
        set => SetValue(IsRegisteredProperty, value);
    }
    public static readonly DependencyProperty IsRegisteredProperty = DependencyProperty.Register(
        nameof(IsRegistered),
        typeof(bool),
        typeof(HotkeyControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
    );

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyControl),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged)
    );

    public HotkeyType Type
    {
        get => (HotkeyType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register(
            nameof(Type),
            typeof(HotkeyType),
            typeof(HotkeyControl),
            new PropertyMetadata(HotkeyType.Global));


    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HotkeyControl hotkey)
        {
            return;
        }

        hotkey.RefreshHotkeyInterface(hotkey.Hotkey);
    }

    private void RefreshHotkeyInterface(string hotkey)
    {
        SetKeysToDisplay(new HotkeyModel(hotkey));
        CurrentHotkey = new HotkeyModel(hotkey);
    }

    private bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
        (!Type.HasFlag(HotkeyType.Global) || !HotkeyMapper.IsReservedGlobalHotkey(hotkey)) &&
        hotkey.Validate(validateKeyGesture) &&
        HotkeyMapper.CheckAvailability(hotkey);

    public string EmptyHotkey => _i18n.GetTranslation("None");

    public ObservableCollection<string> KeysToDisplay { get; set; } = [];

    public HotkeyModel CurrentHotkey { get; private set; } = new(false, false, false, false, Key.None);

    protected override void OnClick() => _ = OpenHotkeyDialogAsync();

    private async Task OpenHotkeyDialogAsync()
    {
        if (_isDialogOpening)
            return;

        _isDialogOpening = true;
        try
        {
            var owner = ResolveDialogOwner();
            if (owner == null)
                return;

            if (Type == HotkeyType.Global &&
                !string.IsNullOrEmpty(Hotkey) &&
                !HotkeyMapper.RemoveHotkey(Hotkey))
                return;

            var dialog = new HotkeyControlDialog(Type, Hotkey, DefaultHotkey, WindowTitle);
            await dialog.ShowAsync(owner);
            switch (dialog.ReturnType)
            {
                case HotkeyControlDialog.HkReturnType.Save:
                    SetHotkey(dialog.ResultValue);
                    break;
                case HotkeyControlDialog.HkReturnType.Cancel:
                    SetHotkey(Hotkey);
                    break;
                case HotkeyControlDialog.HkReturnType.Delete:
                    Delete();
                    break;
                default:
                    break;
            }
        }
        finally
        {
            _isDialogOpening = false;
        }
    }

    private Window? ResolveDialogOwner()
    {
        var owner = Window.GetWindow(this);
        if (owner?.IsVisible == true)
            return owner;

        return Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive && window.IsVisible);
    }

    public void SetHotkey(string keyStr, bool triggerValidate = true) => SetHotkey(new HotkeyModel(keyStr), triggerValidate);

    private void SetHotkey(HotkeyModel keyModel, bool triggerValidate = true)
    {
        var hotkeyString = keyModel.ToString();
        if (triggerValidate)
        {
            // TODO: This is a temporary way to enforce changing only the open flow hotkey to Win, and will be removed by PR #3157
            var isWinKey = hotkeyString is "LWin" or "RWin";

            if (!isWinKey && !CheckHotkeyAvailability(keyModel, ValidateKeyGesture))
            {
                return;
            }

            Hotkey = hotkeyString;
            SetKeysToDisplay(CurrentHotkey);
            ChangeHotkey?.Execute(keyModel);
        }
        else
        {
            Hotkey = hotkeyString;
            ChangeHotkey?.Execute(keyModel);
        }
    }

    private void Delete()
    {
        if (!string.IsNullOrEmpty(Hotkey) && !HotkeyMapper.RemoveHotkey(Hotkey))
            return;
        Hotkey = Constant.EmptyHotkey;
        SetKeysToDisplay(new HotkeyModel(false, false, false, false, Key.None));
    }

    private void SetKeysToDisplay(HotkeyModel? hotkey)
    {
        KeysToDisplay.Clear();

        if (hotkey == null || hotkey == default(HotkeyModel) || hotkey.ToString() == Constant.EmptyHotkey)
        {
            KeysToDisplay.Add(EmptyHotkey);
            return;
        }

        foreach (var key in hotkey.Value.EnumerateDisplayKeys()!)
        {
            KeysToDisplay.Add(key);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var itemsControl = GetTemplateChild("PART_ItemsHost") as ItemsControl;
        itemsControl?.ItemsSource = KeysToDisplay;
    }
}
