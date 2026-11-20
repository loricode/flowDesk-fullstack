using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FlowDesk.Desktop.ViewModels;
using FlowDesk.Shared.Contracts;

namespace FlowDesk.Desktop.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value is not null && value is not "";
        return hasValue ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ItemStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ItemState.Validated => "SuccessBrush",
            ItemState.Invalid or ItemState.Failed => "DangerBrush",
            ItemState.Uploading or ItemState.Processing => "SapoteBrush",
            _ => "InfoBrush",
        };
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ItemStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ItemState.Validated => "CheckCircle",
        ItemState.Invalid => "AlertCircle",
        ItemState.Failed => "CloudAlert",
        ItemState.Uploading => "CloudUpload",
        ItemState.Processing => "ProgressClock",
        _ => "CircleOutline",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Traduce estados y prioridades del API a etiquetas en español.</summary>
public sealed class LabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ExpedientStatuses.Received => "Recibido",
        ExpedientStatuses.InValidation => "En validación",
        ExpedientStatuses.PendingApproval => "Pendiente de aprobación",
        ExpedientStatuses.CorrectionRequested => "Corrección solicitada",
        ExpedientStatuses.Approved => "Aprobado",
        ExpedientStatuses.Rejected => "Rechazado",
        ExpedientPriorities.Low => "Baja",
        ExpedientPriorities.Normal => "Normal",
        ExpedientPriorities.High => "Alta",
        ExpedientPriorities.Urgent => "Urgente",
        _ => value?.ToString() ?? "",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PriorityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ExpedientPriorities.Urgent => Application.Current.FindResource("DangerBrush"),
        ExpedientPriorities.High => Application.Current.FindResource("SapoteBrush"),
        _ => Application.Current.FindResource("InkMutedBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Color con transparencia para fondos de chips a partir de un pincel sólido.</summary>
public sealed class TintConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SolidColorBrush brush) return Brushes.Transparent;
        var c = brush.Color;
        return new SolidColorBrush(Color.FromArgb(0x22, c.R, c.G, c.B));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
