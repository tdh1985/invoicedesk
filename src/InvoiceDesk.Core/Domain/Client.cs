namespace InvoiceDesk.Core.Domain;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Abn { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    public List<Invoice> Invoices { get; set; } = [];

    public Client Clone() => new()
    {
        Id = Id, Name = Name, ContactName = ContactName, Email = Email, Phone = Phone,
        Address = Address, Abn = Abn, Notes = Notes, CreatedAt = CreatedAt, IsArchived = IsArchived,
    };
}
