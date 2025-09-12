using System.Linq;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Model;

public class CsvProperty : Property
{
    public virtual void MergeFrom(Property source)
    {
        Attributes = source.Attributes?.Select(x => (PropertyAttribute)x.Clone()).ToList();
        CatalogId = source.CatalogId;
        CategoryId = source.CategoryId;
        Dictionary = source.Dictionary;
        Hidden = source.Hidden;
        Id = source.Id;
        IsInherited = source.IsInherited;
        IsNew = source.IsNew;
        IsReadOnly = source.IsReadOnly;
        Multilanguage = source.Multilanguage;
        Multivalue = source.Multivalue;
        Name = source.Name;
        OuterId = source.OuterId;
        Required = source.Required;
        Type = source.Type;
        ValidationRules = source.ValidationRules?.Select(x => (PropertyValidationRule)x.Clone()).ToList();
        ValueType = source.ValueType;
    }
}
