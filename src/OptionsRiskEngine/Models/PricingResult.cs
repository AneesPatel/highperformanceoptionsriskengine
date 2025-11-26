namespace OptionsRiskEngine.Models;

public readonly struct PricingResult
{
    public readonly double Price;
    public readonly double Delta;
    public readonly double Gamma;
    public readonly double Vega;
    public readonly double StandardError;

    public PricingResult(double price, double delta, double gamma, double vega, double standardError)
    {
        Price = price;
        Delta = delta;
        Gamma = gamma;
        Vega = vega;
        StandardError = standardError;
    }

    public override string ToString()
    {
        return $"Price: {Price:F4}, Delta: {Delta:F4}, Gamma: {Gamma:F6}, Vega: {Vega:F4}, SE: {StandardError:F6}";
    }
}
