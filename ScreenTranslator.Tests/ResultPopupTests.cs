using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenTranslator.Views;

namespace ScreenTranslator.Tests;

public sealed class ResultPopupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultPopup_RendersOriginalAndTranslationWithoutBindingErrors(bool loading)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var messages = new StringWriter();
            using var listener = new TextWriterTraceListener(messages);
            var source = PresentationTraceSources.DataBindingSource;
            var previousLevel = source.Switch.Level;
            source.Switch.Level = SourceLevels.Error;
            source.Listeners.Add(listener);
            try
            {
                using var cts = new CancellationTokenSource();
                var popup = new ResultPopup(cts.Cancel);
                try
                {
                    popup.ViewModel.LanguagePair = "English → Vietnamese";
                    popup.ViewModel.ElapsedText = "1.2 giây";
                    popup.ViewModel.OriginalText = "Sign in to continue.\nEnter your email address and password.";
                    if (loading) popup.ViewModel.DisplayText = "Đang dịch...";
                    else popup.UpdateSuccess("Đăng nhập để tiếp tục.\nNhập địa chỉ email và mật khẩu của bạn.");
                    ((Expander)popup.FindName("OriginalExpander")).IsExpanded = true;
                    var content = (FrameworkElement)popup.Content;
                    popup.Content = null;
                    content.DataContext = popup.ViewModel;
                    content.Measure(new Size(600, 440));
                    content.Arrange(new Rect(0, 0, 600, 440));
                    content.UpdateLayout();
                    Assert.Equal(Visibility.Visible, ((Button)popup.FindName("BtnCopyOriginal")).Visibility);
                    Assert.True(((Button)popup.FindName("BtnCopyOriginal")).ActualWidth > 0);
                    Assert.Equal(loading ? Visibility.Collapsed : Visibility.Visible, ((Button)popup.FindName("BtnCopy")).Visibility);
                    Assert.Equal(loading ? Visibility.Visible : Visibility.Collapsed, ((Button)popup.FindName("BtnCancel")).Visibility);
                    var image = new RenderTargetBitmap(600, 440, 96, 96, PixelFormats.Pbgra32);
                    image.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using var output = File.Create(Path.Combine(AppContext.BaseDirectory, loading ? "popup-loading-preview.png" : "popup-preview.png"));
                    encoder.Save(output);
                    listener.Flush();
                    Assert.Empty(messages.ToString());
                    if (loading)
                    {
                        ((Button)popup.FindName("BtnCancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert.True(cts.IsCancellationRequested);
                        Assert.False(popup.ViewModel.IsLoading);
                        Assert.True(popup.ViewModel.HasOriginalText);
                    }
                }
                finally { popup.Close(); }
                completed.TrySetResult();
            }
            catch (Exception ex) { completed.TrySetException(ex); }
            finally
            {
                source.Listeners.Remove(listener);
                source.Switch.Level = previousLevel;
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
