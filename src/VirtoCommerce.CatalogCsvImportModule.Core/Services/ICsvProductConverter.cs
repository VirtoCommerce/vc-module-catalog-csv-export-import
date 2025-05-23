using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Services;
public interface ICsvProductConverter
{
    CatalogProduct GetCatalogProduct(CsvProduct csvProduct);
}
