namespace InvoiceDesk.Core.Domain;

public enum InvoiceStatus { Draft = 0, Sent = 1, Void = 2 }

// what the ui shows, worked out from status, payments and due date
public enum DisplayStatus { Draft, Sent, PartPaid, Paid, Overdue, Void }

public enum Direction { In = 0, Out = 1 }

public enum PaymentMethod { None = 0, BankTransfer = 1, Card = 2, Cash = 3, Other = 4 }

public enum AttachmentKind { Receipt = 0, SentInvoicePdf = 1, Logo = 2, Other = 3 }
