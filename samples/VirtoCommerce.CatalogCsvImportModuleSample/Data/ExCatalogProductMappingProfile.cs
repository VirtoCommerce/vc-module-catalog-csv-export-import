using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModuleSample.Data;
public class ExCatalogProductMappingProfile : Profile
{
    public ExCatalogProductMappingProfile()
    {
        CreateMap<ExCsvProduct, ExCatalogProduct>();
    }
}
