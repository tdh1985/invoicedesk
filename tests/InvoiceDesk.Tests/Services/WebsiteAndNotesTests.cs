using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Tests.TestSupport;

namespace InvoiceDesk.Tests.Services;

public class WebAddressTests
{
    [Theory]
    [InlineData("https://www.harbourlane.com.au/", "www.harbourlane.com.au")]
    [InlineData("http://harbourlane.com.au", "harbourlane.com.au")]
    [InlineData("harbourlane.com.au/work", "harbourlane.com.au/work")]
    public void display_drops_the_scheme_and_trailing_slash(string input, string expected) =>
        Assert.Equal(expected, WebAddress.Display(input));

    [Theory]
    [InlineData("harbourlane.com.au", "https://harbourlane.com.au")]
    [InlineData("http://harbourlane.com.au", "http://harbourlane.com.au")]
    public void href_always_has_a_scheme(string input, string expected) =>
        Assert.Equal(expected, WebAddress.Href(input));

    [Theory]
    [InlineData("www.harbourlane.com.au", true)]
    [InlineData("https://harbourlane.com.au/contact", true)]
    [InlineData("harbour lane.com.au", false)]
    [InlineData("harbourlane", false)]
    public void validates(string url, bool ok) => Assert.Equal(ok, WebAddress.IsValid(url));
}

public class WebsiteAndNotesTests
{
    [Fact]
    public async Task profile_keeps_website_and_notes_trimmed()
    {
        await using var env = await TestEnv.CreateAsync();

        await env.SetProfileAsync(p =>
        {
            p.Website = "  www.harbourlane.com.au ";
            p.DefaultInvoiceNotes = " Thanks for your business! ";
            p.PrivateNotes = " Builder licence 12345 ";
        });

        var profile = await env.Get<ProfileService>().GetAsync();
        Assert.Equal("www.harbourlane.com.au", profile.Website);
        Assert.Equal("Thanks for your business!", profile.DefaultInvoiceNotes);
        Assert.Equal("Builder licence 12345", profile.PrivateNotes);
    }

    [Fact]
    public async Task profile_rejects_a_website_with_spaces()
    {
        await using var env = await TestEnv.CreateAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => env.SetProfileAsync(p => p.Website = "harbour lane.com"));

        Assert.Contains(ex.Errors, e => e.Contains("website", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task client_keeps_website_and_invoice_note()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ClientService>();

        var saved = await svc.SaveAsync(new Client { Name = "Acme", Website = " acme.example.com ", InvoiceNote = " PO 4471 " });

        var again = (await svc.GetAsync(saved.Id))!;
        Assert.Equal("acme.example.com", again.Website);
        Assert.Equal("PO 4471", again.InvoiceNote);
    }

    [Fact]
    public async Task client_rejects_a_bad_website()
    {
        await using var env = await TestEnv.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            env.Get<ClientService>().SaveAsync(new Client { Name = "Acme", Website = "not a site" }));
    }

    [Fact]
    public async Task new_invoices_start_with_the_default_notes()
    {
        await using var env = await TestEnv.CreateAsync();
        await env.SetProfileAsync(p => p.DefaultInvoiceNotes = "Payment within 14 days, thanks!");

        var draft = await env.Get<InvoiceService>().NewDraftAsync();

        Assert.Equal("Payment within 14 days, thanks!", draft.Notes);
    }

    [Fact]
    public async Task money_entries_keep_notes_and_find_them_in_search()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();

        var saved = await svc.SaveAsync(new Transaction
        {
            Direction = Direction.Out, Date = env.Today, AmountCents = 99000, GstCents = 9000,
            Party = "JB Hi-Fi", Description = "Laptop", Notes = " Warranty until March 2029 ",
        }, [], []);

        Assert.Equal("Warranty until March 2029", (await svc.GetAsync(saved.Id))!.Notes);
        Assert.Single(await svc.ListAsync(new TransactionFilter(Search: "warranty")));
    }
}
