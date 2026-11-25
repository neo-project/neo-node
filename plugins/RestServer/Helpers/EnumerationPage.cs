namespace Neo.Plugins.RestServer.Helpers;

internal static class EnumerationPage
{
    public static int GetOffset(int page, int size, int? maxPageSize = null)
    {
        var settings = RestServerSettings.Current;
        var end = ((long)page - 1) * size + size;
        if (page < 1 || size < 1 || size > settings.MaxPageSize || size > (maxPageSize ?? int.MaxValue) || end > settings.MaxEnumerationItems)
            throw new Exceptions.InvalidParameterRangeException();

        return (int)(end - size);
    }
}
