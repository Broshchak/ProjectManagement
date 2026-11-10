using System.IO;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace WpfClient.Services;

internal sealed class PdfPageReader(IRandomAccessStream source, PdfDocument document) : IDisposable
{
    public int PageCount => checked((int)document.PageCount);

    public static async Task<PdfPageReader> OpenAsync(byte[] pdf)
    {
        var stream = new MemoryStream(pdf, writable: false).AsRandomAccessStream();
        try { return new(stream, await PdfDocument.LoadFromStreamAsync(stream).AsTask().ConfigureAwait(false)); }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public bool IsLandscape
    {
        get
        {
            using var page = document.GetPage(0);
            return page.Size.Width > page.Size.Height;
        }
    }

    public async Task<BitmapImage> ReadAsync(int index, uint width)
    {
        using var page = document.GetPage((uint)index);
        using var output = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(output, new PdfPageRenderOptions { DestinationWidth = width }).AsTask().ConfigureAwait(false);
        output.Seek(0);
        using var imageStream = output.AsStreamForRead();
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = imageStream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose() => source.Dispose();
}
