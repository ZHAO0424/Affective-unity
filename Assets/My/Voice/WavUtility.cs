using UnityEngine;
using System;
using System.IO;

public static class WavUtility
{
    const int HEADER_SIZE = 44;

    public static byte[] FromAudioClip(AudioClip clip, out string filepath, bool trimSilence = false)
    {
        var samples = new float[clip.samples];
        clip.GetData(samples, 0);

        byte[] bytesData = ConvertAndWrite(samples, clip.channels, clip.frequency);
        filepath = Path.Combine(Application.persistentDataPath, "temp.wav");

        using (FileStream fileStream = new FileStream(filepath, FileMode.Create))
        {
            WriteHeader(fileStream, clip, bytesData.Length);
            fileStream.Write(bytesData, 0, bytesData.Length);
        }

        return bytesData;
    }

    private static byte[] ConvertAndWrite(float[] samples, int channels, int sampleRate)
    {
        int rescaleFactor = 32767; // 16-bit
        byte[] bytes = new byte[samples.Length * 2];
        int offset = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            short val = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(val);
            bytes[offset++] = byteArr[0];
            bytes[offset++] = byteArr[1];
        }
        return bytes;
    }

    private static void WriteHeader(FileStream stream, AudioClip clip, int byteLength)
    {
        int hz = clip.frequency;
        int channels = clip.channels;
        int samples = clip.samples;

        stream.Seek(0, SeekOrigin.Begin);

        // Chunk ID
        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        // ChunkSize
        stream.Write(BitConverter.GetBytes(byteLength + HEADER_SIZE - 8), 0, 4);
        // Format
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);
        // Subchunk1 ID
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        // Subchunk1 size
        stream.Write(BitConverter.GetBytes(16), 0, 4);
        // AudioFormat
        stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
        // NumChannels
        stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
        // SampleRate
        stream.Write(BitConverter.GetBytes(hz), 0, 4);
        // ByteRate
        stream.Write(BitConverter.GetBytes(hz * channels * 2), 0, 4);
        // BlockAlign
        stream.Write(BitConverter.GetBytes((ushort)(channels * 2)), 0, 2);
        // BitsPerSample
        stream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
        // Subchunk2 ID
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        // Subchunk2 size
        stream.Write(BitConverter.GetBytes(byteLength), 0, 4);
    }
}
