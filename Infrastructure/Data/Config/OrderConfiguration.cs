using System;
using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Config
{
  public class OrderConfiguration : IEntityTypeConfiguration<Order>
  {
    public void Configure(EntityTypeBuilder<Order> builder)
    {
      builder.OwnsOne(o => o.ShipToAddress, a =>
      {
        a.WithOwner();
      });

      builder.Property(s => s.Status)
        //Convert our enum to a string.
        .HasConversion(o => o.ToString(), o => (OrderStatus)Enum.Parse(typeof(OrderStatus), o));

      // If we delete an order we want to delte any Order Items.
      builder.HasMany(o => o.OrderItems)
          .WithOne()
          .OnDelete(DeleteBehavior.Cascade);

      builder.Property(o => o.Subtotal)
          .HasColumnType("decimal(18,2)");
    }
  }
}