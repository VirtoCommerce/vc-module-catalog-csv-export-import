using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Seo.Core.Models;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Model;

public class CsvSeoInfo : SeoInfo
{
    public virtual void MergeFrom(SeoInfo source)
    {
        if (Id.IsNullOrEmpty())
        {
            Id = source.Id;
        }

        if (SemanticUrl.IsNullOrEmpty())
        {
            SemanticUrl = source.SemanticUrl;
        }

        if (LanguageCode.IsNullOrEmpty())
        {
            LanguageCode = source.LanguageCode;
        }

        if (StoreId.IsNullOrEmpty())
        {
            StoreId = source.StoreId;
        }

        if (PageTitle.IsNullOrEmpty())
        {
            PageTitle = source.PageTitle;
        }

        if (MetaDescription.IsNullOrEmpty())
        {
            MetaDescription = source.MetaDescription;
        }

        if (MetaKeywords.IsNullOrEmpty())
        {
            MetaKeywords = source.MetaKeywords;
        }

        if (ImageAltDescription.IsNullOrEmpty())
        {
            ImageAltDescription = source.ImageAltDescription;
        }

        ObjectId = source.ObjectId;
        ObjectType = source.ObjectType;
    }
}
