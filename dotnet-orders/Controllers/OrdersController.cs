using Microsoft.AspNetCore.Mvc;
using OrdersApi.DTOs;
using OrdersApi.Services;

namespace OrdersApi.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create([FromBody] CreateOrderRequest request)
    {
        try
        {
            var order = await _orderService.CreateOrderAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }
        catch (CustomerNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "CUSTOMER_NOT_FOUND"));
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "PRODUCT_NOT_FOUND"));
        }
        catch (CouponInvalidException ex)
        {
            return BadRequest(new ApiError(ex.Message, "COUPON_INVALID"));
        }
        catch (StockUnavailableException ex)
        {
            var details = new Dictionary<string, object>
            {
                ["failedItems"] = ex.Failures.Select(f => new { productId = f.ProductId, requested = f.Requested, available = f.Available })
            };
            return Conflict(new ApiError(ex.Message, "STOCK_UNAVAILABLE", details));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        try
        {
            return Ok(await _orderService.GetByIdAsync(id));
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "ORDER_NOT_FOUND"));
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderResponse>>> List(
        [FromQuery] int? customerId, [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _orderService.ListAsync(customerId, status, from, to, page, pageSize));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(int id)
    {
        try
        {
            return Ok(await _orderService.CancelAsync(id));
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new ApiError(ex.Message, "ORDER_NOT_FOUND"));
        }
        catch (OrderAlreadyCancelledException ex)
        {
            return BadRequest(new ApiError(ex.Message, "ORDER_ALREADY_CANCELLED"));
        }
    }
}
