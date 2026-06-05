using System.Windows.Data;

namespace BmsAtelierKyokufu.BmsPartTuner.UI.Converters
{
    /// <summary>
    /// 現在のファイルのフルパスと、現在再生中のファイル名を比較し、一致するかどうかを判定します。
    /// </summary>
    public class FilePlayingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values?.Length >= 2 && values[0] is string currentPath && values[1] is string playingFileName)
            {
                if (string.IsNullOrEmpty(currentPath) || string.IsNullOrEmpty(playingFileName))
                    return false;

                try
                {
                    var currentFileName = Path.GetFileName(currentPath);
                    return string.Equals(currentFileName, playingFileName, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
