using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;
public class CsvProductConverter : ICsvProductConverter
{
    private readonly IMapper _mapper;

    public CsvProductConverter(IMapper mapper)
    {
        _mapper = mapper;
    }

    public virtual CatalogProduct GetCatalogProduct(CsvProduct csvProduct)
    {
        var catalogProduct = AbstractTypeFactory<CatalogProduct>.TryCreateInstance();

        _mapper.Map(csvProduct, catalogProduct);

        return catalogProduct;
    }
}
