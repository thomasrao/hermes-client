using NAudio.Wave;

public class NetworkWavSound
{
    public byte[] AudioData { get; private set; }
    public WaveFormat WaveFormat { get; private set; }

    public NetworkWavSound(string uri)
    {
        using (var mfr = new MediaFoundationReader(uri)) {
            WaveFormat = mfr.WaveFormat;

            byte[] buffer = new byte[4096];
            int read = 0;
            using (var ms = new MemoryStream()) {
                while ((read = mfr.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, read);
                AudioData = ms.ToArray();
            }
        }
    }
}

public class CachedWavProvider : IWaveProvider
{
    private readonly NetworkWavSound _sound;
    private readonly RawSourceWaveStream _stream;

    public WaveFormat WaveFormat { get => _sound.WaveFormat; }


    public CachedWavProvider(NetworkWavSound cachedSound)
    {
        _sound = cachedSound;
        _stream = new RawSourceWaveStream(new MemoryStream(_sound.AudioData), _sound.WaveFormat);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
    }
}