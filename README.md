# Wachin · Optimizador de PC

**Wachin** es un optimizador de PC gratuito y de código abierto para **Windows 10 y 11**, pensado para que cualquier persona pueda acelerar su equipo, mejorar su privacidad y quitar lo que no usa — sin tocar el registro a mano ni buscar entre miles de opciones.

Todo está explicado en **lenguaje simple**, cada ajuste muestra su **nivel de riesgo** y se puede **aplicar y deshacer** cuando quieras.

> ⚠️ **Importante:** Wachin pide permisos de administrador (UAC) para poder aplicar los ajustes. Antes de cambiar cualquier cosa, el programa te recuerda **crear un punto de restauración**.

---

## ✨ Qué hace

### Categorías de ajustes
| Categoría | Ejemplos |
|---|---|
| **Rendimiento** | Animaciones, apps en segundo plano, prioridad de programas, inicio más rápido |
| **Privacidad** | Telemetría, ID de publicidad, Cortana, búsqueda web, ubicación, historial de actividad |
| **GPU y gráficos** | MPO (parpadeos), aceleración de GPU por hardware, optimizaciones de pantalla completa |
| **Energía** | Plan de alto rendimiento, inicio rápido, suspensión USB, hibernación |
| **Escritorio** | Archivos ocultos, extensiones, icono "Este equipo", menú clásico (Win11) |
| **Barra de tareas** | Iconos a la izquierda, segundos en el reloj, botones de widgets/vista de tareas |
| **Explorador** | Abrir en "Este equipo", carpetas en proceso propio, quitar anuncios |
| **Juegos** | Modo juego, Xbox Game Bar, grabación en segundo plano, latencia de red |
| **Sistema** | Apagado más rápido, cierre automático de apps, P2P de Windows Update |

### Herramientas
- **Información del sistema**: procesador, memoria, tarjeta gráfica, discos, batería y más.
- **Inicio (startup)**: deshabilita o elimina los programas que se abren con Windows.
- **Tareas programadas**: desactiva tareas de Windows o de apps que no uses.
- **Limpieza**: archivos temporales, caché de Windows Update, papelera, cachés del navegador y más.
- **Bloatware**: quita las apps preinstaladas que casi nadie usa.
- **Servicios**: desactiva servicios de Windows (Superfetch, telemetría, Xbox…) con un clic.

---

## 🚀 Cómo compilarlo

Necesitas el **.NET 8 SDK** (o superior): <https://dotnet.microsoft.com/download/dotnet/8.0>

```bash
# Compilar en modo desarrollo
dotnet build

# Publicar el EXE portátil (un solo archivo, sin instalar nada)
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish\win-x64
```

También puedes hacer doble clic en **`publish.bat`**.

El resultado es un único `Wachin.exe` (~90 MB) que funciona sin instalar nada y se puede llevar en un USB. El estado de los ajustes se guarda junto al EXE si la carpeta es escribible; si no, en `%LocalAppData%\Wachin`.

> 💡 Ejecuta `Wachin.exe` como administrador (el propio programa te lo pedirá con UAC).

---

## 🛡️ Seguridad primero

- **Crea siempre un punto de restauración** antes de aplicar cambios (botón en la barra superior o en la pantalla de inicio). Si algo sale mal, puedes volver atrás desde *Configuración del sistema → Protección del sistema*.
- Cada ajuste muestra su **riesgo**: 🟢 bajo · 🟡 medio · 🔴 alto.
- Todos los ajustes se pueden **deshacer** desde la propia categoría.
- Wachin no toca el registro "a ciegas": guarda el valor original de cada ajuste para poder restaurarlo.
- Si una carpeta está en uso (por ejemplo, un archivo abierto), la limpieza la **salta** sin romper nada.
- Si tu antivirus marca a Wachin como **falso positivo** (pasa porque modifica el sistema), podés agregarlo a las exclusiones de Microsoft Defender con un clic desde *Configuración → Antivirus y falsos positivos*. Los antivirus de terceros se excluyen manualmente desde su propia interfaz.

---

## ✍️ Firma de código (menos falsos positivos)

Los antivirus y SmartScreen desconfían de los EXE **sin firma** o recién publicados. Firmar el `Wachin.exe` con un certificado de código **Authenticode** no elimina los falsos positivos por completo, pero reduce muchísimo los avisos:

- Windows muestra el **nombre del editor verificado** (desaparece el "Unknown Publisher") en UAC y en las propiedades del archivo.
- SmartScreen y Defender usan la firma como señal de confianza: un EXE firmado con buena reputación deja de dar avisos con el tiempo.

> 💡 **Importante:** la firma **no** es un pase automático. SmartScreen construye la reputación con las descargas y ejecuciones exitosas; un EXE recién firmado puede mostrar el aviso azul las primeras veces hasta que acumula reputación.

### Tipos de certificado

| Tipo | Costo aprox. | ¿Sirve? |
|---|---|---|
| **Auto-firmado** (`New-SelfSignedCertificate`) | Gratis | ❌ Solo pruebas locales. No reduce falsos positivos. |
| **OV — Organization Validation** | ~US$100–300/año | ✅ Recomendado. Verifica la identidad de la organización. |
| **EV — Extended Validation** | ~US$300–700/año | ✅ Máxima confianza. Ya no saltea SmartScreen, pero mejora la reputación y cumple políticas empresariales. |

### Opciones baratas o gratis (open source)
- **SignPath Foundation** — firma gratuita para proyectos open source que cumplan sus requisitos, vía un pipeline de build seguro.
- **Certum (plan Open Source)** — certificado para desarrolladores individuales de OSS con clave en la nube (sin token USB). El editor aparece como "Open Source Developer, [tu nombre]" y no puede usarse en software comercial.
- **Azure Trusted Signing** — ~US$9.99/mes. Firma sin hardware, integrado con GitHub Actions y Azure DevOps.

### Cómo firmar el EXE (signtool)

Con el certificado instalado o como archivo `.pfx`, desde un **Developer Command Prompt**:

```bat
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a /f cert.pfx /p TU_CLAVE publish\win-x64\Wachin.exe
```

- `/fd SHA256` y `/td SHA256`: firman con SHA-256, que es lo que exige Windows moderno.
- `/tr` + `/td`: timestamp **RFC 3161**. Firmá siempre con timestamp para que la firma siga válida aunque el certificado venza.
- Verificá el resultado con: `signtool verify /pa /v publish\win-x64\Wachin.exe`

### Buenas prácticas
- Firmá **siempre el EXE publicado final** (el de un solo archivo), no los binarios de desarrollo.
- Usá **el mismo certificado** en todas las versiones: la reputación de SmartScreen se acumula sobre la firma.
- Mientras tanto, los usuarios pueden usar *Configuración → Antivirus y falsos positivos* para excluir Wachin de Microsoft Defender con un clic.

---

## 📁 Estructura del proyecto

```
Wachin/
├── Wachin.csproj          # Proyecto WPF (.NET 8, Windows 10/11)
├── app.manifest           # Permisos de administrador y DPI
├── Theme/                 # Colores claro/oscuro y estilos
├── Models/                # Modelos de ajustes y entidades
├── Services/              # Motor de ajustes, limpieza, sistema, etc.
├── ViewModels/            # Lógica de la interfaz
├── Views/                 # Pantallas (categorías y herramientas)
└── Controls/              # Diálogos y componentes
```

## ⚖️ Licencia

MIT — úsalo, modifícalo y compártelo libremente. Consulta [LICENSE](LICENSE).

---

*Hecho con ❤️ para que tu PC se sienta nuevo otra vez.*
