using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Tests.TestSupport;

namespace InvoiceDesk.Tests.Services;

public class TransactionServiceTests
{
    static Transaction Expense(TestEnv env, long amount = 5500, long gst = 500, int? categoryId = null) => new()
    {
        Direction = Direction.Out, Date = env.Today, AmountCents = amount, GstCents = gst,
        Party = "Officeworks", Description = "Printer ink", CategoryId = categoryId,
    };

    [Fact]
    public async Task expense_with_receipt_keeps_the_file()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();

        var saved = await svc.SaveAsync(Expense(env), [await env.StageFileAsync("receipt.jpg")], []);

        var again = (await svc.GetAsync(saved.Id))!;
        Assert.Single(again.Attachments);
        Assert.Single(env.AttachmentFiles());
    }

    [Fact]
    public async Task delete_removes_files()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();
        var saved = await svc.SaveAsync(Expense(env), [await env.StageFileAsync("receipt.pdf")], []);

        await svc.DeleteAsync(saved.Id);

        Assert.Null(await svc.GetAsync(saved.Id));
        Assert.Empty(env.AttachmentFiles());
    }

    [Fact]
    public async Task failed_save_leaves_no_files()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();
        var staged = await env.StageFileAsync("receipt.pdf");

        await Assert.ThrowsAnyAsync<Exception>(() => svc.SaveAsync(Expense(env, categoryId: 9999), [staged], []));

        Assert.Empty(env.AttachmentFiles());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000, 1001)]
    [InlineData(1000, -1)]
    public async Task bad_amounts_are_rejected(long amount, long gst)
    {
        await using var env = await TestEnv.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() =>
            env.Get<TransactionService>().SaveAsync(Expense(env, amount, gst), [], []));
    }

    [Fact]
    public async Task removing_an_attachment_deletes_its_file()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();
        var saved = await svc.SaveAsync(Expense(env), [await env.StageFileAsync("receipt.png")], []);
        var loaded = (await svc.GetAsync(saved.Id))!;

        await svc.SaveAsync(loaded, [], [loaded.Attachments[0].Id]);

        Assert.Empty((await svc.GetAsync(saved.Id))!.Attachments);
        Assert.Empty(env.AttachmentFiles());
    }

    [Fact]
    public async Task editing_keeps_existing_attachments()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();
        var saved = await svc.SaveAsync(Expense(env), [await env.StageFileAsync("a.png")], []);
        var loaded = (await svc.GetAsync(saved.Id))!;
        loaded.Description = "Toner";

        await svc.SaveAsync(loaded, [await env.StageFileAsync("b.pdf")], []);

        var again = (await svc.GetAsync(saved.Id))!;
        Assert.Equal("Toner", again.Description);
        Assert.Equal(2, again.Attachments.Count);
    }

    [Fact]
    public async Task list_filters_by_direction_range_category_and_search()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();
        var office = (await env.Get<CategoryService>().ListAsync(Direction.Out)).First(c => c.Name == "Office");
        await svc.SaveAsync(Expense(env, categoryId: office.Id), [], []);
        var old = Expense(env);
        old.Date = env.Today.AddMonths(-3);
        old.Party = "Bunnings";
        await svc.SaveAsync(old, [], []);
        await svc.SaveAsync(new Transaction { Direction = Direction.In, Date = env.Today, AmountCents = 100, Party = "ATO refund" }, [], []);

        Assert.Equal(2, (await svc.ListAsync(new TransactionFilter(Direction: Direction.Out))).Count);
        Assert.Equal(2, (await svc.ListAsync(new TransactionFilter(Range: FinancialPeriods.Month(env.Today)))).Count);
        Assert.Single(await svc.ListAsync(new TransactionFilter(CategoryId: office.Id)));
        Assert.Single(await svc.ListAsync(new TransactionFilter(Search: "bunn")));
    }

    [Fact]
    public async Task gst_suggestion_follows_registration()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<TransactionService>();
        var profile = await env.Get<ProfileService>().GetAsync();

        Assert.Equal(100, svc.SuggestGst(1100, profile));
        profile.GstRegistered = false;
        Assert.Equal(0, svc.SuggestGst(1100, profile));
    }
}

public class DashboardServiceTests
{
    [Fact]
    public async Task figures_add_up()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();
        var late = await env.AddInvoiceAsync(client.Id, 100000, send: true, issuedDaysAgo: 40);
        var paid = await env.AddInvoiceAsync(client.Id, 200000, send: true);
        await env.AddInvoiceAsync(client.Id, 99900);
        await env.Get<InvoiceService>().RecordPaymentAsync(paid.Id, new PaymentInput(env.Today, 220000, PaymentMethod.BankTransfer, ""), []);
        await env.Get<TransactionService>().SaveAsync(new Transaction
        {
            Direction = Direction.Out, Date = env.Today, AmountCents = 55000, GstCents = 5000, Party = "Officeworks",
        }, [], []);

        var d = await env.Get<DashboardService>().GetAsync();

        Assert.Equal(110000, d.OutstandingCents);
        Assert.Equal(1, d.OutstandingCount);
        Assert.Equal(110000, d.OverdueCents);
        Assert.Equal(1, d.OverdueCount);
        Assert.Equal(220000, d.ReceivedMonthCents);
        Assert.Equal(55000, d.SpentMonthCents);
        Assert.Equal(150000, d.ProfitFyCents);
        Assert.Equal(20000, d.GstCollectedQuarterCents);
        Assert.Equal(5000, d.GstPaidQuarterCents);
        Assert.Equal("FY 2026–27", d.FyLabel);
        Assert.Equal("Jul–Sep 2026", d.QuarterLabel);
        Assert.Equal(12, d.Months.Count);
        Assert.Equal(new MonthBar(new DateOnly(2026, 9, 1), 220000, 55000), d.Months[^1]);
        Assert.Equal(late.Id, d.Overdue.Single().Id);
        Assert.NotEmpty(d.Recent);
    }

    [Fact]
    public async Task void_invoices_are_not_outstanding()
    {
        await using var env = await TestEnv.CreateAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 100000, send: true, issuedDaysAgo: 40);
        await env.Get<InvoiceService>().VoidAsync(inv.Id);

        var d = await env.Get<DashboardService>().GetAsync();

        Assert.Equal(0, d.OutstandingCents);
        Assert.Empty(d.Overdue);
    }
}

public class SearchServiceTests
{
    [Fact]
    public async Task finds_clients_invoices_and_transactions()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync("Acme Pty Ltd");
        await env.AddInvoiceAsync(client.Id, 1000);
        await env.Get<TransactionService>().SaveAsync(new Transaction
        {
            Direction = Direction.Out, Date = env.Today, AmountCents = 1000, Party = "Acme Supplies",
        }, [], []);

        var results = await env.Get<SearchService>().SearchAsync("acme");

        Assert.Contains(results, r => r.Kind == SearchKind.Client && r.Id == client.Id);
        Assert.Contains(results, r => r.Kind == SearchKind.Invoice);
        Assert.Contains(results, r => r.Kind == SearchKind.Transaction);
    }

    [Fact]
    public async Task finds_invoice_by_number()
    {
        await using var env = await TestEnv.CreateAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000);

        var results = await env.Get<SearchService>().SearchAsync("inv-0001");

        Assert.Equal(inv.Id, results.Single(r => r.Kind == SearchKind.Invoice).Id);
    }

    [Fact]
    public async Task blank_query_returns_nothing()
    {
        await using var env = await TestEnv.CreateAsync();
        Assert.Empty(await env.Get<SearchService>().SearchAsync("  "));
    }
}
