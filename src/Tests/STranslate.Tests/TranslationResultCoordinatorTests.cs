using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.Tests;

public class TranslationResultCoordinatorTests
{
    [Fact]
    public async Task CancelledOldExecutionCannotOverwriteNewCompletedExecution()
    {
        var coordinator = new TranslationResultCoordinator(
            static key => key == "TranslateCancel" ? "翻译取消" : key);
        var firstStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new StubTranslatePlugin(async (request, result, cancellationToken) =>
        {
            if (request.Text == "first")
            {
                result.Text = "first partial";
                firstStarted.SetResult(true);
                await releaseFirst.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            result.Text = "second completed";
        });

        using var firstCancellation = new CancellationTokenSource();
        var firstOperation = coordinator.BeginOperation(
            "first",
            LangEnum.English,
            LangEnum.ChineseSimplified,
            firstCancellation.Token);
        Assert.True(firstOperation.TryPrepare(plugin));
        var firstTask = firstOperation.TranslateAsync(
            plugin,
            LangEnum.English,
            LangEnum.ChineseSimplified);
        await firstStarted.Task;

        var secondOperation = coordinator.BeginOperation(
            "second",
            LangEnum.English,
            LangEnum.ChineseSimplified,
            CancellationToken.None);
        Assert.True(secondOperation.TryPrepare(plugin));
        var secondResult = await secondOperation.TranslateAsync(
            plugin,
            LangEnum.English,
            LangEnum.ChineseSimplified);

        firstCancellation.Cancel();
        releaseFirst.SetResult(true);
        var firstResult = await firstTask;

        Assert.False(firstResult.IsSuccess);
        Assert.Equal("翻译取消", firstResult.Text);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("second completed", plugin.TransResult.Text);
        Assert.True(plugin.TransResult.IsSuccess);
        Assert.False(plugin.TransResult.IsProcessing);
    }

    [Fact]
    public void NewerStreamingResultCannotBeOverwrittenByCancelledRequest()
    {
        var coordinator = CreateCoordinator();
        var visibleResult = new TranslateResult();

        var firstOperation = BeginOperation(coordinator);
        Assert.True(coordinator.TryReset(visibleResult, firstOperation.OperationId));
        var firstResult = new TranslateResult();
        using var firstForwarding = coordinator.Forward(
            firstResult,
            visibleResult,
            firstOperation.OperationId);
        firstResult.IsProcessing = true;
        firstResult.Text = "first partial";

        var secondOperation = BeginOperation(coordinator);
        Assert.True(coordinator.TryReset(visibleResult, secondOperation.OperationId));
        var secondResult = new TranslateResult();
        using var secondForwarding = coordinator.Forward(
            secondResult,
            visibleResult,
            secondOperation.OperationId);
        secondResult.IsProcessing = true;
        secondResult.Text = "second completed";
        secondResult.Duration = TimeSpan.FromSeconds(2);
        secondResult.IsProcessing = false;

        firstResult.IsSuccess = false;
        firstResult.Text = "翻译取消";
        firstResult.Duration = TimeSpan.FromSeconds(9);
        firstResult.IsProcessing = false;

        Assert.Equal("second completed", visibleResult.Text);
        Assert.True(visibleResult.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(2), visibleResult.Duration);
        Assert.False(visibleResult.IsProcessing);
    }

    [Fact]
    public void CancellationWithoutSuccessorRemainsVisibleAsFailure()
    {
        var coordinator = CreateCoordinator();
        var visibleResult = new TranslateResult();
        var operation = BeginOperation(coordinator);

        Assert.True(coordinator.TryReset(visibleResult, operation.OperationId));
        var executionResult = new TranslateResult();
        using var forwarding = coordinator.Forward(
            executionResult,
            visibleResult,
            operation.OperationId);

        executionResult.IsProcessing = true;
        executionResult.IsSuccess = false;
        executionResult.Text = "翻译取消";
        executionResult.Duration = TimeSpan.FromMilliseconds(300);
        executionResult.IsProcessing = false;

        Assert.Equal("翻译取消", visibleResult.Text);
        Assert.False(visibleResult.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(300), visibleResult.Duration);
        Assert.False(visibleResult.IsProcessing);
    }

    [Fact]
    public void MainAndBackTranslationRejectUpdatesFromOlderOperation()
    {
        var coordinator = CreateCoordinator();
        var visibleMainResult = new TranslateResult();
        var visibleBackResult = new TranslateResult();

        var firstOperation = BeginOperation(coordinator);
        Assert.True(coordinator.TryReset(visibleMainResult, firstOperation.OperationId));
        Assert.True(coordinator.TryReset(visibleBackResult, firstOperation.OperationId));
        var firstMainResult = new TranslateResult();
        var firstBackResult = new TranslateResult();
        using var firstMainForwarding = coordinator.Forward(
            firstMainResult,
            visibleMainResult,
            firstOperation.OperationId);
        using var firstBackForwarding = coordinator.Forward(
            firstBackResult,
            visibleBackResult,
            firstOperation.OperationId);

        var secondOperation = BeginOperation(coordinator);
        Assert.True(coordinator.TryReset(visibleMainResult, secondOperation.OperationId));
        Assert.True(coordinator.TryReset(visibleBackResult, secondOperation.OperationId));
        var secondMainResult = new TranslateResult();
        var secondBackResult = new TranslateResult();
        using var secondMainForwarding = coordinator.Forward(
            secondMainResult,
            visibleMainResult,
            secondOperation.OperationId);
        using var secondBackForwarding = coordinator.Forward(
            secondBackResult,
            visibleBackResult,
            secondOperation.OperationId);

        secondMainResult.Text = "new main";
        secondBackResult.Text = "new back";
        firstMainResult.Text = "old main";
        firstBackResult.Text = "old back";

        Assert.Equal("new main", visibleMainResult.Text);
        Assert.Equal("new back", visibleBackResult.Text);
    }

    [Fact]
    public void NewerDictionaryResultCannotReceiveOlderCollectionsOrError()
    {
        var coordinator = CreateCoordinator();
        var visibleResult = new DictionaryResult();

        var firstOperation = BeginOperation(coordinator);
        Assert.True(coordinator.TryReset(visibleResult, firstOperation.OperationId));

        var secondOperation = BeginOperation(coordinator);
        Assert.True(coordinator.TryReset(visibleResult, secondOperation.OperationId));
        var secondResult = new DictionaryResult
        {
            Text = "new word",
            ResultType = DictionaryResultType.Success,
            Duration = TimeSpan.FromSeconds(1)
        };
        secondResult.Plurals.Add("new words");
        secondResult.Sentences.Add("new sentence");
        Assert.True(coordinator.TryPublish(
            secondResult,
            visibleResult,
            secondOperation.OperationId));

        var firstResult = new DictionaryResult
        {
            Text = "翻译取消",
            ResultType = DictionaryResultType.Error,
            Duration = TimeSpan.FromSeconds(8)
        };
        firstResult.Plurals.Add("old words");
        firstResult.Sentences.Add("old sentence");
        Assert.False(coordinator.TryPublish(
            firstResult,
            visibleResult,
            firstOperation.OperationId));

        Assert.Equal("new word", visibleResult.Text);
        Assert.Equal(DictionaryResultType.Success, visibleResult.ResultType);
        Assert.Equal(["new words"], visibleResult.Plurals);
        Assert.Equal(["new sentence"], visibleResult.Sentences);
        Assert.Equal(TimeSpan.FromSeconds(1), visibleResult.Duration);
    }

    [Fact]
    public void DictionaryNoResultDoesNotPublishPluginDuration()
    {
        var coordinator = CreateCoordinator();
        var visibleResult = new DictionaryResult();
        var operation = BeginOperation(coordinator);
        var pluginResult = new DictionaryResult
        {
            ResultType = DictionaryResultType.NoResult,
            Duration = TimeSpan.FromSeconds(1)
        };

        Assert.True(coordinator.TryReset(visibleResult, operation.OperationId));
        Assert.True(coordinator.TryPublish(
            pluginResult,
            visibleResult,
            operation.OperationId));

        Assert.Equal(DictionaryResultType.NoResult, visibleResult.ResultType);
        Assert.Equal(TimeSpan.Zero, visibleResult.Duration);
    }

    [Fact]
    public void StaleOperationCannotPublishSideEffectsOrReactivateChannel()
    {
        var coordinator = CreateCoordinator();
        var sideEffectsChannel = new object();
        var firstOperation = BeginOperation(coordinator);
        var secondOperation = BeginOperation(coordinator);
        var publishedSideEffects = new List<string>();

        Assert.True(coordinator.TryActivate(
            sideEffectsChannel,
            firstOperation.OperationId));
        Assert.True(coordinator.TryActivate(
            sideEffectsChannel,
            secondOperation.OperationId));
        Assert.False(coordinator.TryActivate(
            sideEffectsChannel,
            firstOperation.OperationId));
        Assert.False(coordinator.TryPublish(
            sideEffectsChannel,
            firstOperation.OperationId,
            () => publishedSideEffects.Add("old history")));
        Assert.True(coordinator.TryPublish(
            sideEffectsChannel,
            secondOperation.OperationId,
            () => publishedSideEffects.Add("new history")));

        Assert.Equal(["new history"], publishedSideEffects);
    }

    private static TranslationResultCoordinator CreateCoordinator() =>
        new(static key => key);

    private static TranslationOperation BeginOperation(TranslationResultCoordinator coordinator) =>
        coordinator.BeginOperation(
            string.Empty,
            LangEnum.Auto,
            LangEnum.English,
            CancellationToken.None);

    private sealed class StubTranslatePlugin(
        Func<TranslateRequest, TranslateResult, CancellationToken, Task> translateAsync)
        : TranslatePluginBase
    {
        public override Task TranslateAsync(
            TranslateRequest request,
            TranslateResult result,
            CancellationToken cancellationToken = default) =>
            translateAsync(request, result, cancellationToken);

        public override string? GetSourceLanguage(LangEnum langEnum) => langEnum.ToString();

        public override string? GetTargetLanguage(LangEnum langEnum) => langEnum.ToString();

        public override System.Windows.Controls.Control GetSettingUI() => null!;

        public override void Init(IPluginContext context)
        {
        }

        public override void Dispose()
        {
        }
    }
}
