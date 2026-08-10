using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Wachin.Models;

namespace Wachin.Services;

public static class ResourceBrush
{
    // Busca un recurso pincel de forma segura: si el recurso no existe O falla al
    // instanciarse (p. ej. un color invalido en el diccionario), devuelve el fallback
    // en lugar de propagar la excepcion y crashear la app.
    public static Brush Safe(string key, Brush fallback)
    {
        try
        {
            return Application.Current.TryFindResource(key) as Brush ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    // Lo mismo pero para estilos (los botones bindean Style a esta propiedad).
    public static Style? Style(string key)
    {
        try
        {
            return Application.Current.TryFindResource(key) as Style;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class RiskToBrushConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RiskLevel risk)
        {
            string key = risk switch
            {
                RiskLevel.Low => "RiskLow",
                RiskLevel.Medium => "RiskMedium",
                RiskLevel.High => "RiskHigh",
                _ => "TextMuted"
            };
            return ResourceBrush.Safe(key, Brushes.Gray);
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class RiskToTextConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RiskLevel risk)
        {
            return risk switch
            {
                RiskLevel.Low => "Riesgo bajo",
                RiskLevel.Medium => "Riesgo medio",
                RiskLevel.High => "Riesgo alto",
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class RiskToSoftBrushConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RiskLevel risk)
        {
            string key = risk switch
            {
                RiskLevel.Low => "RiskLowSoft",
                RiskLevel.Medium => "RiskMediumSoft",
                RiskLevel.High => "RiskHighSoft",
                _ => "BgChip"
            };
            return ResourceBrush.Safe(key, Brushes.Transparent);
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class BoolToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class InverseBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class CategoryToNameConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TweakCategory cat)
        {
            return cat switch
            {
                TweakCategory.Performance => "Rendimiento",
                TweakCategory.Privacy => "Privacidad",
                TweakCategory.Gpu => "GPU y gráficos",
                TweakCategory.Power => "Energía",
                TweakCategory.Services => "Servicios",
                TweakCategory.Bloatware => "Bloatware",
                TweakCategory.Desktop => "Escritorio",
                TweakCategory.Taskbar => "Barra de tareas",
                TweakCategory.Explorer => "Explorador de archivos",
                TweakCategory.Gaming => "Juegos",
                TweakCategory.System => "Configuración del sistema",
                _ => value.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class CategoryToDescriptionConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TweakCategory cat)
        {
            return cat switch
            {
                TweakCategory.Performance => "Haz que tu PC responda más rápido: animaciones, inicio, fondo…",
                TweakCategory.Privacy => "Reduce los datos que Windows y las apps recopilan sobre ti.",
                TweakCategory.Gpu => "Ajustes gráficos: grabación, latencia y estabilidad de la tarjeta.",
                TweakCategory.Power => "Planes de energía, suspensión USB e hibernación.",
                TweakCategory.Services => "Activa o desactiva servicios de Windows con un clic.",
                TweakCategory.Bloatware => "Apps preinstaladas que casi nadie usa. Quítalas y gana espacio.",
                TweakCategory.Desktop => "Iconos, extensiones y apariencia del escritorio.",
                TweakCategory.Taskbar => "Botones, reloj y widgets de la barra de tareas.",
                TweakCategory.Explorer => "Cómo se ven y se comportan las ventanas de archivos.",
                TweakCategory.Gaming => "Modo juego, Game Bar y ajustes para jugar mejor.",
                TweakCategory.System => "Apagado, cierres y opciones generales de Windows.",
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

public sealed class StatusToColorConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (s.Contains("ejecución") || s == "Running") return ResourceBrush.Safe("Success", Brushes.Green);
            if (s == "Detenido" || s == "Stopped") return ResourceBrush.Safe("TextMuted", Brushes.Gray);
        }
        return ResourceBrush.Safe("TextSecondary", Brushes.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
