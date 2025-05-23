using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.CatalogCsvImportModule.Data.Services;
public class CatalogProductMappingProfile : Profile
{
    public CatalogProductMappingProfile()
    {
        CreateMap<CsvProduct, CatalogProduct>();
    }
}
