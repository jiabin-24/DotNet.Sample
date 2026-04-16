namespace ConsoleApp.Data;

internal class OrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public virtual Order Order { get; set; } = null!;
}
