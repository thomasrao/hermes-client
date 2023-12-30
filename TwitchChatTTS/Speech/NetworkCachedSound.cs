using NAudio.Wave;
using System;

public class NetworkWavSound
{
    public byte[] AudioData { get; private set; }
    public WaveFormat WaveFormat { get; private set; }

    public NetworkWavSound(string uri)
    {
        using (var mfr = new MediaFoundationReader(uri)) {
            WaveFormat = mfr.WaveFormat;
            //Console.WriteLine("W: " + WaveFormat.SampleRate + " C: " + WaveFormat.Channels + " B: " + WaveFormat.BitsPerSample + " E: " + WaveFormat.Encoding);

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
    private readonly NetworkWavSound sound;
    private long position;
    private readonly RawSourceWaveStream stream;

    public WaveFormat WaveFormat { get => sound.WaveFormat; }


    public CachedWavProvider(NetworkWavSound cachedSound)
    {
        sound = cachedSound;
        stream = new RawSourceWaveStream(new MemoryStream(sound.AudioData), sound.WaveFormat);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return stream.Read(buffer, offset, count);
    }
}