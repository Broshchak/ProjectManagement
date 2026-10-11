using Application.Common.Interfaces;
using Application.Dtos.Products;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Route("api/products")]
[ApiController]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts(CancellationToken ct = default)
    {
        var products = await productService.GetProductsAsync(ct);
        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id, CancellationToken ct = default)
    {
        var product = await productService.GetProductAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] UpsertProductDto dto, CancellationToken ct = default)
    {
        try
        {
            var product = await productService.CreateProductAsync(dto, ct);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpsertProductDto dto, CancellationToken ct = default)
    {
        try
        {
            var product = await productService.UpdateProductAsync(id, dto, ct);
            return product is null ? NotFound(new { message = "Product was not found." }) : Ok(product);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:int}/stock")]
    public async Task<IActionResult> GetProductStock(int id, CancellationToken ct = default)
    {
        var stock = await productService.GetProductStockAsync(id, ct);
        return stock is null ? NotFound(new { message = "Product was not found." }) : Ok(stock);
    }

    [HttpPut("{id:int}/stock")]
    public async Task<IActionResult> UpdateProductStock(int id, [FromBody] UpdateProductStockDto dto, CancellationToken ct = default)
    {
        try
        {
            var stock = await productService.UpdateProductStockAsync(id, dto, ct);
            return stock is null ? NotFound(new { message = "Product was not found." }) : Ok(stock);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
