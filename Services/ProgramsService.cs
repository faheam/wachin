using System.IO;
using System.Net.Http;
using Wachin.Models;

namespace Wachin.Services;

public static class ProgramsService
{
    // (Id, Nombre, Categoria, Descripcion, WingetId, DownloadUrl, InstallArgs, Homepage, SizeMb)
    private static readonly (string, string, string, string, string?, string?, string?, string?, long)[] Catalog =
    {
        // ═══════════════════════ NAVEGADORES WEB ═════════════════════════════
        ("chrome", "Google Chrome", "Navegadores Web",
         "El navegador más usado del mundo. Rápido, seguro y con la mayor compatibilidad con sitios web.",
         "Google.Chrome", null, null, "https://www.google.com/chrome/", 150),
        ("firefox", "Mozilla Firefox", "Navegadores Web",
         "Navegador de código abierto enfocado en la privacidad y la personalización.",
         "Mozilla.Firefox", null, null, "https://www.mozilla.org/firefox/", 60),
        ("edge", "Microsoft Edge", "Navegadores Web",
         "El navegador integrado de Windows, actualizado a su versión optimizada.",
         "Microsoft.Edge", null, null, "https://www.microsoft.com/edge", 200),
        ("brave", "Brave Browser", "Navegadores Web",
         "Bloquea rastreadores y anuncios por defecto, sin extensiones extra.",
         "Brave.Brave", null, null, "https://brave.com/", 120),
        ("opera", "Opera", "Navegadores Web",
         "Navegador con VPN integrada y bloqueador de anuncios incluido.",
         "Opera.Opera", null, null, "https://www.opera.com/", 90),
        ("opera-gx", "Opera GX", "Navegadores Web",
         "La versión gamer de Opera con limitador de CPU/RAM y estética gaming.",
         "Opera.OperaGX", null, null, "https://www.opera.com/gx", 100),

        // ═══════════════════════ UTILIDADES ══════════════════════════════════
        ("powertoys", "Microsoft PowerToys", "Utilidades",
         "Suite oficial de Microsoft para superusuarios: FancyZones, PowerRename, búsqueda global y más.",
         "Microsoft.PowerToys", null, null, "https://learn.microsoft.com/windows/powertoys/", 100),
        ("wiztree", "WizTree", "Utilidades",
         "Analizador de espacio en disco ultrarrápido: muestra qué ocupa tu disco en segundos.",
         "AntibodySoftware.WizTree", null, null, "https://diskanalyzer.com/", 20),
        ("windirstat", "WinDirStat", "Utilidades",
         "Analizador de espacio en disco clásico con mapas visuales de uso.",
         "WinDirStat.WinDirStat", null, null, "https://windirstat.net/", 10),
        ("7zip", "7-Zip", "Utilidades",
         "Compresor y descompresor de archivos ligero, gratuito y de código abierto.",
         "7zip.7zip", null, null, "https://www.7-zip.org/", 2),
        ("winrar", "WinRAR", "Utilidades",
         "Compresor clásico con soporte total de RAR y ZIP, el estándar de facto para archivos comprimidos.",
         "RARLab.WinRAR", null, null, "https://www.win-rar.com/", 4),
        ("ccleaner", "CCleaner", "Utilidades",
         "Clásico limpiador de archivos temporales y registro de Windows.",
         "Piriform.CCleaner", null, null, "https://www.ccleaner.com/", 30),
        ("everything", "Everything", "Utilidades",
         "Buscador instantáneo de archivos para Windows por nombre de archivo.",
         "voidtools.Everything", null, null, "https://www.voidtools.com/", 3),

        // ═══════════════════════ SEGURIDAD Y PRIVACIDAD ═════════════════════
        ("malwarebytes", "Malwarebytes", "Seguridad",
         "Escáner y eliminador de malware líder, ideal como complemento de Windows Defender.",
         "Malwarebytes.Malwarebytes", null, null, "https://www.malwarebytes.com/", 120),
        ("bitwarden", "Bitwarden", "Seguridad",
         "Gestor de contraseñas de código abierto y gratuito, con sincronización en la nube.",
         "Bitwarden.Bitwarden", null, null, "https://bitwarden.com/", 100),
        ("avast", "Avast Free Antivirus", "Seguridad",
         "Protección antivirus residente gratuita. No está en winget: se abre su web oficial para descargar.",
         null, null, null, "https://www.avast.com/free-antivirus-download", 300),
        ("spybot", "Spybot Search & Destroy", "Seguridad",
         "Herramienta clásica de remoción de spyware y rastreadores. No está en winget: se abre su web oficial.",
         null, null, null, "https://www.safer-networking.org/free-download/", 150),
        ("nordvpn", "NordVPN", "Seguridad",
         "Cliente oficial de la VPN más popular, con miles de servidores en todo el mundo.",
         "NordSecurity.NordVPN", null, null, "https://nordvpn.com/", 100),
        ("protonvpn", "ProtonVPN", "Seguridad",
         "VPN gratuita y de código abierto desarrollada por los creadores de Proton Mail.",
         "Proton.ProtonVPN", null, null, "https://protonvpn.com/", 80),

        // ═══════════════════════ MULTIMEDIA Y COMUNICACIÓN ══════════════════
        ("vlc", "VLC Media Player", "Multimedia",
         "El reproductor de video definitivo: reproduce prácticamente cualquier formato sin códecs extra.",
         "VideoLAN.VLC", null, null, "https://www.videolan.org/vlc/", 45),
        ("spotify", "Spotify", "Multimedia",
         "Streaming de música con millones de canciones, podcasts y playlists.",
         "Spotify.Spotify", null, null, "https://www.spotify.com/", 100),
        ("discord", "Discord", "Multimedia",
         "Chat de voz y texto muy enfocado en comunidades y gaming.",
         "Discord.Discord", null, null, "https://discord.com/", 100),
        ("zoom", "Zoom", "Multimedia",
         "Videoconferencias y reuniones en línea, el estándar del trabajo remoto.",
         "Zoom.Zoom", null, null, "https://zoom.us/", 80),
        ("obs", "OBS Studio", "Multimedia",
         "Grabación de pantalla y streaming profesional, gratuito y de código abierto.",
         "OBSProject.OBSStudio", null, null, "https://obsproject.com/", 130),

        // ═══════════════════════ DESARROLLADORES Y OFIMÁTICA ════════════════
        ("vscode", "Visual Studio Code", "Desarrolladores",
         "El editor de código más popular del mundo, con extensiones para todo.",
         "Microsoft.VisualStudioCode", null, null, "https://code.visualstudio.com/", 100),
        ("libreoffice", "LibreOffice", "Desarrolladores",
         "Suite ofimática gratuita y de código abierto, alternativa a Microsoft Office.",
         "TheDocumentFoundation.LibreOffice", null, null, "https://www.libreoffice.org/", 350),
        ("notepadpp", "Notepad++", "Desarrolladores",
         "Editor de texto avanzado para scripts, logs y código ligero.",
         "Notepad++.Notepad++", null, null, "https://notepad-plus-plus.org/", 5),
        ("python", "Python 3.12", "Desarrolladores",
         "El lenguaje de programación base, con su intérprete oficial.",
         "Python.Python.3.12", null, null, "https://www.python.org/", 30),
        ("git", "Git", "Desarrolladores",
         "Sistema de control de versiones estándar de la industria.",
         "Git.Git", null, null, "https://git-scm.com/", 60),
    };

    public static IReadOnlyList<ProgramItem> GetPrograms()
    {
        var list = new List<ProgramItem>();
        foreach (var (id, name, cat, desc, winget, url, args, site, mb) in Catalog)
        {
            list.Add(new ProgramItem
            {
                Id = id, Name = name, Category = cat, Description = desc,
                WingetId = winget, DownloadUrl = url, InstallArgs = args,
                Homepage = site, SizeMb = mb
            });
        }
        return list;
    }

    private static bool? _wingetAvailable;

    /// <summary>Verifica si winget está disponible. El resultado se cachea: no cambia durante la sesión.</summary>
    public static async Task<bool> IsWingetAvailableAsync()
    {
        if (_wingetAvailable.HasValue) return _wingetAvailable.Value;
        var (code, _, _) = await ProcessRunner.RunAsync("winget", "--version", 15_000);
        _wingetAvailable = code == 0;
        return _wingetAvailable.Value;
    }

    /// <summary>Instala un programa: vía winget o descarga directa. Devuelve (ok, mensaje).</summary>
    public static async Task<(bool Ok, string Message)> InstallAsync(ProgramItem p)
    {
        // 1) Vía winget: repositorio oficial de cada publisher
        if (!string.IsNullOrEmpty(p.WingetId))
        {
            var (wCode, _, _) = await ProcessRunner.RunAsync("winget", "--version", 15_000);
            if (wCode == 0)
            {
                // Intento silencioso
                var (code, _, err) = await ProcessRunner.RunAsync("winget",
                    $"install --exact --id {p.WingetId} --accept-source-agreements --accept-package-agreements --silent --disable-interactivity",
                    900_000);

                if (code == 0) return (true, $"{p.Name} instalado correctamente.");
                if (IsAlreadyInstalled(code)) return (true, $"{p.Name} ya estaba instalado.");

                // Reintento sin --silent: algunos instaladores fallan en modo silencioso
                var (code2, _, err2) = await ProcessRunner.RunAsync("winget",
                    $"install --exact --id {p.WingetId} --accept-source-agreements --accept-package-agreements --disable-interactivity",
                    900_000);

                if (code2 == 0) return (true, $"{p.Name} instalado correctamente.");
                if (IsAlreadyInstalled(code2)) return (true, $"{p.Name} ya estaba instalado.");

                return (false, $"Winget no pudo instalar {p.Name} (código {code}). {err} {err2}".Trim());
            }

            // Winget no disponible → fallback a descarga directa si la hay
            if (string.IsNullOrEmpty(p.DownloadUrl))
                return (false, "Winget no está disponible en este equipo. Instalá la app 'App Installer' desde Microsoft Store.");
        }

        // 2) Descarga directa desde la URL oficial
        if (!string.IsNullOrEmpty(p.DownloadUrl))
        {
            string installer = Path.Combine(Path.GetTempPath(), $"wachin-{p.Id}-installer.exe");
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var bytes = await client.GetByteArrayAsync(p.DownloadUrl);
                await File.WriteAllBytesAsync(installer, bytes);
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo descargar el instalador de {p.Name}: {ex.Message}");
            }

            var (code, _, err) = await ProcessRunner.RunAsync(installer, p.InstallArgs ?? "", 900_000);
            try { File.Delete(installer); } catch { }

            if (code == 0) return (true, $"{p.Name} instalado correctamente.");
            return (false, $"El instalador de {p.Name} falló (código {code}). {err}".Trim());
        }

        // 3) Sin script automático: avisar que hay que usar la web oficial
        return (false, $"No hay instalador automático para {p.Name}. Abrí su página oficial para descargarlo.");
    }

    /// <summary>0x8A150019 = "el paquete ya está instalado" (winget).</summary>
    private static bool IsAlreadyInstalled(int exitCode)
        => (uint)exitCode == 0x8A150019;
}
