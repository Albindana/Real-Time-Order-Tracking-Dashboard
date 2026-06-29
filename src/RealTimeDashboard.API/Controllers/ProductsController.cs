using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Infrastructure.Auth;

namespace RealTimeDashboard.API.Controllers;

[Authorize]
public class ProductsController : ApiControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] PaginationQuery query, CancellationToken ct)
        => Ok(await _productService.GetProductsAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _productService.GetProductByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = AuthService.AdminRole)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await _productService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AuthService.AdminRole)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
        => Ok(await _productService.UpdateAsync(id, request, ct));
}
