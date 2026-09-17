using Microsoft.AspNetCore.Mvc;
using OrdersApi.DTOs;
using OrdersApi.Services;

namespace OrdersApi.Controllers;

[ApiController]
[Route("api/coupons")]
public class CouponsController : ControllerBase
{
    private readonly CouponService _couponService;

    public CouponsController(CouponService couponService)
    {
        _couponService = couponService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CouponResponse>>> List()
    {
        return Ok(await _couponService.ListActiveAsync());
    }
}
