using NAudio.Wave;
using NAudio.Extras;
using NAudio.Wave.SampleProviders;

public class AudioPlaybackEngine : IDisposable
{
    public static readonly AudioPlaybackEngine Instance = new AudioPlaybackEngine(44100, 2);

    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider mixer;
    public int SampleRate { get; }

    private AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
    {
        SampleRate = sampleRate;
        outputDevice = new WaveOutEvent();

        mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
        mixer.ReadFully = true;

        outputDevice.Init(mixer);
        outputDevice.Play();
    }

    private ISampleProvider ConvertToRightChannelCount(ISampleProvider? input)
    {
        if (input == null)
            throw new NullReferenceException(nameof(input));

        if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            return input;
        if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            return new MonoToStereoSampleProvider(input);
        if (input.WaveFormat.Channels == 2 && mixer.WaveFormat.Channels == 1)
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
        mixer.AddMixerInput(input);
    }

    public void AddMixerInput(IWaveProvider input)
    {
        mixer.AddMixerInput(input);
    }

    public void RemoveMixerInput(ISampleProvider sound)
    {
        mixer.RemoveMixerInput(sound);
    }

    public void AddOnMixerInputEnded(EventHandler<SampleProviderEventArgs> e)
    {
        mixer.MixerInputEnded += e;
    }

    public void Dispose()
    {
        outputDevice.Dispose();
    }
}