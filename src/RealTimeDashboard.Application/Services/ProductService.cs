using Microsoft.EntityFrameworkCore;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Application.Mapping;
using RealTimeDashboard.Domain.Entities;
using RealTimeDashboard.Domain.Exceptions;

namespace RealTimeDashboard.Application.Services;

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _db;
    private readonly ProductMapper _mapper = new();

    public ProductService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.Products.AsNoTracking().OrderBy(p => p.Name);
        var total = await baseQuery.CountAsync(ct);
        var entities = await baseQuery
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(ct);
        var items = entities.Select(_mapper.ToDto).ToList();

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<ProductDto> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Product), id);
        return _mapper.ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            Category = request.Category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return _mapper.ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        product.Name = request.Name;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.Category = request.Category;
        product.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);

        return _mapper.ToDto(product);
    }
}
