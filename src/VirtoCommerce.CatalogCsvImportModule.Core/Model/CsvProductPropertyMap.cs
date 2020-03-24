﻿using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Model
{
    public class CsvProductPropertyMap : ValueObject<CsvProductPropertyMap>
    {
        public string EntityColumnName { get; set; }
        public string CsvColumnName { get; set; }
        public bool IsSystemProperty { get; set; }
        public bool IsRequired { get; set; }
        public string CustomValue { get; set; }
        public string StringFormat { get; set; }
        public string Locale { get; set; }

        public override string ToString()
        {
            return string.Format("{0} -> {1}", (CsvColumnName ?? CustomValue) ?? "none", EntityColumnName ?? "none");
        }
    }
}
