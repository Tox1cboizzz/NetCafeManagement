// ── DTOs ──────────────────────────────────────────────────────────────────────
namespace MachineService.Application.DTOs;

public record MachineDto(Guid Id, string Name, string Type, decimal PricePerHour, string Status, Guid? CurrentUserId, string? Specs);
public record CreateMachineRequest(string Name, string Type, decimal PricePerHour, string? Specs);
public record UpdateMachineRequest(string Name, decimal PricePerHour, string? Specs);
public record OccupyMachineRequest(Guid UserId);

// ── Commands & Queries ────────────────────────────────────────────────────────
namespace MachineService.Application.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;
using MachineService.Application.DTOs;
using MachineService.Domain.Entities;
using MachineService.Infrastructure.Data;

public record CreateMachineCommand(string Name, string Type, decimal PricePerHour, string? Specs) : IRequest<Result<MachineDto>>;
public record OccupyMachineCommand(Guid MachineId, Guid UserId) : IRequest<Result<MachineDto>>;
public record ReleaseMachineCommand(Guid MachineId) : IRequest<Result<MachineDto>>;
public record GetAllMachinesQuery : IRequest<Result<List<MachineDto>>>;
public record GetMachineByIdQuery(Guid Id) : IRequest<Result<MachineDto>>;

public class CreateMachineCommandHandler : IRequestHandler<CreateMachineCommand, Result<MachineDto>>
{
    private readonly MachineDbContext _context;
    public CreateMachineCommandHandler(MachineDbContext ctx) => _context = ctx;

    public async Task<Result<MachineDto>> Handle(CreateMachineCommand req, CancellationToken ct)
    {
        if (!Enum.TryParse<MachineType>(req.Type, true, out var type))
            return Result<MachineDto>.Failure("Invalid machine type. Use Normal or Premium");

        var machine = Machine.Create(req.Name, type, req.PricePerHour, req.Specs);
        _context.Machines.Add(machine);
        await _context.SaveChangesAsync(ct);
        return Result<MachineDto>.Success(ToDto(machine));
    }
}

public class OccupyMachineCommandHandler : IRequestHandler<OccupyMachineCommand, Result<MachineDto>>
{
    private readonly MachineDbContext _context;
    public OccupyMachineCommandHandler(MachineDbContext ctx) => _context = ctx;

    public async Task<Result<MachineDto>> Handle(OccupyMachineCommand req, CancellationToken ct)
    {
        var machine = await _context.Machines.FindAsync(req.MachineId);
        if (machine == null) return Result<MachineDto>.Failure("Machine not found");
        if (machine.Status != MachineStatus.Available) return Result<MachineDto>.Failure("Machine is not available");

        machine.Occupy(req.UserId);
        await _context.SaveChangesAsync(ct);
        return Result<MachineDto>.Success(ToDto(machine));
    }
}

public class ReleaseMachineCommandHandler : IRequestHandler<ReleaseMachineCommand, Result<MachineDto>>
{
    private readonly MachineDbContext _context;
    public ReleaseMachineCommandHandler(MachineDbContext ctx) => _context = ctx;

    public async Task<Result<MachineDto>> Handle(ReleaseMachineCommand req, CancellationToken ct)
    {
        var machine = await _context.Machines.FindAsync(req.MachineId);
        if (machine == null) return Result<MachineDto>.Failure("Machine not found");
        machine.Release();
        await _context.SaveChangesAsync(ct);
        return Result<MachineDto>.Success(ToDto(machine));
    }
}

public class GetAllMachinesQueryHandler : IRequestHandler<GetAllMachinesQuery, Result<List<MachineDto>>>
{
    private readonly MachineDbContext _context;
    public GetAllMachinesQueryHandler(MachineDbContext ctx) => _context = ctx;

    public async Task<Result<List<MachineDto>>> Handle(GetAllMachinesQuery req, CancellationToken ct)
    {
        var machines = await _context.Machines.Where(m => !m.IsDeleted).ToListAsync(ct);
        return Result<List<MachineDto>>.Success(machines.Select(ToDto).ToList());
    }
}

public class GetMachineByIdQueryHandler : IRequestHandler<GetMachineByIdQuery, Result<MachineDto>>
{
    private readonly MachineDbContext _context;
    public GetMachineByIdQueryHandler(MachineDbContext ctx) => _context = ctx;

    public async Task<Result<MachineDto>> Handle(GetMachineByIdQuery req, CancellationToken ct)
    {
        var machine = await _context.Machines.FindAsync(req.Id);
        return machine == null ? Result<MachineDto>.Failure("Not found") : Result<MachineDto>.Success(ToDto(machine));
    }
}

file static MachineDto ToDto(Machine m) =>
    new(m.Id, m.Name, m.Type.ToString(), m.PricePerHour, m.Status.ToString(), m.CurrentUserId, m.Specs);
