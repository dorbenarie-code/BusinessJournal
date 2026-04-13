using System;
using BusinessJournal.Domain.Common;

namespace BusinessJournal.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; }
    public string FullName { get; private set; }
    public string PhoneNumber { get; private set; }
    public string? Email { get; private set; }

    private Customer(Guid id, string fullName, string phoneNumber, string? email)
    {
        Id = id;
        FullName = TextNormalizer.NormalizeRequired(fullName, nameof(fullName), "Full name is required.");
        PhoneNumber = TextNormalizer.NormalizeRequired(phoneNumber, nameof(phoneNumber), "Phone number is required.");
        Email = TextNormalizer.NormalizeOptionalEmail(email);
    }

    public static Customer Create(string fullName, string phoneNumber, string? email = null)
    {
        return new Customer(
            Guid.NewGuid(),
            fullName,
            phoneNumber,
            email);
    }

    public static Customer Restore(Guid id, string fullName, string phoneNumber, string? email = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(id));
        }

        return new Customer(id, fullName, phoneNumber, email);
    }

    public void ChangeFullName(string fullName)
    {
        FullName = TextNormalizer.NormalizeRequired(fullName, nameof(fullName), "Full name is required.");
    }

    public void ChangePhoneNumber(string phoneNumber)
    {
        PhoneNumber = TextNormalizer.NormalizeRequired(phoneNumber, nameof(phoneNumber), "Phone number is required.");
    }

    public void ChangeEmail(string? email)
    {
        Email = TextNormalizer.NormalizeOptionalEmail(email);
    }
}