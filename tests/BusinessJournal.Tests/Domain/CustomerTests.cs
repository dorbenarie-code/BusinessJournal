using BusinessJournal.Domain.Entities;
using Xunit;

namespace BusinessJournal.Tests.Domain;

public sealed class CustomerTests
{
    [Fact]
    public void Create_WithValidValues_ShouldCreateCustomer()
    {
        var customer = Customer.Create("  Rachel Cohen  ", " 0501234567 ", "  Rachel@Gmail.com ");

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Rachel Cohen", customer.FullName);
        Assert.Equal("0501234567", customer.PhoneNumber);
        Assert.Equal("rachel@gmail.com", customer.Email);
    }

    [Fact]
    public void Create_WithEmptyFullName_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Customer.Create("   ", "0501234567"));
    }

    [Fact]
    public void Create_WithEmptyPhoneNumber_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Customer.Create("Rachel Cohen", "   "));
    }

    [Fact]
    public void ChangeFullName_WithValidValue_ShouldUpdateFullName()
    {
        var customer = Customer.Create("Rachel Cohen", "0501234567");

        customer.ChangeFullName("Rachel Levi");

        Assert.Equal("Rachel Levi", customer.FullName);
    }

    [Fact]
    public void ChangePhoneNumber_WithValidValue_ShouldUpdatePhoneNumber()
    {
        var customer = Customer.Create("Rachel Cohen", "0501234567");

        customer.ChangePhoneNumber("0529999999");

        Assert.Equal("0529999999", customer.PhoneNumber);
    }

    [Fact]
    public void ChangeEmail_WithNullOrWhitespace_ShouldSetEmailToNull()
    {
        var customer = Customer.Create("Rachel Cohen", "0501234567", "rachel@gmail.com");

        customer.ChangeEmail("   ");

        Assert.Null(customer.Email);
    }
    [Fact]
public void Restore_WithValidValues_ShouldRestoreCustomer()
{
    var id = Guid.NewGuid();

    var customer = Customer.Restore(
        id,
        "  Rachel Cohen  ",
        " 0501234567 ",
        " Rachel@Gmail.com ");

    Assert.Equal(id, customer.Id);
    Assert.Equal("Rachel Cohen", customer.FullName);
    Assert.Equal("0501234567", customer.PhoneNumber);
    Assert.Equal("rachel@gmail.com", customer.Email);
}
    
}