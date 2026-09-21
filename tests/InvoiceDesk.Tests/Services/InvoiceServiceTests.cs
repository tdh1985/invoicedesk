// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Tests.TestSupport;

namespace InvoiceDesk.Tests.Services;

public class InvoiceServiceTests
{
    static PaymentInput Pay(TestEnv env, long cents) => new(env.Today, cents, PaymentMethod.BankTransfer, "");

    [Fact]
    public async Task new_draft_uses_profile_defaults()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();

        var draft = await env.Get<InvoiceService>().NewDraftAsync(client.Id);

        Assert.Equal(0, draft.Id);
        Assert.Equal(InvoiceStatus.Draft, draft.Status);
        Assert.Equal(client.Id, draft.ClientId);
        Assert.Equal(env.Today, draft.IssueDate);
        Assert.Equal(env.Today.AddDays(14), draft.DueDate);
        Assert.True(draft.GstEnabled);
        Assert.Equal(1000, draft.GstRateBasisPoints);
        Assert.Single(draft.Lines);
    }

    [Fact]
    public async Task new_draft_starts_with_gst_off_when_not_registered()
    {
        await using var env = await TestEnv.CreateAsync();
        await env.SetProfileAsync(p => p.GstRegistered = false);

        var draft = await env.Get<InvoiceService>().NewDraftAsync();

        Assert.False(draft.GstEnabled);
    }

    [Fact]
    public async Task numbers_are_assigned_in_sequence_and_never_reused()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();

        var a = await env.AddInvoiceAsync(client.Id, 1000);
        var b = await env.AddInvoiceAsync(client.Id, 1000);
        Assert.Equal("INV-0001", a.Number);
        Assert.Equal("INV-0002", b.Number);

        await env.SetProfileAsync(p => p.NextInvoiceNumber = 1);
        var c = await env.AddInvoiceAsync(client.Id, 1000);
        Assert.Equal("INV-0003", c.Number);
        Assert.Equal(4, (await env.Get<ProfileService>().GetAsync()).NextInvoiceNumber);
    }

    [Fact]
    public async Task save_requires_client_and_due_after_issue()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var draft = await svc.NewDraftAsync();

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(draft));

        draft.ClientId = (await env.AddClientAsync()).Id;
        draft.DueDate = draft.IssueDate.AddDays(-1);
        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(draft));
    }

    [Fact]
    public async Task updating_replaces_lines_and_keeps_number()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var client = await env.AddClientAsync();
        var inv = await env.AddInvoiceAsync(client.Id, 1000);
        inv.Lines =
        [
            new InvoiceLine { Description = "Design", Quantity = 2, UnitPriceCents = 5000 },
            new InvoiceLine { Description = "Hosting", Quantity = 1, UnitPriceCents = 2000, GstFree = true },
        ];

        await svc.SaveAsync(inv);

        var again = (await svc.GetAsync(inv.Id))!;
        Assert.Equal("INV-0001", again.Number);
        Assert.Equal(["Design", "Hosting"], again.Lines.Select(l => l.Description));
        Assert.Equal(13000, again.Totals().TotalCents);
    }

    [Fact]
    public async Task payments_move_status_to_part_paid_then_paid()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var client = await env.AddClientAsync();
        var inv = await env.AddInvoiceAsync(client.Id, 270000, send: true);

        var first = await svc.RecordPaymentAsync(inv.Id, Pay(env, 148500), []);
        Assert.Equal(13500, first.GstCents);
        Assert.Equal(Direction.In, first.Direction);
        Assert.Equal("Acme Pty Ltd", first.Party);
        Assert.Equal("Sales", first.Category!.Name);
        var summary = (await svc.ListAsync()).Single();
        Assert.Equal(DisplayStatus.PartPaid, summary.Status);
        Assert.Equal(148500, summary.BalanceCents);

        await svc.RecordPaymentAsync(inv.Id, Pay(env, 148500), []);
        summary = (await svc.ListAsync()).Single();
        Assert.Equal(DisplayStatus.Paid, summary.Status);
        Assert.Equal(0, summary.BalanceCents);
    }

    [Fact]
    public async Task payment_receipt_is_attached()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();
        var inv = await env.AddInvoiceAsync(client.Id, 10000, send: true);

        var tx = await env.Get<InvoiceService>().RecordPaymentAsync(inv.Id, Pay(env, 11000), [await env.StageFileAsync("remittance.pdf")]);

        Assert.Single(tx.Attachments);
        Assert.Single(env.AttachmentFiles());
    }

    [Fact]
    public async Task payment_on_draft_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000);

        await Assert.ThrowsAsync<ValidationException>(() => env.Get<InvoiceService>().RecordPaymentAsync(inv.Id, Pay(env, 100), []));
    }

    [Fact]
    public async Task zero_payment_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000, send: true);

        await Assert.ThrowsAsync<ValidationException>(() => env.Get<InvoiceService>().RecordPaymentAsync(inv.Id, Pay(env, 0), []));
    }

    [Fact]
    public async Task deleting_a_sent_invoice_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000, send: true);

        await Assert.ThrowsAsync<ValidationException>(() => env.Get<InvoiceService>().DeleteDraftAsync(inv.Id));
    }

    [Fact]
    public async Task deleting_a_draft_removes_it()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000);

        await svc.DeleteDraftAsync(inv.Id);

        Assert.Null(await svc.GetAsync(inv.Id));
    }

    [Fact]
    public async Task tax_invoice_without_abn_fails_issue_checks()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000);

        var errors = svc.ValidateForIssue(inv, await env.Get<ProfileService>().GetAsync());

        Assert.Contains(errors, e => e.Contains("ABN"));
        await Assert.ThrowsAsync<ValidationException>(() => svc.MarkSentAsync(inv.Id, null));
    }

    [Fact]
    public async Task gst_off_invoice_does_not_need_abn()
    {
        await using var env = await TestEnv.CreateAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000, gst: false);

        var errors = env.Get<InvoiceService>().ValidateForIssue(inv, await env.Get<ProfileService>().GetAsync());

        Assert.Empty(errors);
    }

    [Fact]
    public async Task issue_checks_need_descriptions_and_lines()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var profile = await env.Get<ProfileService>().GetAsync();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000, gst: false);

        inv.Lines[0].Description = " ";
        Assert.NotEmpty(svc.ValidateForIssue(inv, profile));

        inv.Lines.Clear();
        Assert.NotEmpty(svc.ValidateForIssue(inv, profile));
    }

    [Fact]
    public async Task editing_an_invoice_with_payments_is_rejected()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 10000, send: true);
        await svc.RecordPaymentAsync(inv.Id, Pay(env, 500), []);

        var loaded = (await svc.GetAsync(inv.Id))!;
        loaded.Lines[0].UnitPriceCents = 1;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(loaded));
    }

    [Fact]
    public async Task void_invoices_show_as_void_and_filter()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000, send: true);

        await svc.VoidAsync(inv.Id);

        Assert.Equal(DisplayStatus.Void, (await svc.ListAsync(InvoiceFilter.Void)).Single().Status);
        Assert.Empty(await svc.ListAsync(InvoiceFilter.Sent));
    }

    [Fact]
    public async Task overdue_filter_finds_late_invoices()
    {
        await using var env = await TestEnv.CreateAsync();
        var client = await env.AddClientAsync();
        await env.AddInvoiceAsync(client.Id, 1000, send: true, issuedDaysAgo: 30);
        await env.AddInvoiceAsync(client.Id, 1000, send: true);

        var overdue = await env.Get<InvoiceService>().ListAsync(InvoiceFilter.Overdue);

        Assert.Single(overdue);
        Assert.Equal(2, (await env.Get<InvoiceService>().ListAsync(InvoiceFilter.Sent)).Count);
    }

    [Fact]
    public async Task mark_sent_keeps_the_pdf()
    {
        await using var env = await TestEnv.CreateAsync();
        await env.MakeReadyToIssueAsync();
        var svc = env.Get<InvoiceService>();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1000);
        var pdf =Path.Combine(env.Paths.Exports, "INV-0001.pdf");
        File.WriteAllText(pdf, "%PDF-1.4");

        await svc.MarkSentAsync(inv.Id, pdf);

        var again = (await svc.GetAsync(inv.Id))!;
        Assert.Equal(InvoiceStatus.Sent, again.Status);
        Assert.NotNull(again.SentAt);
        Assert.Equal(AttachmentKind.SentInvoicePdf, again.Attachments.Single().Kind);
    }

    [Fact]
    public async Task duplicate_copies_lines_with_fresh_dates()
    {
        await using var env = await TestEnv.CreateAsync();
        var svc = env.Get<InvoiceService>();
        var inv = await env.AddInvoiceAsync((await env.AddClientAsync()).Id, 1234, send: true, issuedDaysAgo: 20);

        var copy = await svc.DuplicateAsync(inv.Id);

        Assert.Equal(0, copy.Id);
        Assert.Equal("", copy.Number);
        Assert.Equal(InvoiceStatus.Draft, copy.Status);
        Assert.Equal(env.Today, copy.IssueDate);
        Assert.Equal(1234, copy.Lines.Single().UnitPriceCents);
        Assert.Equal(0, copy.Lines.Single().Id);
    }

    [Fact]
    public async Task list_search_matches_number_and_client()
    {
        await using var env = await TestEnv.CreateAsync();
        await env.AddInvoiceAsync((await env.AddClientAsync("Acme")).Id, 1000);
        await env.AddInvoiceAsync((await env.AddClientAsync("Blue Fin")).Id, 1000);
        var svc = env.Get<InvoiceService>();

        Assert.Single(await svc.ListAsync(search: "blue"));
        Assert.Single(await svc.ListAsync(search: "0001"));
    }
}
