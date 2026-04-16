namespace ConsoleApp.Data;

internal class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
