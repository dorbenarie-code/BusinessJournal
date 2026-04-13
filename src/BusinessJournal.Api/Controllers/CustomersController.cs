using BusinessJournal.Api.Contracts.Customers;
using BusinessJournal.Application.Services;
using BusinessJournal.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace BusinessJournal.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        ArgumentNullException.ThrowIfNull(customerService);
        _customerService = customerService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<CustomerResponse> Create(RegisterCustomerRequest request)
    {
        var customer = _customerService.RegisterCustomer(
            request.FullName,
            request.PhoneNumber,
            request.Email);

        var response = ToResponse(customer);

        return CreatedAtAction(
            nameof(GetById),
            new { id = customer.Id },
            response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CustomerResponse> GetById(Guid id)
    {
        var customer = _customerService.FindCustomerById(id);

        if (customer is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(customer));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CustomerResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<CustomerResponse>> GetAll()
    {
        var customers = _customerService.GetAllCustomers()
            .Select(ToResponse)
            .ToList();

        return Ok(customers);
    }

    private static CustomerResponse ToResponse(Customer customer)
    {
        return new CustomerResponse
        {
            Id = customer.Id,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email
        };
    }
}