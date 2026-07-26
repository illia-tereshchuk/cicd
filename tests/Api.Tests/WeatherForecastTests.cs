namespace Api.Tests;

public class WeatherForecastTests
{
    // [Fact] — один конкретний тест-кейс без параметрів.
    [Fact]
    public void FreezingPoint_ZeroCelsius_Is32Fahrenheit()
    {
        var forecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), 0, "Freezing");

        Assert.Equal(32, forecast.TemperatureF); // 0°C = 32°F
    }

    // [Theory] + [InlineData] — той самий тест на кількох наборах даних.
    // Значення порахованi за формулою: 32 + (int)(C / 0.5556), дріб відкидається.
    [Theory]
    [InlineData(0, 32)]
    [InlineData(20, 67)]
    [InlineData(-20, -3)]
    public void TemperatureF_MatchesFormula(int celsius, int expectedFahrenheit)
    {
        var forecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), celsius, null);

        Assert.Equal(expectedFahrenheit, forecast.TemperatureF);
    }
}
