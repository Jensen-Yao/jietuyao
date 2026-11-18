using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace STranslate.Core;

public partial class DropdownDataGeneric<TValue> : ObservableObject where TValue : Enum
{
    private static readonly Internationalization _i18n = Ioc.Default.GetRequiredService<Internationalization>();

    [ObservableProperty]
    public partial string Display { get; set; } = string.Empty;
    public TValue Value { get; private init; } = default!;
    public string LocalizationKey { get; set; } = string.Empty;

    public static List<TR> GetValues<TR>(string keyPrefix) where TR : DropdownDataGeneric<TValue>, new()
    {
        var data = new List<TR>();
        var enumValues = (TValue[])Enum.GetValues(typeof(TValue));

        foreach (var value in enumValues)
        {
            var key = keyPrefix + value;
            var display = _i18n.GetTranslation(key);
            data.Add(new TR { Display = display, Value = value, LocalizationKey = key });
        }

        return data;
    }

    public static void UpdateLabels<TR>(List<TR> options) where TR : DropdownDataGeneric<TValue>
    {
        foreach (var item in options)
        {
            item.Display = _i18n.GetTranslation(item.LocalizationKey);
        }
    }
}