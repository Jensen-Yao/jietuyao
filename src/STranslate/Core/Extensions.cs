using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Reflection;

namespace STranslate.Core;

public static class Extensions
{
    public static T NonNull<T>(this T? value) =>
        value is null ? throw new NullReferenceException($"Value of type {typeof(T)} is null.") : value;

    public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
    {
        return source is ObservableCollection<T> observableCollection ? observableCollection : new ObservableCollection<T>(source);
    }

    public static string GetIntValue(this Enum value) => Convert.ToInt32(value).ToString();

    public static IServiceCollection AddScopedFromNamespace(
        this IServiceCollection services,
        string namespaceName,
        params Assembly[] assemblies
    )
    {
        foreach (var assembly in assemblies)
        {
            var types = assembly
                .GetTypes()
                .Where(x =>
                    x.IsClass
                    && !x.IsAbstract
                    && !x.IsNested                      // 排除所有嵌套类型
                    && x.Namespace != null
                    && x.Namespace.StartsWith(namespaceName, StringComparison.InvariantCultureIgnoreCase)
                );

            foreach (var type in types)
            {
                if (services.All(x => x.ServiceType != type))
                {
                    _ = services.AddScoped(type);
                }
            }
        }

        return services;
    }
}