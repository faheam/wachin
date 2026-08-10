using Windows.Management.Deployment;
using Wachin.Models;

namespace Wachin.Services;

public static class BloatwareService
{
    private static readonly (string Prefix, string Name, string Desc, RiskLevel Risk)[] Catalog =
    {
        ("Microsoft.BingNews", "Noticias de Bing", "App de noticias preinstalada.", RiskLevel.Low),
        ("Microsoft.BingWeather", "Clima de Bing", "App de clima preinstalada.", RiskLevel.Low),
        ("Microsoft.BingFinance", "Finanzas de Bing", "App de finanzas preinstalada.", RiskLevel.Low),
        ("Microsoft.BingSports", "Deportes de Bing", "App de deportes preinstalada.", RiskLevel.Low),
        ("Microsoft.MicrosoftSolitaireCollection", "Solitario de Microsoft", "Colección de juegos de cartas.", RiskLevel.Low),
        ("Microsoft.MicrosoftOfficeHub", "Office (acceso rápido)", "Portal que anuncia Microsoft Office.", RiskLevel.Low),
        ("Microsoft.Office.OneNote", "OneNote", "Bloc de notas de Microsoft.", RiskLevel.Low),
        ("Microsoft.People", "Personas", "App de contactos integrada.", RiskLevel.Low),
        ("Microsoft.WindowsFeedbackHub", "Centro de comentarios", "Envía comentarios a Microsoft.", RiskLevel.Low),
        ("microsoft.windowscommunicationsapps", "Correo y Calendario (legacy)", "App antigua de correo de Microsoft.", RiskLevel.Low),
        ("Microsoft.GetHelp", "Obtener ayuda", "App de soporte de Microsoft.", RiskLevel.Low),
        ("Microsoft.MicrosoftTips", "Sugerencias de Windows", "Muestra consejos y tutoriales.", RiskLevel.Low),
        ("Microsoft.ZuneMusic", "Música Groove", "Reproductor de música de Microsoft.", RiskLevel.Low),
        ("Microsoft.ZuneVideo", "Cine y TV", "Reproductor de video de Microsoft.", RiskLevel.Low),
        ("Microsoft.XboxApp", "Xbox (app)", "App principal de Xbox.", RiskLevel.Low),
        ("Microsoft.YourPhone", "Phone Link", "Conecta tu celular con el PC.", RiskLevel.Low),
        ("Microsoft.Todos", "Tareas de Microsoft", "Gestor de tareas.", RiskLevel.Low),
        ("Microsoft.Teams", "Microsoft Teams (personal)", "App de chat y videollamadas.", RiskLevel.Low),
        ("Microsoft.Copilot", "Copilot", "Asistente IA de Microsoft (Windows 11).", RiskLevel.Low),
        ("Microsoft.Clipchamp", "Clipchamp", "Editor de video sencillo.", RiskLevel.Low),
        ("Microsoft.MixedReality.Portal", "Portal de realidad mixta", "App para realidad mixta.", RiskLevel.Low),
        ("Microsoft.PowerAutomateDesktop", "Power Automate", "Automatización de tareas.", RiskLevel.Low),
        ("Microsoft.SkypeApp", "Skype", "App de videollamadas.", RiskLevel.Low),
        ("Microsoft.549981C3F5F10", "Cortana", "Asistente de voz (Windows 10).", RiskLevel.Low),
        ("Microsoft.OutlookForWindows", "Outlook (nuevo)", "App nueva de correo de Outlook.", RiskLevel.Low),
        ("Microsoft.StickyNotes", "Notas rápidas", "App de notas adhesivas.", RiskLevel.Low),
        ("Microsoft.Getstarted", "Introducción a Windows", "App de bienvenida.", RiskLevel.Low),
        ("Microsoft.WindowsMaps", "Mapas", "App de mapas y navegación.", RiskLevel.Low),
        ("Microsoft.WindowsAlarms", "Reloj", "Alarma, temporizador y cronómetro.", RiskLevel.Low),
        ("Microsoft.AAD.BrokerPlugin", "Broker de cuentas", "Componente de autenticación Microsoft.", RiskLevel.Medium),
        ("Microsoft.Windows.PinningConfirmationDialog", "Confirmación de anclaje", "Diálogo de confirmación de pin.", RiskLevel.Low),
        ("Microsoft.Windows.OOBENetworkCaptivePortal", "Portal cautivo OOBE", "Configuración de red inicial.", RiskLevel.Medium),
        ("Microsoft.Windows.CloudExperienceHost", "Experiencia en la nube", "Configuración de cuenta Microsoft.", RiskLevel.Medium),
        ("Microsoft.Windows.ContentDeliveryManager", "Gestor de contenido", "Entrega de contenido dinámico.", RiskLevel.Medium),
        ("king.com.CandyCrushSaga", "Candy Crush Saga", "Juego preinstalado.", RiskLevel.Low),
        ("king.com.CandyCrushSodaSaga", "Candy Crush Soda", "Juego preinstalado.", RiskLevel.Low),
        ("Disney.37853FC22B2CE", "Disney+", "App de streaming de Disney.", RiskLevel.Low),
        ("SpotifyAB.SpotifyMusic", "Spotify", "App de música Spotify.", RiskLevel.Low),
        ("Facebook.317180KD0PN5", "Facebook", "App de Facebook.", RiskLevel.Low),
        ("Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Superposición de juegos de Xbox.", RiskLevel.Medium),
        // ═══════════════════════ EXTRAS ═════════════════════════════════════
        ("Microsoft.MicrosoftSolitaireCollection", "Solitario Premium", "Versión premium del solitario.", RiskLevel.Low),
        ("Microsoft.WindowsReadingList", "Lista de lectura", "Guarda artículos para leer después.", RiskLevel.Low),
        ("Microsoft.Print3D", "Print 3D", "Impresión 3D de Windows.", RiskLevel.Low),
        ("Microsoft.WindowsCamera", "Cámara", "App de cámara de Windows.", RiskLevel.Low),
        ("Microsoft.WindowsSoundRecorder", "Grabadora de sonido", "App para grabar audio.", RiskLevel.Low),
        ("Microsoft.WindowsFeedbackHub", "Feedback Hub", "Centro de comentarios de Windows.", RiskLevel.Low),
        ("Microsoft.Messaging", "Mensajes", "App de mensajes de Microsoft.", RiskLevel.Low),
        ("Microsoft.WindowsStore", "Microsoft Store", "Tienda de apps de Microsoft.", RiskLevel.Medium),
        ("Microsoft.Xbox.TCUI", "Xbox TCUI", "Componente de Xbox para UI.", RiskLevel.Medium),
        ("Microsoft.XboxGameCallableUI", "Xbox Callable UI", "Componente de Xbox.", RiskLevel.Medium),
        ("Microsoft.XboxIdentityProvider", "Xbox Identity Provider", "Proveedor de identidad de Xbox.", RiskLevel.Medium),
        ("Microsoft.XboxSpeechToTextOverlay", "Xbox Speech to Text", "Conversión de voz a texto de Xbox.", RiskLevel.Low),
        ("Microsoft.WindowsPhone", "Phone Companion", "Compañero de teléfono.", RiskLevel.Low),
        ("Microsoft.HEIFImageExtension", "HEIF Image Extension", "Soporte para imágenes HEIF.", RiskLevel.Low),
        ("Microsoft.WebpImageExtension", "WebP Image Extension", "Soporte para imágenes WebP.", RiskLevel.Low),
        ("Microsoft.RawImageExtension", "Raw Image Extension", "Soporte para imágenes RAW.", RiskLevel.Low),
        ("Microsoft.VP9VideoExtensions", "VP9 Video Extensions", "Soporte para video VP9.", RiskLevel.Low),
        ("Microsoft.WebMediaExtensions", "Web Media Extensions", "Soporte para formatos web.", RiskLevel.Low),
        ("Microsoft.549981C3F5F10", "Cortana", "Asistente de voz (Windows 10).", RiskLevel.Low),
        ("king.com.BubbleWitch3Saga", "Bubble Witch 3 Saga", "Juego preinstalado.", RiskLevel.Low),
        ("king.com.FarmHeroesSaga", "Farm Heroes Saga", "Juego preinstalado.", RiskLevel.Low),
        ("SpotifyAB.SpotifyMusic", "Spotify", "App de música Spotify.", RiskLevel.Low),
        ("BytedancePte.Ltd.TikTok", "TikTok", "App de TikTok.", RiskLevel.Low),
        ("AdobeSystemsIncorporated.AdobeCreativeCloudExpress", "Adobe Express", "Editor de Adobe.", RiskLevel.Low),
        ("AmazonVideo.PrimeVideo", "Amazon Prime Video", "App de streaming de Amazon.", RiskLevel.Low),
        ("Netflix.Netflix", "Netflix", "App de streaming de Netflix.", RiskLevel.Low),
        ("Microsoft_corporation.549981C3F5F10", "Cortana (nuevo)", "Asistente de voz.", RiskLevel.Low),
    };

    public static List<BloatApp> GetInstalled()
    {
        var result = new List<BloatApp>();
        try
        {
            var pm = new PackageManager();
            var packages = pm.FindPackagesForUser("").ToList();

            foreach (var (prefix, name, desc, risk) in Catalog)
            {
                var match = packages.FirstOrDefault(p =>
                    p.Id.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                result.Add(new BloatApp
                {
                    PackagePrefix = prefix,
                    Name = name,
                    Description = desc,
                    Risk = risk,
                    Installed = match != null,
                    FullName = match?.Id.FullName,
                    Publisher = match?.PublisherDisplayName
                });
            }
        }
        catch
        {
            // PackageManager not available
        }
        return result;
    }

    public static async Task<(bool Ok, string Message)> RemoveAsync(BloatApp app)
    {
        if (string.IsNullOrEmpty(app.FullName))
            return (false, "No se encontró la paquete de la aplicación.");

        try
        {
            var pm = new PackageManager();
            var result = await pm.RemovePackageAsync(app.FullName);

            if (result.ErrorText is { Length: > 0 } err)
                return (false, $"Error al eliminar: {err}");

            return (true, $"{app.Name} eliminada correctamente.");
        }
        catch (Exception ex)
        {
            // Fallback to PowerShell
            return await RemoveViaPowerShell(app);
        }
    }

    private static async Task<(bool Ok, string Message)> RemoveViaPowerShell(BloatApp app)
    {
        var name = app.PackagePrefix;
        var (code, stdout, stderr) = await ProcessRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"Get-AppxPackage -Name '*{name}*' | Remove-AppxPackage\"");

        if (code == 0)
            return (true, $"{app.Name} eliminada correctamente (vía PowerShell).");

        return (false, $"No se pudo eliminar: {stderr}");
    }

    public static void OpenStore(string familyName)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"ms-windows-store://pdp/?PFN={familyName}",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
