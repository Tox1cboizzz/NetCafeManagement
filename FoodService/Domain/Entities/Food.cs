using SharedKernel.BaseEntities;

namespace FoodService.Domain.Entities;

public class MenuItem : BaseEntity
{
    public string Name { get; private set; } = null!;
    public FoodCategory Category { get; private set; }
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; }

    private MenuItem() { }

    public static MenuItem Create(string name, FoodCategory category, decimal price)
        => new() { Name = name, Category = category, Price = price, IsAvailable = true };

    public void Update(string name, decimal price) { Name = name; Price = price; SetUpdated(); }
    public void ToggleAvailability() { IsAvailable = !IsAvailable; SetUpdated(); }
}

public class Order : BaseEntity
{
    public Guid SessionId { get; private set; }
    public Guid ItemId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }

    private Order() { }

    public static Order Create(Guid sessionId, Guid itemId, int quantity, decimal unitPrice)
        => new() { SessionId = sessionId, ItemId = itemId, Quantity = quantity, UnitPrice = unitPrice, TotalPrice = quantity * unitPrice };
}

public enum FoodCategory { Food = 1, Drink = 2, Snack = 3 }
