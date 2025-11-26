using System.Runtime.CompilerServices;

namespace OptionsRiskEngine.Models;

public readonly struct OptionParameters
{
    public readonly double SpotPrice;
    public readonly double StrikePrice;
    public readonly double TimeToMaturity;
    public readonly double RiskFreeRate;
    public readonly double Volatility;

    public OptionParameters(double spotPrice, double strikePrice, double timeToMaturity, 
        double riskFreeRate, double volatility)
    {
        SpotPrice = spotPrice;
        StrikePrice = strikePrice;
        TimeToMaturity = timeToMaturity;
        RiskFreeRate = riskFreeRate;
        Volatility = volatility;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Validate()
    {
        if (SpotPrice <= 0) throw new ArgumentException("Spot price must be positive", nameof(SpotPrice));
        if (StrikePrice <= 0) throw new ArgumentException("Strike price must be positive", nameof(StrikePrice));
        if (TimeToMaturity <= 0) throw new ArgumentException("Time to maturity must be positive", nameof(TimeToMaturity));
        if (RiskFreeRate < 0) throw new ArgumentException("Risk-free rate cannot be negative", nameof(RiskFreeRate));
        if (Volatility <= 0) throw new ArgumentException("Volatility must be positive", nameof(Volatility));
    }
}
