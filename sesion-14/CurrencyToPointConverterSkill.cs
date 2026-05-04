using Microsoft.Agents.AI;
using System.ComponentModel;
using System.Text.Json;

internal sealed class CurrencyToPointConverterSkill : AgentClassSkill<CurrencyToPointConverterSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } =
        new("currency-to-points-converter", "Convierte el valor de una divisa a puntos.");

    protected override string Instructions => """Usa los recursos.""";

    [AgentSkillResource("conversion-table")]
    [Description("Lookup table for factors.")]
    public string ConversionTable => """
                | Currency Range | Points Conversion |
        |----------------|------------------|
        | 0 – 200        | 1.7              |
        | 201 – 500      | 1.8              |
        | 501 – 1000     | 2.0              |
        """;

    [AgentSkillScript("convert")]
    [Description("Multiplies value by factor.")]
    private static string ConvertUnits(double value, double factor)
    {
        return JsonSerializer.Serialize(new { result = value * factor });
    }
}