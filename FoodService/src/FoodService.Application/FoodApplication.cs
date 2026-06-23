using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using FoodService.Domain.Entities;
using FoodService.Infrastructure.Data;

namespace FoodService.Application.DTOs;
public record MenuItemDto(Guid Id, string Name, string Category, decimal Price, bool IsAvailable);
public record OrderDto(Guid Id, Guid SessionId, Guid ItemId, string ItemName, int Quantity, decimal UnitPrice, decimal TotalPrice);
public record CreateMenuItemRequest(string Name, string Category, decimal Price);
public record CreateOrderRequest(Guid SessionId, Guid ItemId, int Quantity);
public record GetOrdersBySessionRequest(Guid SessionId);

namespace FoodService.Application.Commands;
using FoodService.Application.DTOs;

public record CreateMenuItemCommand(string Name, string Category, decimal Price) : IRequest<Result<MenuItemDto>>;
public record CreateOrderCommand(Guid SessionId, Guid ItemId, int Quantity) : IRequest<Result<OrderDto>>;
public record GetMenuQuery : IRequest<Result<List<MenuItemDto>>>;
public record GetOrdersBySessionQuery(Guid SessionId) : IRequest<Result<List<OrderDto>>>;

public class CreateMenuItemCommandHandler : IRequestHandler<CreateMenuItemCommand, Result<MenuItemDto>>
{
    private readonly FoodDbContext _context;
    public CreateMenuItemCommandHandler(FoodDbContext ctx) => _context = ctx;
    public async Task<Result<MenuItemDto>> Handle(CreateMenuItemCommand req, CancellationToken ct)
    {
        if (!Enum.TryParse<FoodCategory>(req.Category, true, out var cat)) return Result<MenuItemDto>.Failure("Invalid category");
        var item = MenuItem.Create(req.Name, cat, req.Price);
        _context.MenuItems.Add(item);
        await _context.SaveChangesAsync(ct);
        return Result<MenuItemDto>.Success(new MenuItemDto(item.Id, item.Name, item.Category.ToString(), item.Price, item.IsAvailable));
    }
}

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly FoodDbContext _context;
    public CreateOrderCommandHandler(FoodDbContext ctx) => _context = ctx;
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand req, CancellationToken ct)
    {
        var item = await _context.MenuItems.FindAsync(req.ItemId);
        if (item == null || !item.IsAvailable) return Result<OrderDto>.Failure("Item not available");
        var order = Order.Create(req.SessionId, req.ItemId, req.Quantity, item.Price);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(ct);
        return Result<OrderDto>.Success(new OrderDto(order.Id, order.SessionId, order.ItemId, item.Name, order.Quantity, order.UnitPrice, order.TotalPrice));
    }
}

public class GetMenuQueryHandler : IRequestHandler<GetMenuQuery, Result<List<MenuItemDto>>>
{
    private readonly FoodDbContext _context;
    public GetMenuQueryHandler(FoodDbContext ctx) => _context = ctx;
    public async Task<Result<List<MenuItemDto>>> Handle(GetMenuQuery req, CancellationToken ct)
    {
        var items = await _context.MenuItems.Where(i => !i.IsDeleted).Select(i => new MenuItemDto(i.Id, i.Name, i.Category.ToString(), i.Price, i.IsAvailable)).ToListAsync(ct);
        return Result<List<MenuItemDto>>.Success(items);
    }
}

public class GetOrdersBySessionQueryHandler : IRequestHandler<GetOrdersBySessionQuery, Result<List<OrderDto>>>
{
    private readonly FoodDbContext _context;
    public GetOrdersBySessionQueryHandler(FoodDbContext ctx) => _context = ctx;
    public async Task<Result<List<OrderDto>>> Handle(GetOrdersBySessionQuery req, CancellationToken ct)
    {
        var orders = await _context.Orders.Where(o => o.SessionId == req.SessionId)
            .Join(_context.MenuItems, o => o.ItemId, m => m.Id, (o, m) => new OrderDto(o.Id, o.SessionId, o.ItemId, m.Name, o.Quantity, o.UnitPrice, o.TotalPrice))
            .ToListAsync(ct);
        return Result<List<OrderDto>>.Success(orders);
    }
}
