using Newtonsoft.Json;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public static class TestUtils
{
    public static T Clone<T>(this T source)
    {
        var serialized = JsonConvert.SerializeObject(source);
        return JsonConvert.DeserializeObject<T>(serialized);
    }
}
