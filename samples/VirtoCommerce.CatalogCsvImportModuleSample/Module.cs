using CsvHelper.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModuleSample.Core;
using VirtoCommerce.CatalogCsvImportModuleSample.Data;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.CatalogCsvImportModuleSample;

public class Module : IModule
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public void Initialize(IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<ICsvProductConverter, ExCsvProductConverter>();
        serviceCollection.AddSingleton<Func<CsvProductMappingConfiguration, ClassMap>>(config => new ExCsvProductMap(config));
        serviceCollection.AddAutoMapper(typeof(DataAssemblyMarker).Assembly);
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        AbstractTypeFactory<CsvProduct>.OverrideType<CsvProduct, ExCsvProduct>();
        AbstractTypeFactory<CsvProductMappingConfiguration>.OverrideType<CsvProductMappingConfiguration, ExCsvProductMappingConfiguration>();
    }

    public void Uninstall()
    {
        // No need in actions
    }
}
