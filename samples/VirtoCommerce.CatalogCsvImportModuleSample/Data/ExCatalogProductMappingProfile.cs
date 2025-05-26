using AutoMapper;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Data;

public class ExCatalogProductMappingProfile : Profile
{
    public ExCatalogProductMappingProfile()
    {
        CreateMap<ExCsvProduct, ExCatalogProduct>();
    }
}
