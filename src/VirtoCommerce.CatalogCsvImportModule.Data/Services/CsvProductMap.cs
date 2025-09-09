using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using CsvHelper;
using CsvHelper.Configuration;
using VirtoCommerce.CatalogCsvImportModule.Core.Helpers;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;

public class CsvProductMap : ClassMap<CsvProduct>
{
    public static CsvProductMap Create(CsvProductMappingConfiguration mappingCfg)
    {
        var map = AbstractTypeFactory<CsvProductMap>.TryCreateInstance();
        map.Initialize(mappingCfg);

        return map;
    }

    public virtual void Initialize(CsvProductMappingConfiguration mappingCfg)
    {
        //Dynamical map scalar product fields use by manual mapping information
        var index = 0;
        var csvProductType = AbstractTypeFactoryHelper.GetEffectiveType<CsvProduct>();

        foreach (var mappingItem in mappingCfg.PropertyMaps.Where(x => !string.IsNullOrEmpty(x.CsvColumnName) || !string.IsNullOrEmpty(x.CustomValue)))
        {
            var propertyInfo = csvProductType.GetProperty(mappingItem.EntityColumnName);
            if (propertyInfo != null)
            {
                var newMap = MemberMap.CreateGeneric(csvProductType, propertyInfo);

                newMap.Data.TypeConverterOptions.CultureInfo = CultureInfo.InvariantCulture;
                newMap.Data.TypeConverterOptions.NumberStyles = NumberStyles.Any;
                newMap.Data.TypeConverterOptions.BooleanTrueValues.AddRange(new List<string> { "True", "Yes" });
                newMap.Data.TypeConverterOptions.BooleanFalseValues.AddRange(new List<string> { "False", "No" });

                newMap.Data.Index = ++index;

                if (!string.IsNullOrEmpty(mappingItem.CsvColumnName))
                {
                    //Map fields if mapping specified
                    newMap.Name(mappingItem.CsvColumnName);
                }
                //And default values if it specified
                else if (mappingItem.CustomValue != null)
                {
                    var typeConverter = TypeDescriptor.GetConverter(propertyInfo.PropertyType);
                    newMap.Data.ReadingConvertExpression = (Expression<Func<ConvertFromStringArgs, object>>)(x => typeConverter.ConvertFromString(mappingItem.CustomValue));
                }
                MemberMaps.Add(newMap);
            }
        }

        //Map properties
        if (mappingCfg.PropertyCsvColumns != null && mappingCfg.PropertyCsvColumns.Any())
        {
            // Exporting multiple csv fields from the same property (which is a collection)
            foreach (var propertyCsvColumn in mappingCfg.PropertyCsvColumns)
            {
                // create CsvPropertyMap manually, because this.Map(x =>...) does not allow
                // to export multiple entries for the same property

                var propertyInfo = csvProductType.GetProperty(nameof(CsvProduct.Properties));
                var csvPropertyMap = MemberMap.CreateGeneric(csvProductType, propertyInfo);
                csvPropertyMap.Name(propertyCsvColumn);

                csvPropertyMap.Data.Index = ++index;

                // create custom converter instance which will get the required record from the collection
                csvPropertyMap.UsingExpression<ICollection<Property>>(null, properties =>
                {
                    var property = properties.FirstOrDefault(x => x.Name == propertyCsvColumn && x.Values.Any());
                    return property.JoinValues();
                });

                MemberMaps.Add(csvPropertyMap);
            }

            var newPropertyInfo = csvProductType.GetProperty(nameof(CsvProduct.Properties));
            var newPropMap = MemberMap.CreateGeneric(csvProductType, newPropertyInfo);
            newPropMap.Data.ReadingConvertExpression =
                (Expression<Func<ConvertFromStringArgs, object>>)(x => mappingCfg.PropertyCsvColumns.Select(column =>
                    (Property)new CsvProperty
                    {
                        Name = column,
                        Values = x.Row.GetPropertiesByColumn(column).ToList()
                    }).ToList());
            newPropMap.UsingExpression<ICollection<PropertyValue>>(null, null);

            newPropMap.Data.Index = ++index;

            MemberMaps.Add(newPropMap);
            newPropMap.Ignore(true);
        }

        //map line number
        var lineNumber = Map(m => m.LineNumber).Convert(row => row.Row.Parser.RawRow);
        lineNumber.Data.Index = ++index;
        lineNumber.Ignore(true);
    }
}
