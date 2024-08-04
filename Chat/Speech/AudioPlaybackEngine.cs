using NAudio.Wave;
using NAudio.Extras;
using NAudio.Wave.SampleProviders;

public sealed class AudioPlaybackEngine : IDisposable
{
    public int SampleRate { get; }
    
    private readonly IWavePlayer _outputDevice;
    private readonly MixingSampleProvider _mixer;

    public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
    {
        SampleRate = sampleRate;
        _outputDevice = new WaveOutEvent();

        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
        _mixer.ReadFully = true;

        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    private ISampleProvider ConvertToRightChannelCount(ISampleProvider? input)
    {
        if (input == null)
            throw new NullReferenceException(nameof(input));

        if (input.WaveFormat.Channels == _mixer.WaveFormat.Channels)
            return input;
        if (input.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
            return new MonoToStereoSampleProvider(input);
        if (input.WaveFormat.Channels == 2 && _mixer.WaveFormat.Channels == 1)
            return new StereoToMonoSampleProvider(input);
        throw new NotImplementedException("Not yet implemented this channel count conversion");
    }

    public void PlaySound(string fileName)
    {
        var input = new AudioFileReader(fileName);
        AddMixerInput(new WdlResamplingSampleProvider(ConvertToRightChannelCount(new AutoDisposeFileReader(input)), SampleRate));
    }

    public void PlaySound(NetworkWavSound sound)
    {
        AddMixerInput(new CachedWavProvider(sound));
    }

    public ISampleProvider ConvertSound(IWaveProvider provider)
    {
        ISampleProvider? converted = null;
        if (provider.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
        {
            if (provider.WaveFormat.BitsPerSample == 8)
            {
                converted = new Pcm8BitToSampleProvider(provider);
            }
            else if (provider.WaveFormat.BitsPerSample == 16)
            {
                converted = new Pcm16BitToSampleProvider(provider);
            }
            else if (provider.WaveFormat.BitsPerSample == 24)
            {
                converted = new Pcm24BitToSampleProvider(provider);
            }
            else if (provider.WaveFormat.BitsPerSample == 32)
            {
                converted = new Pcm32BitToSampleProvider(provider);
            }
        }
        else if (provider.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            if (provider.WaveFormat.BitsPerSample == 64)
            {
                converted = new WaveToSampleProvider64(provider);
            }
            else
            {
                converted = new WaveToSampleProvider(provider);
            }
        }
        else
        {
            throw new ArgumentException("Unsupported source encoding while adding to mixer.");
        }
        return ConvertToRightChannelCount(converted);
    }

    public void AddMixerInput(ISampleProvider input)
    {
        _mixer.AddMixerInput(input);
    }

    public void AddMixerInput(IWaveProvider input)
    {
        _mixer.AddMixerInput(input);
    }

    public void RemoveMixerInput(ISampleProvider sound)
    {
        _mixer.RemoveMixerInput(sound);
    }

    public void AddOnMixerInputEnded(EventHandler<SampleProviderEventArgs> e)
    {
        _mixer.MixerInputEnded += e;
    }

    public void Dispose()
    {
        _outputDevice.Dispose();
    }
}