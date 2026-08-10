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
