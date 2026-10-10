using ScreenTranslator.ViewModels;

namespace ScreenTranslator.Tests;

public sealed class ResultViewModelTests
{
    [Fact]
    public void CancelledTranslation_PreservesOriginalAndDisablesCopyOfTranslation()
    {
        var viewModel = new ResultViewModel();
        viewModel.SetLoading("Đang nhận diện chữ...");
        viewModel.OriginalText = "Hello world";
        viewModel.SetCancelled();
        Assert.Equal("Hello world", viewModel.OriginalText);
        Assert.True(viewModel.HasOriginalText);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsError);
        Assert.False(viewModel.CanCopy);
        viewModel.SetLoading();
        Assert.False(viewModel.HasOriginalText);
    }

    [Fact]
    public void ErrorAfterOcr_AllowsAccessToOriginalText()
    {
        var viewModel = new ResultViewModel { OriginalText = "Hello" };
        viewModel.SetError("API key không hợp lệ");
        Assert.True(viewModel.HasOriginalText);
        Assert.Equal("Hello", viewModel.OriginalText);
        Assert.False(viewModel.CanCopy);
        Assert.True(viewModel.IsError);
    }
}
