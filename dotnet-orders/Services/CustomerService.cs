using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;
using OrdersApi.DTOs;

namespace OrdersApi.Services;

// Solo lectura: el frontend lo usa para el selector de "sesión de cliente simulada"
// (ver DECISIONS.md, punto 8), no hay CRUD de clientes en el alcance de la prueba.
public class CustomerService
{
    private readonly OrdersDbContext _db;

    public CustomerService(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<List<CustomerResponse>> ListAsync()
    {
        return await _db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new CustomerResponse(c.Id, c.Name, c.Email))
            .ToListAsync();
    }
}
