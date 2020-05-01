using System;
using System.Linq.Expressions;
using Core.Entities;

namespace Core.Specifications
{
  public class ProductsWithTypesAndBrandsSpecification : BaseSpecification<Product>
  {
    // Call into Base,base has a contstructor that takes criteria. We can use that constructor.
    // HEre we'll use a lambda expression but use || and && to build up the expression dependin on if a brandId and typeID is present.
    public ProductsWithTypesAndBrandsSpecification(ProductSpecParams productSpecParams) :
    base(x => 
      (string.IsNullOrEmpty(productSpecParams.Search) || x.Name.ToLower().Contains(productSpecParams.Search))
      && (!productSpecParams.BrandId.HasValue || x.ProductBrandId == productSpecParams.BrandId) 
      && (!productSpecParams.TypeId.HasValue || x.ProductTypeId == productSpecParams.TypeId))
    {
      AddInclude(p => p.ProductType);
      AddInclude(p => p.ProductBrand);
      // Order by Name by default
      AddOrderBy(x => x.Name);
      // Apply paging, skip has to be the pagesize * the pageindex -1. The minus 1 is if we are on page 1 we dont' want to skip any.
      ApplyPaging(productSpecParams.PageSize * (productSpecParams.PageIndex - 1), productSpecParams.PageSize);

      if (!string.IsNullOrEmpty(productSpecParams.Sort))
      {
        switch (productSpecParams.Sort)
        {
          case "priceAsc":
            AddOrderBy(p => p.Price);
            break;
          case "priceDesc":
            AddOrderByDescending(p => p.Price);
            break;
          default:
            AddOrderBy(p => p.Name);
            break;
        }
      }
    }

    public ProductsWithTypesAndBrandsSpecification(int id) : base(x => x.Id == id)
    {
      AddInclude(p => p.ProductType);
      AddInclude(p => p.ProductBrand);
    }
  }
}