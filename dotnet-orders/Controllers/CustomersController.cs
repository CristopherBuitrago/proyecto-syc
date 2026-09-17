using Microsoft.AspNetCore.Mvc;
using OrdersApi.DTOs;
using OrdersApi.Services;

namespace OrdersApi.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> List()
    {
        return Ok(await _customerService.ListAsync());
    }
}
