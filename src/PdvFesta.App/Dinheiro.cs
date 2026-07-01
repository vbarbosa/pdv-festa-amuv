using System.Globalization;
using PdvFesta.Core;

namespace PdvFesta.App;

/// <summary>
/// Conversao robusta de texto digitado -> centavos (int). Blinda contra letras,
/// separadores trocados e "R$". Centraliza a regra para todas as telas de entrada.
/// </summary>
public static class Dinheiro
{
    /// <summary>Interpreta "R$ 1.234,56", "12,50", "50" como centavos. null se invalido.</summary>
    public static int? ParseCentavos(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return null;
        var limpo = txt.Trim()
            .Replace("R$", "").Replace(" ", "")
            .Replace(".", "")       // milhar
            .Replace(',', '.');     // decimal
        if (decimal.TryParse(limpo, NumberStyles.Any, CultureInfo.InvariantCulture, out var reais) && reais >= 0)
            return (int)Math.Round(reais * 100);
        return null;
    }

    /// <summary>Formata centavos como "R$ 12,50".</summary>
    public static string Formatar(int centavos) => CupomFormatter.Moeda(centavos);

    /// <summary>Formata centavos como "12,50" (sem "R$"), para preencher campos de entrada.</summary>
    public static string FormatarSemSimbolo(int centavos) =>
        (centavos / 100m).ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
}
