using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Helpers;
public static class AbstractTypeFactoryHelper
{
    public static Type GetEffectiveType<T>()
    {
        Type result;
        var registeredTypes = AbstractTypeFactory<T>.AllTypeInfos.ToList();

        // If only one registered type - return it
        if (registeredTypes.Count == 1)
        {
            result = registeredTypes[0].Type;
        }
        else
        {
            result = typeof(T);
        }

        return result;
    }
}
