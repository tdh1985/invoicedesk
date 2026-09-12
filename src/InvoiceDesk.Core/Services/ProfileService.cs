using System.Text.RegularExpressions;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed partial class ProfileService(IDbContextFactory<AppDbContext> factory, AttachmentStore store)
{
    public event Action? Changed;

    public async Task<BusinessProfile> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Profiles.AsNoTracking().Include(p => p.LogoAttachment).SingleAsync();
    }

    public async Task SaveAsync(BusinessProfile profile)
    {
        Normalise(profile);
        ValidationException.ThrowIfAny(Validate(profile));

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Profiles.SingleAsync();
        var logoId = existing.LogoAttachmentId;
        db.Entry(existing).CurrentValues.SetValues(profile);
        // the logo only changes through SetLogoAsync so a stale form can't drop it
        existing.LogoAttachmentId = logoId;
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task SetLogoAsync(StagedFile? file)
    {
        if (file is not null && !file.IsImage)
            throw new ValidationException("The logo must be a JPG, PNG or WebP image.");

        await using var db = await factory.CreateDbContextAsync();
        var profile = await db.Profiles.Include(p => p.LogoAttachment).SingleAsync();
        var old = profile.LogoAttachment;

        Attachment? added = null;
        if (file is not null)
        {
            added = store.Import(file.TempPath, file.OriginalFileName, AttachmentKind.Logo);
            profile.LogoAttachment = added;
        }
        else
        {
            profile.LogoAttachment = null;
            profile.LogoAttachmentId = null;
        }
        if (old is not null) db.Attachments.Remove(old);

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            if (added is not null) store.DeleteFile(added);
            throw;
        }

        if (old is not null) store.DeleteFile(old);
        if (file is not null) store.DiscardStaged([file]);
        Changed?.Invoke();
    }

    public static List<string> Validate(BusinessProfile p)
    {
        var errors = new List<string>();
        if (p.Abn.Length > 0 && !Abn.IsValid(p.Abn)) errors.Add("ABN must be 11 digits and pass the ATO check.");
        if (!HexColour().IsMatch(p.AccentColour)) errors.Add("Accent colour must look like #4F46E5.");
        if (p.Bsb.Length > 0 && !BsbFormat().IsMatch(p.Bsb)) errors.Add("BSB must be 6 digits.");
        if (p.NumberPadding is < 0 or > 8) errors.Add("Number padding must be between 0 and 8.");
        if (p.NextInvoiceNumber < 1) errors.Add("Next invoice number must be 1 or more.");
        if (p.PaymentTermsDays is < 0 or > 365) errors.Add("Payment terms must be between 0 and 365 days.");
        if (p.GstRateBasisPoints is < 0 or > 10000) errors.Add("GST rate must be between 0% and 100%.");
        if (p.InvoicePrefix.Length > 12) errors.Add("Invoice prefix must be 12 characters or fewer.");
        return errors;
    }

    static void Normalise(BusinessProfile p)
    {
        p.Name = Text.Clean(p.Name);
        p.Abn = Text.Clean(p.Abn).Replace(" ", "");
        p.Address = Text.Clean(p.Address);
        p.Email = Text.Clean(p.Email);
        p.Phone = Text.Clean(p.Phone);
        p.BankAccountName = Text.Clean(p.BankAccountName);
        p.AccountNumber = Text.Clean(p.AccountNumber);
        p.AccentColour = Text.Clean(p.AccentColour);
        p.InvoicePrefix = Text.Clean(p.InvoicePrefix);
        p.FooterNote = Text.Clean(p.FooterNote);
        var bsbDigits = new string(Text.Clean(p.Bsb).Where(char.IsAsciiDigit).ToArray());
        p.Bsb = bsbDigits.Length == 6 ? $"{bsbDigits[..3]}-{bsbDigits[3..]}" : Text.Clean(p.Bsb);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColour();

    [GeneratedRegex(@"^\d{3}-\d{3}$")]
    private static partial Regex BsbFormat();
}
