using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.CatalogCsvImportModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Seo.Core.Models;
using Xunit;

namespace VirtoCommerce.CatalogCsvImportModule.Tests;

public class MergingTests
{
    [Fact]
    public void CsvProductMergeTest_ProductHasSameImages_ImagesUpdated()
    {
        // Arrange
        var existingProduct = GetExistingProduct();

        var csvProduct = new CsvProduct
        {
            Images = new List<Image> { new() { Url = "Original URL", AltText = "New text" } },
        };

        // Act
        csvProduct.MergeFrom(existingProduct);

        // Assert
        csvProduct.Images.Should().HaveCount(1);
        var image1 = csvProduct.Images[0];
        Assert.Equal("1", image1.Id);
        Assert.Equal("Original URL", image1.Url);
        Assert.Equal("New text", image1.AltText);
    }

    [Fact]
    public void CsvProductMergeTest_ProductHasAnotherImages_ImagesAdded()
    {
        // Arrange
        var existingProduct = GetExistingProduct();

        var csvProduct = new CsvProduct
        {
            Images = new List<Image> { new() { Url = "New URL", AltText = "New text" } },
        };

        // Act
        csvProduct.MergeFrom(existingProduct);

        // Assert
        csvProduct.Images.Should().HaveCount(2);

        var image1 = csvProduct.Images[0];
        Assert.Null(image1.Id);
        Assert.Equal("New URL", image1.Url);
        Assert.Equal("New text", image1.AltText);

        var image2 = csvProduct.Images[1];
        Assert.Equal("1", image2.Id);
        Assert.Equal("Original URL", image2.Url);
        Assert.Equal("Original text", image2.AltText);
    }

    [Fact]
    public void CsvProductMergeTest_ProductHasSameSeoUrl_SeoUpdated()
    {
        // Arrange
        var existingProduct = GetExistingProduct();

        var csvProduct = new CsvProduct
        {
            SeoInfos = new List<SeoInfo> { new CsvSeoInfo { SemanticUrl = "Original URL", PageTitle = "New title" } },
        };

        // Act
        csvProduct.MergeFrom(existingProduct);

        // Assert
        csvProduct.SeoInfos.Should().HaveCount(1);
        var seo1 = csvProduct.SeoInfos[0];
        Assert.Equal("1", seo1.Id);
        Assert.Equal("Original URL", seo1.SemanticUrl);
        Assert.Equal("New title", seo1.PageTitle);
    }

    [Fact]
    public void CsvProductMergeTest_ProductHasDifferentSeoUrl_SeoReplaced()
    {
        // Arrange
        var existingProduct = GetExistingProduct();

        var csvProduct = new CsvProduct
        {
            SeoInfos = new List<SeoInfo> { new CsvSeoInfo { SemanticUrl = "New URL", PageTitle = "New title" } },
        };

        // Act
        csvProduct.MergeFrom(existingProduct);

        // Assert
        csvProduct.SeoInfos.Should().HaveCount(2);

        var seo1 = csvProduct.SeoInfos[0];
        Assert.Null(seo1.Id);
        Assert.Equal("New URL", seo1.SemanticUrl);
        Assert.Equal("New title", seo1.PageTitle);

        var seo2 = csvProduct.SeoInfos[1];
        Assert.Equal("1", seo2.Id);
        Assert.Equal("Original URL", seo2.SemanticUrl);
        Assert.Equal("Original title", seo2.PageTitle);
    }


    private static CatalogProduct GetExistingProduct()
    {
        return new CatalogProduct
        {
            Images = new List<Image> { new() { Id = "1", Url = "Original URL", AltText = "Original text" } },
            Assets = new List<Asset>(),
            Reviews = new List<EditorialReview>(),
            Properties = new List<Property>(),
            SeoInfos = new List<SeoInfo> { new() { Id = "1", SemanticUrl = "Original URL", PageTitle = "Original title" } },
        };
    }
}
