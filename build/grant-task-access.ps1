# Erlaubt normalen (nicht-administrativen) Benutzern, den bereits von
# installer.iss angelegten Taskplaner-Task "SerialManagerUpdate" ABZUFRAGEN
# und AUSZULOESEN (schtasks /Query, schtasks /Run) - OHNE ihn aendern oder
# loeschen zu koennen.
#
# Hintergrund/Bug, den dieses Skript behebt: "schtasks /Create ... /RU
# SYSTEM" legt einen Task standardmaessig mit einer Sicherheitsbeschreibung
# an, die NUR Mitgliedern der Administratoren-Gruppe (und SYSTEM selbst)
# erlaubt, den Task abzufragen oder auszuloesen - ein normaler, nicht-
# administrativer Benutzer bekommt bei "schtasks /Query" oder "schtasks
# /Run" schlicht "Zugriff verweigert". Genau das hat UpdateService.cs'
# ScheduledUpdateTaskExists() (per "schtasks /Query", laeuft als normaler
# Benutzer) immer "false" liefern lassen - und dadurch bei JEDEM Update
# wieder den alten UAC-Dialog ausgeloest, obwohl der Task laengst existierte.
#
# Wird direkt im Anschluss an "schtasks /Create" ausgefuehrt (siehe [Run] in
# installer.iss), also noch waehrend der (admin-pflichtigen) Installation -
# genau hier, mit bereits vorhandenen Administratorrechten, muss die
# Berechtigung gesetzt werden, damit die App sie spaeter (als normaler
# Benutzer) nie wieder selbst aendern muesste.
$taskName = "SerialManagerUpdate"

try {
    $service = New-Object -ComObject "Schedule.Service"
    $service.Connect()
    $folder = $service.GetFolder("\")
    $task = $folder.GetTask($taskName)

    # SDDL-Sicherheitsbeschreibung fuer den Task:
    #   O:BA   - Eigentuemer: Builtin Administrators
    #   G:SY   - Gruppe: SYSTEM
    #   D:     - Discretionary ACL (wer darf was):
    #     (A;;FA;;;SY)  - SYSTEM: voller Zugriff (fuehrt den Task aus)
    #     (A;;FA;;;BA)  - Administratoren: voller Zugriff (aendern/loeschen,
    #                     z. B. beim Deinstallieren)
    #     (A;;GRGX;;;AU)- Authenticated Users (alle angemeldeten Benutzer,
    #                     auch ohne Adminrechte): NUR Lesen (GR, fuer
    #                     "schtasks /Query") und Ausfuehren (GX, fuer
    #                     "schtasks /Run") - kein Aendern/Loeschen moeglich.
    $sddl = "O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;AU)"

    $task.SetSecurityDescriptor($sddl, 0)
}
catch {
    # Best effort - falls das aus irgendeinem Grund fehlschlaegt (z. B. der
    # Task wurde entgegen Erwartung doch nicht angelegt), soll die
    # Installation trotzdem nicht abbrechen. In diesem Fall faellt
    # UpdateService.cs beim naechsten Update einfach wieder auf den
    # klassischen UAC-Dialog zurueck - kein Datenverlust, nur unbequemer.
}
