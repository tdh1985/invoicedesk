using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Tests.TestSupport;

public static class TestData
{
    public const string ValidAbn = "51824753556";

    public static Task<Client> AddClientAsync(this TestEnv env, string name = "Acme Pty Ltd") =>
        env.Get<ClientService>().SaveAsync(new Client { Name = name });

    // tax invoices need the seller's name and abn before they can be sent
    public static Task MakeReadyToIssueAsync(this TestEnv env) =>
        env.SetProfileAsync(p =>
        {
            p.Name = "Downey Digital";
            p.Abn = ValidAbn;
        });

    public static async Task SetProfileAsync(this TestEnv env, Action<BusinessProfile> change)
    {
        var svc = env.Get<ProfileService>();
        var profile = await svc.GetAsync();
        change(profile);
        await svc.SaveAsync(profile);
    }

    public static async Task<Invoice> AddInvoiceAsync(
        this TestEnv env, int clientId, long unitCents, bool gst = true, bool send = false,
        int issuedDaysAgo = 0, int termsDays = 14)
    {
        var svc = env.Get<InvoiceService>();
        var inv = await svc.NewDraftAsync(clientId);
        inv.GstEnabled = gst;
        inv.IssueDate = env.Today.AddDays(-issuedDaysAgo);
        inv.DueDate = inv.IssueDate.AddDays(termsDays);
        inv.Lines = [new InvoiceLine { Description = "Consulting", Quantity = 1, UnitPriceCents = unitCents }];
        inv = await svc.SaveAsync(inv);
        if (send)
        {
            await env.MakeReadyToIssueAsync();
            await svc.MarkSentAsync(inv.Id, null);
        }
        return (await svc.GetAsync(inv.Id))!;
    }

    public static Task<StagedFile> StageFileAsync(this TestEnv env, string name, int bytes = 64) =>
        env.Get<AttachmentStore>().StageAsync(
            new MemoryStream(Enumerable.Repeat((byte)7, bytes).ToArray()), name, bytes);

    public static string[] AttachmentFiles(this TestEnv env) =>
        Directory.Exists(env.Paths.Attachments)
            ? Directory.GetFiles(env.Paths.Attachments, "*", SearchOption.AllDirectories)
            : [];
}
