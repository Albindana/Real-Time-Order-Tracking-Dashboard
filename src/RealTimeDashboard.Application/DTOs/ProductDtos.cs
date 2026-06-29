namespace RealTimeDashboard.Application.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    decimal Price,
    int StockQuantity,
    string Category,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateProductRequest(
    string Name,
    decimal Price,
    int StockQuantity,
    string Category
);

public record UpdateProductRequest(
    string Name,
    decimal Price,
    int StockQuantity,
    string Category,
    bool IsActive
);
