using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Baidu;

internal static class EnumExtensions
{
    /// <summary>
    /// 获取枚举的 Description 特性值
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        if (value == null)
            return string.Empty;

        var fieldInfo = value.GetType().GetField(value.ToString());
        if (fieldInfo == null)
            return value.ToString();

        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(
            typeof(DescriptionAttribute), false);

        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
}