using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class LegacyDerivedCatalogProductMappingProfile : Profile
{
    public LegacyDerivedCatalogProductMappingProfile()
    {
        CreateMap<CsvProduct, CatalogProduct>();
        CreateMap<DerivedCsvProductStandIn, DerivedCatalogProductStandIn>();
    }
}
