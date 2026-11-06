namespace EvoOffer.Models;

public sealed class ServiceDefinition
{
    public string Name { get; set; } = string.Empty;
    public string UnitOfMeasurement { get; set; } = string.Empty;
    public decimal PriceExcludingVat { get; set; }
}
