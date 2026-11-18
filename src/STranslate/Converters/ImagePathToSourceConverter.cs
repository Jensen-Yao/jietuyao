using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace STranslate.Converters;

/// <summary>
/// 将图片文件路径转为 <see cref="BitmapImage"/>，使用 <see cref="BitmapCacheOption.OnLoad"/>
/// 立即关闭文件流，避免 WPF 默认行为锁定文件导致后续删除/覆盖失败。
/// </summary>
public class ImagePathToSourceConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
            return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
