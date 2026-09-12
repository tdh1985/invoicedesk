namespace InvoiceDesk.Core.Domain;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Direction Direction { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
}
