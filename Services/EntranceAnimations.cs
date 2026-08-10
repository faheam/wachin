using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Wachin.Services;

/// <summary>
/// Animaciones de entrada reutilizables: fade-up escalonado con spline custom
/// (KeySpline 0.16,1,0.3,1). Solo anima transform + opacity (GPU-safe).
/// </summary>
public static class EntranceAnimations
{
    /// <summary>Anima los hijos directos de un contenedor con aparicion escalonada.</summary>
    public static void FadeUpStaggered(FrameworkElement container, int delayStepMs = 45, double rise = 14, int durationMs = 260)
    {
        int count = VisualTreeHelper.GetChildrenCount(container);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(container, i) is FrameworkElement fe && fe.Visibility != Visibility.Collapsed)
                AnimateElement(fe, i * delayStepMs, rise, durationMs);
        }
    }

    /// <summary>Anima un elemento: fade + subida sutil con delay.</summary>
    public static void AnimateElement(UIElement element, int delayMs, double rise, int durationMs)
    {
        var translate = new TranslateTransform(0, rise);
        element.RenderTransform = translate;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.Opacity = 0;

        var fade = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        fade.KeyFrames.Add(new SplineDoubleKeyFrame(
            1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)),
            new KeySpline(0.16, 1, 0.3, 1)));
        element.BeginAnimation(UIElement.OpacityProperty, fade);

        var riseAnim = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        riseAnim.KeyFrames.Add(new SplineDoubleKeyFrame(
            0,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)),
            new KeySpline(0.16, 1, 0.3, 1)));
        translate.BeginAnimation(TranslateTransform.YProperty, riseAnim);
    }
}
