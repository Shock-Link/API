namespace OpenShock.Common.Models;

public sealed class Paginated<T>
{
    public required int Offset { get; set; }
    public required int Limit { get; set; }
    public required long Total { get; set; }
    public required T[] Data { get; set; }
}