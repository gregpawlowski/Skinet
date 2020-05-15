using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities;
using Core.Entities.OrderAggregate;
using Core.Interfaces;
using Core.Specifications;

namespace Infrastructure.Services
{
  public class OrderService : IOrderService
  {
    private readonly IBasketRepository _basketRepository;
    private readonly IUnitOfWork _unitOfWork;
    public OrderService(IBasketRepository basketRepository, IUnitOfWork unitOfWork)
    {
      _unitOfWork = unitOfWork;
      _basketRepository = basketRepository;
    }

    public async Task<Order> CreateOrderAsync(string buyerEmail, int deliveryMethodId, string basketId, Address shippingAddress)
    {
      // Get basket from repo.
      var basket = await _basketRepository.GetBasketAsync(basketId);

      // We can't trust the price in the basket, we'll have to take the price from the product.
      // Get items from product repo
      var items = new List<OrderItem>();

      foreach (var item in basket.Items)
      {
        // Get the prodcut from the repository
        var productItem = await _unitOfWork.Repository<Product>().GetByIdAsync(item.Id);

        // Create the snapshot of hte item at this give moment in time.
        var itemOrdered = new ProductItemOrdered(productItem.Id, productItem.Name, productItem.PictureUrl);

        // Create the order item, taking the price and quantity fro mthe proudct in the database not the basket.
        var orderItem = new OrderItem(itemOrdered, productItem.Price, item.Quantity);

        items.Add(orderItem);
      }

      // Get the delivery method from repo
      var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod>().GetByIdAsync(deliveryMethodId);

      // Calculat subtotal
      var subtotal = items.Sum(item => item.Price * item.Quantity);

      // Create order
      var order = new Order(items, buyerEmail, shippingAddress, deliveryMethod, subtotal);

      // Save the Order
      _unitOfWork.Repository<Order>().Add(order);

      var result = await _unitOfWork.Complete();

      if (result <= 0)
        return null;
      
      // Delete bacsket
      await _basketRepository.DeleteBasketAsync(basketId);
      // Return the order
      return order;
    }

    public async Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodsAsync()
    {
      return await _unitOfWork.Repository<DeliveryMethod>().ListAllAsync();
    }

    public async Task<Order> GetOrderByIdAsync(int id, string buyerEmail)
    {
      // We need to add Eager loading for Orders. Need a new specification.
      // Sort them by date as well so we need a new specification.
      var spec = new OrdersWithItemsAndOrderingSpecification(id, buyerEmail);

      return await _unitOfWork.Repository<Order>().GetEntityWithSpec(spec);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersForUserAsync(string buyerEmail)
    {
      var spec = new OrdersWithItemsAndOrderingSpecification(buyerEmail);

      return await _unitOfWork.Repository<Order>().ListAsync(spec);
    }
  }
}