# Good and Bad Tests

## Good Tests

**Integration-style**: Test through real interfaces, not mocks of internal parts.

```csharp
// GOOD: Tests observable behavior
[TestMethod]
public async Task UserCanCheckoutWithValidCart()
{
    var cart = CreateCart();
    cart.Add(product);
    var result = await Checkout(cart, paymentMethod);
    result.Status.Should().Be("confirmed");
}
```

Characteristics:

- Tests behavior users/callers care about
- Uses public API only
- Survives internal refactors
- Describes WHAT, not HOW
- One logical assertion per test

## Bad Tests

**Implementation-detail tests**: Coupled to internal structure.

```csharp
// BAD: Tests implementation details
[TestMethod]
public async Task CheckoutCallsPaymentServiceProcess()
{
    var mockPayment = new Mock<IPaymentService>();
    await Checkout(cart, payment);
    mockPayment.Verify(p => p.Process(cart.Total), Times.Once);
}
```

Red flags:

- Mocking internal collaborators
- Testing private methods
- Asserting on call counts/order
- Test breaks when refactoring without behavior change
- Test name describes HOW not WHAT
- Verifying through external means instead of interface

```csharp
// BAD: Bypasses interface to verify
[TestMethod]
public async Task CreateUserSavesToDatabase()
{
    await CreateUser(new UserRequest { Name = "Alice" });
    var row = await db.QueryAsync("SELECT * FROM users WHERE name = @name", new { name = "Alice" });
    row.Should().NotBeNull();
}

// GOOD: Verifies through interface
[TestMethod]
public async Task CreateUserMakesUserRetrievable()
{
    var user = await CreateUser(new UserRequest { Name = "Alice" });
    var retrieved = await GetUser(user.Id);
    retrieved.Name.Should().Be("Alice");
}
```
