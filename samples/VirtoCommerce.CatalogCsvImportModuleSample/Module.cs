using System;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;
using VirtoCommerce.CatalogCsvImportModuleSample.Data;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.CatalogCsvImportModuleSample;

public class Module : IModule
{
    public ManifestModuleInfo ModuleInfo { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        AbstractTypeFactory<CatalogProduct>.OverrideType<CatalogProduct, ExCatalogProduct>();
        AbstractTypeFactory<CsvProduct>.OverrideType<CsvProduct, ExCsvProduct>();
        AbstractTypeFactory<CsvProductMappingConfiguration>.OverrideType<CsvProductMappingConfiguration, ExCsvProductMappingConfiguration>();

        serviceCollection.AddAutoMapper(typeof(DataAssemblyMarker).Assembly);
        serviceCollection.AddSingleton<Func<CsvProductMappingConfiguration, ClassMap>>(CsvProductMap<ExCsvProduct>.Create);

        serviceCollection.AddTransient<ICsvProductConverter, ExCsvProductConverter>();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        // Nothing to do here
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
