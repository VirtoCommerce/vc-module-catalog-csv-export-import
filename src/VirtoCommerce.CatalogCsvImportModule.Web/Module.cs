using CsvHelper.Configuration;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogCsvImportModule.Core;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Data;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using System.Diagnostics;
using System.Threading;

namespace VirtoCommerce.CatalogCsvImportModule.Web
{
    public class Module : IModule
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ICsvCatalogExporter, CsvCatalogExporter>();
            serviceCollection.AddTransient<ICsvCatalogImporter, CsvCatalogImporter>();
            serviceCollection.AddTransient<ICsvProductConverter, CsvProductConverter>();

            serviceCollection.AddSingleton<Func<CsvProductMappingConfiguration, ClassMap>>(config => new CsvProductMap<CsvProduct>(config));

            serviceCollection.AddAutoMapper(typeof(DataAssemblyMarker).Assembly);
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        }

        public void Uninstall()
        {
            // No need in actions
        }
    }
}
