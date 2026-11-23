using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;

namespace Filmoteka.Controls;

/// <summary>
/// Оценка със звезди от 1 до 10. Кликване върху звезда задава оценката,
/// повторно кликване върху същата звезда я изчиства.
/// </summary>
public class StarRating : UserControl
{
    public const int MaxStars = 10;

    private static readonly Geometry StarGeometry = CreateStarGeometry();
    private static readonly Brush FilledBrush = Frozen(Color.FromRgb(0xF5, 0xC5, 0x18));
    private static readonly Brush HoverBrush = Frozen(Color.FromRgb(0xFF, 0xDD, 0x66));
    private static readonly Brush EmptyBrush = Frozen(Color.FromRgb(0x3A, 0x3F, 0x4C));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(StarRating),
        new FrameworkPropertyMetadata(
            0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((StarRating)d).Refresh(),
            (_, baseValue) => Math.Clamp((int)baseValue, 0, MaxStars)));

    public static readonly DependencyProperty StarSizeProperty = DependencyProperty.Register(
        nameof(StarSize), typeof(double), typeof(StarRating),
        new PropertyMetadata(20.0, (d, _) => ((StarRating)d).UpdateSizes()));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(StarRating),
        new PropertyMetadata(false, (d, _) => ((StarRating)d).UpdateInteractivity()));

    /// <summary>Вдига се само когато потребителят кликне звезда.</summary>
    public event EventHandler<int>? RatingChanged;

    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
    private readonly List<WpfPath> _stars = [];
    private int _hover;

    public StarRating()
    {
        for (int i = 1; i <= MaxStars; i++)
        {
            int index = i;
            var star = new WpfPath
            {
                Data = StarGeometry,
                Stretch = Stretch.Uniform,
                Fill = EmptyBrush
            };

            star.MouseEnter += (_, _) =>
            {
                if (IsReadOnly) return;
                _hover = index;
                Refresh();
            };

            star.MouseLeftButtonDown += (_, e) =>
            {
                if (IsReadOnly) return;
                int newValue = Value == index ? 0 : index;
                SetCurrentValue(ValueProperty, newValue); // запазва евентуален Binding
                RatingChanged?.Invoke(this, newValue);
                e.Handled = true;
            };

            _stars.Add(star);
            _panel.Children.Add(star);
        }

        _panel.MouseLeave += (_, _) =>
        {
            _hover = 0;
            Refresh();
        };

        Content = _panel;
        Focusable = false;
        UpdateSizes();
        UpdateInteractivity();
        Refresh();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private void UpdateSizes()
    {
        double gap = Math.Max(1.0, StarSize / 7.0);
        foreach (var star in _stars)
        {
            star.Width = StarSize;
            star.Height = StarSize;
            star.Margin = new Thickness(0, 0, gap, 0);
        }
    }

    private void UpdateInteractivity()
    {
        Cursor = IsReadOnly ? null : Cursors.Hand;
        for (int i = 0; i < _stars.Count; i++)
            _stars[i].ToolTip = IsReadOnly ? null : $"{i + 1} от {MaxStars}";

        _hover = 0;
        Refresh();
    }

    private void Refresh()
    {
        int shown = _hover > 0 ? _hover : Value;
        var on = _hover > 0 ? HoverBrush : FilledBrush;
        for (int i = 0; i < _stars.Count; i++)
            _stars[i].Fill = i < shown ? on : EmptyBrush;
    }

    private static Geometry CreateStarGeometry()
    {
        var geometry = Geometry.Parse("M12,1.6 L15,8.3 L22.3,9.1 L16.9,14 L18.4,21.2 L12,17.5 L5.6,21.2 L7.1,14 L1.7,9.1 L9,8.3 Z");
        geometry.Freeze();
        return geometry;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
