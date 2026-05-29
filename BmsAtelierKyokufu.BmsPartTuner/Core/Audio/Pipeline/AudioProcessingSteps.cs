using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio.Pipeline;

internal sealed class LoadAndDeinterleaveStep : IAudioProcessingStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(LoadAndDeinterleaveStep));

    public void Execute(AudioProcessingContext context)
    {
        var (samplesPerChannel, fileInfo) = AudioFileReaderService.LoadAndDeinterleave(context.Path);
        context.SamplesPerChannel = samplesPerChannel;
        context.FileInfo = fileInfo;
    }
}

internal sealed class ApplyNormalizationStep : IAudioProcessingStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(ApplyNormalizationStep));

    public void Execute(AudioProcessingContext context)
    {
        if (context.NormalizationMode != NormalizationMode.None && context.SamplesPerChannel != null)
        {
            AudioNormalizationEngine.ApplyNormalization(
                context.SamplesPerChannel,
                context.Channels,
                context.NormalizationMode);
        }
    }
}

internal sealed class ExtractMetricsStep : IAudioProcessingStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(ExtractMetricsStep));

    public void Execute(AudioProcessingContext context)
    {
        if (context.SamplesPerChannel != null)
        {
            context.Metrics = AudioNormalizationEngine.ExtractMetrics(
                context.SamplesPerChannel,
                context.SamplesPerChannelLen,
                context.Channels);
        }
    }
}

internal sealed class ExtractFeaturesStep : IAudioProcessingStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(ExtractFeaturesStep));

    public void Execute(AudioProcessingContext context)
    {
        if (context.SamplesPerChannel != null && context.Metrics != null)
        {
            context.Features = AudioFeatureExtractor.ExtractAllFeatures(
                context.SamplesPerChannel,
                context.SamplesPerChannelLen,
                context.Metrics.Regions,
                context.Channels);
        }
    }
}

internal sealed class BuildResultStep : IAudioProcessingStep
{
    public string Name => PipelineStepHelper.GetStepName(nameof(BuildResultStep));

    public void Execute(AudioProcessingContext context)
    {
        if (context.FileInfo != null && context.Metrics != null && context.Features != null)
        {
            var p = new PreNormalizedSoundDataParameters(
                context.FileInfo,
                context.Metrics,
                context.Features
            );
            context.Result = new PreNormalizedSoundData(p);
        }
        else
        {
            throw new InvalidOperationException("Missing required data to build PreNormalizedSoundData.");
        }
    }
}
