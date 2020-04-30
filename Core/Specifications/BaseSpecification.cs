using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Core.Specifications
{
  public class BaseSpecification<T> : ISpecification<T>
  {
    public BaseSpecification()
    {
    }

    public BaseSpecification(Expression<Func<T, bool>> criteria)
    {
      Criteria = criteria;
    }

    public Expression<Func<T, bool>> Criteria { get; }

    // The list of includes will be a list of expressions that take a type and object.
    public List<Expression<Func<T, object>>> Includes { get;} = new List<Expression<Func<T, object>>>();

    // This method will be used to add ot the list of includes.
    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }
  }
}