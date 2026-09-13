using System;
using System.Security.Cryptography;
using System.Text;

namespace SerialManager.Services;

/// <summary>
/// Ver-/entschlüsselt sensible Werte (aktuell: die Datenbank-Passwörter in
/// database.json) mit der Windows Data Protection API (DPAPI),
/// CurrentUser-Scope.
///
/// Das passt genau zu dem Ordner, in dem database.json ohnehin schon liegt
/// (%LocalAppData%, also pro Windows-Benutzer): Windows verknüpft den
/// verschlüsselten Wert fest mit dem Benutzerkonto, das ihn erzeugt hat -
/// kein eigenes Passwort, kein eigener Schlüssel, keine eigene
/// Schlüsselverwaltung nötig. Jeder, der die Datei liest (anderer Benutzer,
/// anderer PC), sieht nur unbrauchbaren Chiffretext.
///
/// Wichtige Einschränkung: Genau weil der Wert an das erzeugende
/// Windows-Benutzerkonto gebunden ist, funktioniert das NICHT, wenn
/// database.json zentral vorbereitet und per IT/Image auf viele PCs oder
/// Benutzerprofile kopiert wird - dort muss die Datenbank-Einrichtung
/// (DatabaseSetupWindow) einmal je PC/Benutzer durchlaufen werden, damit das
/// Passwort dort neu (mit dem jeweiligen Konto) verschlüsselt wird. Das ist
/// aber keine Verschlechterung gegenüber vorher: database.json lag schon
/// immer pro Benutzer in %LocalAppData%, nicht zentral.
/// </summary>
public static class SecretProtector
{
    // Kennzeichnet einen bereits verschlüsselten Wert - unterscheidet ihn
    // beim Laden von einem alten, noch im Klartext gespeicherten Passwort
    // (siehe Unprotect/IsProtected: Abwärtskompatibilität zu vor dieser
    // Änderung gespeicherten database.json-Dateien).
    private const string Prefix = "dpapi:";

    public static bool IsProtected(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText ?? "";

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Prefix + Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Gibt einen mit Protect() verschlüsselten Wert wieder im Klartext
    /// zurück. Ein Wert OHNE das "dpapi:"-Präfix wird unverändert
    /// zurückgegeben (alte, noch im Klartext gespeicherte Passwörter aus
    /// database.json-Dateien von vor dieser Änderung) - so funktionieren
    /// bestehende Installationen beim ersten Start nach dem Update sofort
    /// weiter, ohne dass Zugangsdaten neu eingegeben werden müssen.
    /// DatabaseConfigurationService.Load() erkennt diesen Fall zusätzlich
    /// und speichert automatisch einmalig verschlüsselt zurück.
    /// </summary>
    public static string Unprotect(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue) || !IsProtected(storedValue))
            return storedValue ?? "";

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(storedValue[Prefix.Length..]);

            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // Kann passieren, wenn die Datei von einem anderen Windows-Konto
            // oder einem anderen PC kopiert wurde (DPAPI ist fest an das
            // erzeugende Konto gebunden) - dann lieber leer zurückgeben
            // (führt weiter oben zu einer klaren "Zugangsdaten falsch"-
            // Fehlermeldung beim Verbindungsversuch) als einen kryptischen
            // Absturz zu riskieren.
            return "";
        }
    }
}
