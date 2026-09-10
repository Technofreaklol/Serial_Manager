using Microsoft.EntityFrameworkCore;
using SerialManager.Data;
using SerialManager.Services;
using SerialManager.Views;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Velopack;

namespace SerialManager;

public partial class App : Application
{
    // ---------------------------------------------------------------
    // Eigener Einstiegspunkt statt des automatisch generierten WPF-
    // Main(). VelopackApp.Build().Run() MUSS als Allererstes passieren:
    // Beim (Erst-)Start nach der Installation, bei einem Update oder
    // bei der Deinstallation übernimmt Velopack hier kurz die
    // Kontrolle (z. B. um Verknüpfungen anzulegen) und beendet den
    // Prozess danach sofort wieder - der restliche Code darf also
    // nicht vorher laufen.
    // ---------------------------------------------------------------
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticLogService.Write("Nicht behandelter UI-Fehler.", e.Exception);

        MessageBox.Show(
            "Ein unerwarteter Fehler ist aufgetreten. Die Anwendung wurde nicht automatisch beendet.\n\n" +
            e.Exception.Message + "\n\n" +
            "Details wurden in der Diagnose-Logdatei gespeichert.",
            "Unerwarteter Fehler",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            DiagnosticLogService.Write("Nicht behandelter Anwendungsfehler.", ex);
        else
            DiagnosticLogService.Write("Nicht behandelter Anwendungsfehler: " + e.ExceptionObject);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // ---------------------------------------------------------
        // Splash Screen anzeigen
        // ---------------------------------------------------------
        var splash = new SplashWindow();
        splash.Show();

        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);

        // ---------------------------------------------------------
        // Erststart: Es existiert noch keine Datenbank-Konfiguration
        // (database.json). Ohne diese Prüfung würde
        // DatabaseConfigurationService.Load() weiter unten im Hintergrund
        // unbemerkt eine leere Standard-SQLite-Datenbank anlegen, ohne
        // dem Benutzer je die Möglichkeit zu geben, stattdessen eine
        // bereits vorhandene Datenbank auszuwählen. Deshalb hier VOR
        // dem ersten Load() explizit die Datenbankeinrichtung zeigen.
        // ---------------------------------------------------------
        if (!File.Exists(AppPaths.DatabaseConfigFile))
        {
            splash.Hide();

            MessageBox.Show(
                "Es wurde noch keine Datenbank eingerichtet.\n\n" +
                "Wähle im folgenden Fenster entweder eine bereits " +
                "vorhandene Datenbank aus (\"Durchsuchen...\") oder " +
                "übernimm die vorausgefüllte SQLite-Datei und klicke " +
                "auf \"Initialisieren\", um eine neue Datenbank " +
                "anzulegen. Danach mit \"Speichern\" bestätigen.",
                "Datenbank einrichten",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var firstStartSetupWindow = new DatabaseSetupWindow();

            if (firstStartSetupWindow.ShowDialog() != true)
            {
                splash.Close();
                Shutdown();
                return;
            }

            splash.Show();

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);
        }

        while (true)
        {
            var initializer = new DatabaseInitializer();

            // -----------------------------------------------------
            // Datenbankverbindung prüfen
            // -----------------------------------------------------
            splash.SetStatus("Datenbankverbindung wird geprüft …");

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);

            var databaseCheck = await Task.Run(() =>
            {
                string errorMessage;

                bool canStart =
                    initializer.CanStartApplication(out errorMessage);

                return new DatabaseCheckResult(
                    canStart,
                    errorMessage);
            });

            if (databaseCheck.CanStart)
            {
                // ---------------------------------------------
                // Datenbankstruktur prüfen (Tabellen/Migrationen)
                // ---------------------------------------------
                if (await EnsureDatabaseStructureAsync(splash))
                    break;

                // Struktur konnte nicht automatisch hergestellt
                // werden → direkt zur Datenbankeinrichtung springen.
                splash.Hide();

                MessageBox.Show(
                    "Mit der Datenbankstruktur stimmt etwas nicht " +
                    "(fehlende oder fehlerhafte Tabellen) und konnte " +
                    "nicht automatisch behoben werden.\n\n" +
                    "Die Datenbankeinrichtung wird geöffnet.",
                    "Datenbankproblem",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                var setupWindow = new DatabaseSetupWindow();

                if (setupWindow.ShowDialog() != true)
                {
                    splash.Close();
                    Shutdown();
                    return;
                }

                splash.Show();

                await Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);

                continue;
            }

            // -----------------------------------------------------
            // Datenbankfehler (keine Verbindung möglich)
            // -----------------------------------------------------
            splash.Hide();

            var configForError = new DatabaseConfigurationService().Load();

            var result = MessageBox.Show(
                $"Die {configForError.Provider}-Datenbank konnte nicht erreicht werden.\n\n" +
                $"{databaseCheck.Message}\n\n" +
                "Ja = Datenbankeinstellungen öffnen\n" +
                "Nein = Mit SQLite starten\n" +
                "Abbrechen = Anwendung beenden",
                "Datenbankfehler",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            // -----------------------------------------------------
            // Datenbankeinstellungen öffnen
            // -----------------------------------------------------
            if (result == MessageBoxResult.Yes)
            {
                var window = new DatabaseSetupWindow();

                if (window.ShowDialog() != true)
                {
                    splash.Close();
                    Shutdown();
                    return;
                }

                splash.Show();

                await Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);

                continue;
            }

            // -----------------------------------------------------
            // Mit SQLite starten
            // -----------------------------------------------------
            if (result == MessageBoxResult.No)
            {
                splash.Show();

                splash.SetStatus("SQLite wird eingerichtet …");

                await Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);

                var configService =
                    new DatabaseConfigurationService();

                var config = configService.Load();

                config.Provider = "SQLite";

                configService.Save(config);

                var databaseInitialization = await Task.Run(() =>
                {
                    string initMessage;

                    bool initialized =
                        initializer.InitializeDatabase(
                            out initMessage);

                    return new DatabaseInitializationResult(
                        initialized,
                        initMessage);
                });

                if (!databaseInitialization.Success)
                {
                    splash.Hide();

                    MessageBox.Show(
                        databaseInitialization.Message,
                        "Fehler beim Initialisieren der Datenbank",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    splash.Close();
                    Shutdown();
                    return;
                }

                break;
            }

            // -----------------------------------------------------
            // Abbrechen
            // -----------------------------------------------------
            splash.Close();
            Shutdown();
            return;
        }

        // ---------------------------------------------------------
        // Einstellungen laden
        // ---------------------------------------------------------
        splash.SetStatus("Einstellungen werden geladen …");

        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);

        InitializeSettings();

        // ---------------------------------------------------------
        // Benutzeranmeldung
        // ---------------------------------------------------------
        splash.SetStatus("Anmeldung wird geprüft …");

        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);

        var userService = new UserService();

        if (!userService.HasUsers())
        {
            splash.Hide();

            var createAdmin = new CreateAdminWindow();

            if (createAdmin.ShowDialog() != true)
            {
                splash.Close();
                Shutdown();
                return;
            }

            splash.Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        splash.Hide();

        var login = new LoginWindow();

        if (login.ShowDialog() != true)
        {
            splash.Close();
            Shutdown();
            return;
        }

        CurrentSession.CurrentUser = login.AuthenticatedUser;

        splash.Show();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        // ---------------------------------------------------------
        // Hauptfenster laden
        // ---------------------------------------------------------
        splash.SetStatus("Oberfläche wird geladen …");

        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.Render);

        MainWindow = new MainWindow();
        MainWindow.Show();

        // ---------------------------------------------------------
        // Splash ausblenden
        // ---------------------------------------------------------
        await splash.CloseAnimatedAsync();
    }

    private async Task<bool> EnsureDatabaseStructureAsync(SplashWindow splash)
    {
        var config = new DatabaseConfigurationService().Load();
        var initializer = new DatabaseInitializer();

        if (config.Provider == "SQLite" || config.Provider == "MSSQL")
        {
            // Beide nutzen bewusst keine EF-Migrationen (siehe
            // DatabaseInitializer), sondern EnsureCreated() + eigene
            // Sicherheitsnetz-Prüfungen. GetPendingMigrations() weiter unten
            // würde hier fälschlich IMMER "Reparatur nötig" melden und bei
            // JEDEM Start ein Backup anstoßen - deshalb hier direkt und ohne
            // Migrationsverlauf initialisieren, wie bei SQLite.
            splash.SetStatus(config.Provider == "SQLite"
                ? "SQLite-Datenbank wird vorbereitet …"
                : "SQL-Server-Datenbank wird vorbereitet …");

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);

            return await Task.Run(() =>
            {
                string message;
                return initializer.InitializeDatabase(out message);
            });
        }

        // MySQL: sowohl ausstehende Migrationen als auch fehlende
        // Tabellen trotz "aktuellem" Verlauf berücksichtigen.
        bool needsRepair;

        try
        {
            needsRepair = await Task.Run(() =>
            {
                using var db = DbContextFactory.Create();

                if (db.Database.GetPendingMigrations().Any())
                    return true;

                try
                {
                    _ = db.Articles.Take(1).Count();
                    _ = db.Machines.Take(1).Count();
                    _ = db.SerialHistories.Take(1).Count();
                    _ = db.Settings.Take(1).Count();
                    _ = db.Users.Take(1).Count();
                    _ = db.AuditLogEntries.Take(1).Count();
                    return false;
                }
                catch
                {
                    return true;
                }
            });
        }
        catch
        {
            return false;
        }

        if (needsRepair)
        {
            splash.SetStatus("Datenbank wird gesichert …");

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);

            var backupService = new BackupService();
            string? backupError = null;

            bool backupOk = await Task.Run(() =>
            {
                try
                {
                    if (backupService.HasExistingDatabase())
                        backupService.CreateBackup();

                    return true;
                }
                catch (Exception ex)
                {
                    backupError = ex.Message;
                    return false;
                }
            });

            if (!backupOk)
            {
                splash.Hide();

                var proceedAnyway = MessageBox.Show(
                    "Vor der automatischen Datenbank-Aktualisierung konnte kein Backup " +
                    $"erstellt werden:\n\n{backupError}\n\nTrotzdem fortfahren?",
                    "Backup fehlgeschlagen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (proceedAnyway != MessageBoxResult.Yes)
                    return false;

                splash.Show();

                await Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);
            }

            splash.SetStatus("Datenbank wird aktualisiert …");

            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);

            bool migrated = await Task.Run(() =>
            {
                string message;
                return initializer.InitializeDatabase(out message);
            });

            if (!migrated)
                return false;
        }

        // Abschließende Kontrolle: Sind die wichtigsten Tabellen jetzt
        // tatsächlich lesbar?
        return await Task.Run(() =>
        {
            try
            {
                using var db = DbContextFactory.Create();
                _ = db.Articles.Take(1).Count();
                _ = db.Users.Take(1).Count();
                _ = db.AuditLogEntries.Take(1).Count();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }
    private void InitializeSettings()
    {
        try
        {
            var settingsService = new SettingsService();

            // Firmenname
            if (string.IsNullOrWhiteSpace(
                settingsService.GetValue("CompanyName")))
            {
                settingsService.SetValue(
                    "CompanyName",
                    "Stewe");
            }

            // Anzahl Backups
            if (string.IsNullOrWhiteSpace(
                settingsService.GetValue("BackupCount")))
            {
                settingsService.SetValue(
                    "BackupCount",
                    "20");
            }

            // Automatisches Backup
            if (string.IsNullOrWhiteSpace(
                settingsService.GetValue("AutoBackup")))
            {
                settingsService.SetValue(
                    "AutoBackup",
                    "false");
            }

            // Automatisches Backup
            if (settingsService.GetBool("AutoBackup"))
            {
                try
                {
                    new BackupService().CreateBackup();
                }
                catch
                {
                    // Backupfehler ignorieren
                }
            }

            // Button-Farbe: VOR dem ersten sichtbaren Fenster (Login etc.)
            // anwenden, damit die vom Benutzer gewählte Farbe von Anfang an
            // überall greift, nicht erst nach dem Öffnen der Einstellungen.
            ThemeService.ApplyPersistedButtonColor();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Die Anwendung konnte die Einstellungen nicht laden.\n\n" +
                ex.Message,
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }


    // -------------------------------------------------------------
    // Ergebnis der Datenbankprüfung
    // -------------------------------------------------------------
    private sealed record DatabaseCheckResult(
        bool CanStart,
        string Message);


    // -------------------------------------------------------------
    // Ergebnis der Datenbankinitialisierung
    // -------------------------------------------------------------
    private sealed record DatabaseInitializationResult(
        bool Success,
        string Message);
}