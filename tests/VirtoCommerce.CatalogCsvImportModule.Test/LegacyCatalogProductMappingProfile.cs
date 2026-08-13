using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class LegacyCatalogProductMappingProfile : Profile
{
    public LegacyCatalogProductMappingProfile()
    {
        CreateMap<CsvProduct, CatalogProduct>();
    }
}
