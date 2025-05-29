using System;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Helpers;

public static class AbstractTypeFactoryHelper
{
    public static Type GetEffectiveType<T>()
    {
        var registeredTypes = AbstractTypeFactory<T>.AllTypeInfos.ToList();

        // If there is only one registered type, return it
        return registeredTypes.Count == 1
            ? registeredTypes[0].Type
            : typeof(T);
    }
}
