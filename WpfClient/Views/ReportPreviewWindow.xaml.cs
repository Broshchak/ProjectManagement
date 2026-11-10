using System.Windows;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient.Views;

public partial class ReportPreviewWindow : Window
{
    private readonly byte[] content;
    private PdfPageReader? reader;
    private int pageIndex;
    private bool busy;
    private bool closed;

    public ReportPreviewWindow(byte[] content)
    {
        this.content = content;
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) =>
        {
            closed = true;
            if (!busy) Release();
        };
    }

    public LocalizationManager T => LocalizationManager.Instance;

    private Task LoadAsync() => RenderAsync(0);

    private async Task RenderAsync(int index)
    {
        if (closed || busy) return;
        busy = true;
        UpdateNavigation();
        try
        {
            reader ??= await PdfPageReader.OpenAsync(content);
            if (closed) return;
            var bitmap = await reader.ReadAsync(index, 1600);
            if (closed) return;

            PageImage.Source = bitmap;
            PageScroll.ScrollToTop();
            pageIndex = index;
            PageLabel.Text = T.Format("ReportPreviewPage", pageIndex + 1, reader.PageCount);
            PreviewMessage.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            if (!closed)
            {
                PreviewMessage.Text = T["ReportPreviewFailed"];
                PreviewMessage.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            busy = false;
            if (closed) Release();
            else UpdateNavigation();
        }
    }

    private void UpdateNavigation()
    {
        PreviousPage.IsEnabled = !busy && pageIndex > 0;
        NextPage.IsEnabled = !busy && reader is not null && pageIndex + 1 < reader.PageCount;
    }

    private async void PreviousClicked(object sender, RoutedEventArgs e) => await RenderAsync(pageIndex - 1);
    private async void NextClicked(object sender, RoutedEventArgs e) => await RenderAsync(pageIndex + 1);

    private void Release()
    {
        reader?.Dispose();
        reader = null;
    }
}
