using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TweaksLabPanel.Helpers;

public enum BackgroundMotionProfile
{
    Full,
    Preview,
}

public static class BackgroundMotionHelper
{
    private static readonly IEasingFunction Ease = new SineEase { EasingMode = EasingMode.EaseInOut };

    public static void Attach(Image image, BackgroundMotionProfile profile = BackgroundMotionProfile.Full)
    {
        Detach(image);

        image.Stretch = Stretch.UniformToFill;
        image.RenderTransformOrigin = new Point(0.5, 0.5);

        var (minScale, maxScale, panX, panY, scaleSeconds, panSeconds) = profile switch
        {
            BackgroundMotionProfile.Preview => (1.12, 1.24, 12.0, 8.0, 16, 20),
            _ => (1.10, 1.22, 32.0, 20.0, 20, 24),
        };

        var scale = new ScaleTransform((minScale + maxScale) / 2, (minScale + maxScale) / 2);
        var translate = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        image.RenderTransform = group;

        var repeat = RepeatBehavior.Forever;
        var reverse = true;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(minScale, maxScale, TimeSpan.FromSeconds(scaleSeconds))
            {
                AutoReverse = reverse,
                RepeatBehavior = repeat,
                EasingFunction = Ease,
            });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(minScale, maxScale, TimeSpan.FromSeconds(scaleSeconds))
            {
                AutoReverse = reverse,
                RepeatBehavior = repeat,
                EasingFunction = Ease,
            });

        translate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-panX, panX, TimeSpan.FromSeconds(panSeconds))
            {
                AutoReverse = reverse,
                RepeatBehavior = repeat,
                EasingFunction = Ease,
            });
        translate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-panY, panY, TimeSpan.FromSeconds(panSeconds + 4))
            {
                AutoReverse = reverse,
                RepeatBehavior = repeat,
                EasingFunction = Ease,
            });
    }

    public static void Detach(Image image)
    {
        if (image.RenderTransform is not TransformGroup group)
            return;

        foreach (var child in group.Children)
        {
            switch (child)
            {
                case ScaleTransform scale:
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    break;
                case TranslateTransform translate:
                    translate.BeginAnimation(TranslateTransform.XProperty, null);
                    translate.BeginAnimation(TranslateTransform.YProperty, null);
                    break;
            }
        }
    }
}
