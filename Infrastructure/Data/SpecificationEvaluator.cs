using System.Linq;
using Core.Entities;
using Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class SpecificationEvaluator<TEntity> where TEntity : BaseEntity
    {
        public static IQueryable<TEntity> GetQuery(IQueryable<TEntity> inputQuery, ISpecification<TEntity> spec)
        {
            var query = inputQuery;

            // If we have a criteria in the specification then add it to the query.
            if (spec.Criteria != null)
            {
                query = query.Where(spec.Criteria); // p => p.ProductTypeId == id
            }
            // If we have an OrderBy then add it on to the query
            if (spec.OrderBy != null)
            {
                query = query.OrderBy(spec.OrderBy); // p => p.ProductTypeId == id
            }
                        // If we have an OrderBy then add it on to the query
            if (spec.OrderByDescending != null)
            {
                query = query.OrderByDescending(spec.OrderByDescending); // p => p.ProductTypeId == id
            }

            if (spec.isPagingEnabled)
            {
                query = query.Skip(spec.Skip).Take(spec.Take);
            }

            // Go over all the includes, aggregate them into include expressions.
            query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

            return query;
        }
    }
}