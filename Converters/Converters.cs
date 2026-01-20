using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SkillManager.Converters;

/// <summary>
/// 布尔值取反转换器
/// </summary>
public class BoolToInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}

/// <summary>
/// 布尔值转可见性转换器
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
            var isVisible = invert ? !boolValue : boolValue;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isVisible = value is Visibility visibility && visibility == Visibility.Visible;
        var invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !isVisible : isVisible;
    }
}

/// <summary>
/// 数量为0时显示，否则隐藏（用于空状态提示）
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 数量转布尔值（大于0为true）
/// </summary>
public class CountToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 布尔值转展开/收起箭头符号
/// </summary>
public class BoolToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded 
                ? Wpf.Ui.Controls.SymbolRegular.ChevronUp24 
                : Wpf.Ui.Controls.SymbolRegular.ChevronDown24;
        }
        return Wpf.Ui.Controls.SymbolRegular.ChevronDown24;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 字符串非空时显示，空时隐藏
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 宽度转列数（用于卡片布局自适应）
/// 根据容器宽度自动计算最佳列数，确保卡片均匀分布
/// </summary>
public class WidthToColumnsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double width && width > 0)
        {
            // 默认最小卡片宽度
            var minCardWidth = 280d;
            if (parameter != null && double.TryParse(parameter.ToString(), out var parsed))
            {
                minCardWidth = parsed;
            }

            // 卡片间距（每侧8px，共16px）
            var spacing = 16d;
            
            // 计算可容纳的列数
            // 公式：(可用宽度 + 间距) / (卡片最小宽度 + 间距)
            var columns = (int)Math.Floor((width + spacing) / (minCardWidth + spacing));
            
            // 限制最小1列，最大6列（防止卡片过小）
            return Math.Clamp(columns, 1, 6);
        }

        return 3; // 默认3列
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
