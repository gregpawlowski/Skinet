using Core.Entities;

namespace Core.Specifications
{
  public class ProductsWithFiltersForCountSpecification : BaseSpecification<Product>
  {
    public ProductsWithFiltersForCountSpecification(ProductSpecParams productSpecParams)
    : base(x =>
      (string.IsNullOrEmpty(productSpecParams.Search) || x.Name.ToLower().Contains(productSpecParams.Search))
      && (!productSpecParams.BrandId.HasValue || x.ProductBrandId == productSpecParams.BrandId) 
      && (!productSpecParams.TypeId.HasValue || x.ProductTypeId == productSpecParams.TypeId))
    {
        
    }
  }
}