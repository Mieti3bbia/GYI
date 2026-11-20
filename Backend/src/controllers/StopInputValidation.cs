using GYI.src.dataModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FYI.Controllers;

internal static class StopInputValidation
{
    internal static void Validate(ModelStateDictionary state, DraftStopDetails details, string prefix = "")
    {
        ValidateText(state, prefix + "name", details.Name);
        ValidateText(state, prefix + "description", details.Description);
        ValidateText(state, prefix + "municipality", details.Municipality);
        ValidateText(state, prefix + "province", details.Province);
        ValidateCoordinate(state, prefix + "latitude", details.Latitude, 90, "latitudine");
        ValidateCoordinate(state, prefix + "longitude", details.Longitude, 180, "longitudine");

        if (details.Cap is not { Length: 5 } cap || cap == "00000" || !cap.All(char.IsAsciiDigit))
            state.AddModelError(prefix + "cap", "Inserisci un CAP valido di cinque cifre.");

        if (details.Price is not { } price || price < 0 || decimal.Round(price, 2) != price)
            state.AddModelError(prefix + "price", "Inserisci un prezzo non negativo con al massimo due decimali.");

        if (!string.IsNullOrWhiteSpace(details.SourceUrl) &&
            (details.SourceUrl.Trim().Any(char.IsControl) ||
             !Uri.TryCreate(details.SourceUrl.Trim(), UriKind.Absolute, out var source) ||
             (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps) ||
             string.IsNullOrWhiteSpace(source.Host)))
            state.AddModelError(prefix + "sourceUrl", "Inserisci un URL completo che inizi con http:// o https://.");
    }

    internal static void ValidateText(ModelStateDictionary state, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            state.AddModelError(field, "Campo obbligatorio.");
        else if (value.Contains('\0'))
            state.AddModelError(field, "Il campo contiene un carattere non valido.");
    }

    private static void ValidateCoordinate(
        ModelStateDictionary state, string field, decimal? value, int limit, string label)
    {
        if (!value.HasValue || value < -limit || value > limit)
            state.AddModelError(field, $"La {label} deve essere tra {-limit} e {limit}.");
    }
}
