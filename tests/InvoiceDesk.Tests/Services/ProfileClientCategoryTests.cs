using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Tests.TestSupport;

namespace InvoiceDesk.Tests.Services;

public class ProfileServiceTests
{
    [Fact]
    public async Task save_round_trips_and_normalises_abn()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ProfileService>();
        var p = await svc.GetAsync();
        p.Name = "Downey Digital";
        p.Abn = "51 824 753 556";

        await svc.SaveAsync(p);

        var again = await svc.GetAsync();
        Assert.Equal("Downey Digital", again.Name);
        Assert.Equal(TestData.ValidAbn, again.Abn);
    }

    [Fact]
    public async Task bad_abn_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ProfileService>();
        var p = await svc.GetAsync();
        p.Abn = "12 345";

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(p));
        Assert.Contains(ex.Errors, e => e.Contains("ABN"));
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#12345")]
    public async Task bad_accent_is_rejected(string colour)
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ProfileService>();
        var p = await svc.GetAsync();
        p.AccentColour = colour;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(p));
    }

    [Fact]
    public async Task changed_event_fires_on_save()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ProfileService>();
        var fired = 0;
        svc.Changed += () => fired++;

        await svc.SaveAsync(await svc.GetAsync());

        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task logo_can_be_set_and_removed()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ProfileService>();
        var store = env.Get<AttachmentStore>();

        await svc.SetLogoAsync(await env.StageFileAsync("logo.png"));
        var p = await svc.GetAsync();
        Assert.NotNull(p.LogoAttachment);
        var file = store.FullPath(p.LogoAttachment!);
        Assert.True(File.Exists(file));

        await svc.SetLogoAsync(null);
        p = await svc.GetAsync();
        Assert.Null(p.LogoAttachmentId);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task logo_must_be_an_image()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ProfileService>();

        await Assert.ThrowsAsync<ValidationException>(async () =>
            await svc.SetLogoAsync(await env.StageFileAsync("logo.pdf")));
    }
}

public class ClientServiceTests
{
    [Fact]
    public async Task name_is_required()
    {
        await using var env = await TestEnv.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => env.Get<ClientService>().SaveAsync(new Client { Name = "  " }));
    }

    [Fact]
    public async Task bad_client_abn_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() =>
            env.Get<ClientService>().SaveAsync(new Client { Name = "Acme", Abn = "999" }));
    }

    [Fact]
    public async Task search_matches_name_contact_or_email()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ClientService>();
        await svc.SaveAsync(new Client { Name = "Acme Pty Ltd" });
        await svc.SaveAsync(new Client { Name = "Blue Fin", ContactName = "Sam Acmeson" });
        await svc.SaveAsync(new Client { Name = "Cobalt", Email = "hello@cobalt.au" });

        Assert.Equal(2, (await svc.ListAsync("ACME")).Count);
        Assert.Single(await svc.ListAsync("cobalt.au"));
        Assert.Equal(3, (await svc.ListAsync()).Count);
    }

    [Fact]
    public async Task updates_keep_the_same_id()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ClientService>();
        var c = await svc.SaveAsync(new Client { Name = "Acme" });
        c.Email = "accounts@acme.test";

        var saved = await svc.SaveAsync(c);

        Assert.Equal(c.Id, saved.Id);
        Assert.Equal("accounts@acme.test", (await svc.GetAsync(c.Id))!.Email);
    }

    [Fact]
    public async Task deleting_client_with_invoices_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();
        await env.AddInvoiceAsync(client.Id, 1000);

        await Assert.ThrowsAsync<ValidationException>(() => env.Get<ClientService>().DeleteAsync(client.Id));
    }

    [Fact]
    public async Task deleting_client_without_invoices_works()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();

        await env.Get<ClientService>().DeleteAsync(client.Id);

        Assert.Null(await env.Get<ClientService>().GetAsync(client.Id));
    }

    [Fact]
    public async Task archived_clients_are_hidden_by_default()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<ClientService>();
        var client = await env.AddClientAsync();

        await svc.SetArchivedAsync(client.Id, true);

        Assert.Empty(await svc.ListAsync());
        Assert.Single(await svc.ListAsync(includeArchived: true));
    }

    [Fact]
    public async Task summary_totals_ignore_drafts_and_void()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();
        var sent = await env.AddInvoiceAsync(client.Id, 10000, send: true);
        await env.AddInvoiceAsync(client.Id, 5000);
        var voided = await env.AddInvoiceAsync(client.Id, 3000, send: true);
        await env.Get<InvoiceService>().VoidAsync(voided.Id);
        await env.Get<InvoiceService>().RecordPaymentAsync(sent.Id, new PaymentInput(env.Today, 1000, PaymentMethod.BankTransfer, ""), []);

        var summary = (await env.Get<ClientService>().ListAsync()).Single();

        Assert.Equal(3, summary.InvoiceCount);
        Assert.Equal(11000, summary.BilledCents);
        Assert.Equal(10000, summary.OutstandingCents);
    }
}

public class CategoryServiceTests
{
    [Fact]
    public async Task lists_by_direction_and_hides_archived()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<CategoryService>();
        var income = await svc.ListAsync(Direction.In);
        Assert.Equal(3, income.Count);

        await svc.SetArchivedAsync(income[1].Id, true);

        Assert.Equal(2, (await svc.ListAsync(Direction.In)).Count);
        Assert.Equal(3, (await svc.ListAsync(Direction.In, includeArchived: true)).Count);
    }

    [Fact]
    public async Task name_is_required()
    {
        await using var env = await TestEnv.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() =>
            env.Get<CategoryService>().SaveAsync(new Category { Name = "", Direction = Direction.Out }));
    }

    [Fact]
    public async Task new_categories_go_last()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<CategoryService>();

        await svc.SaveAsync(new Category { Name = "Training", Direction = Direction.Out });

        Assert.Equal("Training", (await svc.ListAsync(Direction.Out)).Last().Name);
    }

    [Fact]
    public async Task sales_category_is_found()
    {
        await using var env = await TestEnv.CreateAsync();
        Assert.Equal("Sales", (await env.Get<CategoryService>().GetSalesAsync()).Name);
    }
}
