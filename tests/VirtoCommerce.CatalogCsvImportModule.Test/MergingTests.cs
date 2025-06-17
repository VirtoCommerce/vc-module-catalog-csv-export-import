using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests
{
    public class MergingTests
    {
        [Fact]
        public void CsvProductMergeTest_ProductHasSameImages_ImagesUpdated()
        {
            //Arrange
            var catalogProduct = GetCatalogProductWithImage();
            var csvProduct = new CsvProduct()
            {
                Images = new List<Image>() { new Image() { Id = "", Url = "SameURL" } }
            };
            //Act
            csvProduct.MergeFrom(catalogProduct);

            //Assets
            Assert.Single(csvProduct.Images);
            Assert.NotNull(csvProduct.Images.FirstOrDefault(x => x.Id == "1"));
        }

        [Fact]
        public void CsvProductMergeTest_ProductHasAnotherImages_ImagesAdded()
        {
            //Arrange
            var catalogProduct = GetCatalogProductWithImage();

            var csvProduct = new CsvProduct()
            {
                Images = new List<Image>() { new Image() { Id = "", Url = "AnotherUrl" } }
            };

            //Act
            csvProduct.MergeFrom(catalogProduct);

            //Assert
            Assert.Equal(2, csvProduct.Images.Count);
            Assert.NotNull(csvProduct.Images.FirstOrDefault(x => x.Id == "1"));
            Assert.NotNull(csvProduct.Images.FirstOrDefault(x => x.Id == ""));
        }

        private CatalogProduct GetCatalogProductWithImage()
        {
            return new CatalogProduct()
            {
                Images = new List<Image>() { new Image() { Id = "1", Url = "SameURL" } },
                Assets = new List<Asset>(),
                Reviews = new List<EditorialReview>(),
                Properties = new List<Property>(),
                SeoInfos = new List<SeoInfo>()
            };
        }

    }
}
