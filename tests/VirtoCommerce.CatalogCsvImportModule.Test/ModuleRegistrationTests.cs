using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogCsvImportModule.Core.Services;
using VirtoCommerce.CatalogCsvImportModule.Data.Services;
using VirtoCommerce.CatalogCsvImportModule.Web;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class ModuleRegistrationTests
{
    [Fact]
    public void Initialize_Registers_CatalogCsvImportModuleMapper_AsSingleton()
    {
        var services = new ServiceCollection();

        new Module().Initialize(services);

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(ICatalogCsvImportModuleMapper));

        descriptor.Should().NotBeNull();
        descriptor.ImplementationType.Should().Be<CatalogCsvImportModuleMapper>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
