namespace STranslate.Plugin.Translate.DeepL;

internal static class Constant
{
    internal const string ApiBaseUrl = "https://api.deepl.com";
    internal const string ApiFreeBaseUrl = "https://api-free.deepl.com";
    internal const string TranslatePath = "/v2/translate";
    internal const string UsagePath = "/v2/usage";

    internal static bool TryBuildEndpoint(Settings settings, string endpointPath, out string endpoint)
    {
        var baseUrl = settings.ApiType switch
        {
            DeepLApiType.Api => ApiBaseUrl,
            DeepLApiType.ApiFree => ApiFreeBaseUrl,
            DeepLApiType.Custom => settings.CustomApiUrl,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(baseUri.Host)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            endpoint = string.Empty;
            return false;
        }

        endpoint = $"{baseUri.AbsoluteUri.TrimEnd('/')}/{endpointPath.TrimStart('/')}";
        return true;
    }
}
