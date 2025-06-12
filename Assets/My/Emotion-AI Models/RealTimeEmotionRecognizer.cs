using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using Unity.Sentis;

public class RealTimeEmotionRecognizer : MonoBehaviour
{
    [Header("ģ������")]
    public ModelAsset modelAsset;
    public event Action<string> OnAudioEmotionRecognized;

    [Header("��Ƶ��������")]
    public string microphoneDeviceName = null;
    public int recordingBufferLengthSec = 5;   // ���λ���������(��)
    public float inferenceInterval = 0.5f;     // ����������(��)

    [Header("ʵʱ��� (ֻ��)")]
    public Text resultTextUI = null;           // UI �ı����

    private const int SampleRate = 16000;      // wav2vec2 Ԥ�ڲ�����
    // 6 ��������ǩ(�� id2label ��Ӧ)
    private readonly List<string> _emotionLabels = new List<string> {
        "SAD","ANGRY","DISGUST","FEAR","HAPPY","NEUTRAL"
    };

    private Worker _worker;
    private AudioClip _recordingClip;
    private float[] _processingBuffer;       // ���λ�����
    private float _timer = 0f;

    void Start()
    {
        if (modelAsset == null) throw new Exception("���� Inspector ���� ModelAsset");
        var runtimeModel = ModelLoader.Load(modelAsset);
        _worker = new Worker(runtimeModel, BackendType.CPU);

        // ���λ��������� = bufferSec * sampleRate
        _processingBuffer = new float[recordingBufferLengthSec * SampleRate];

        if (Microphone.devices.Length == 0) throw new Exception("δ��⵽��˷��豸");
        if (string.IsNullOrEmpty(microphoneDeviceName))
            microphoneDeviceName = Microphone.devices[0];

        _recordingClip = Microphone.Start(microphoneDeviceName, true, recordingBufferLengthSec, SampleRate);
        Invoke(nameof(CheckMicStarted), 0.5f);
    }

    void CheckMicStarted()
    {
        if (Microphone.GetPosition(microphoneDeviceName) <= 0)
            Debug.LogWarning("��˷���δ��ʼ¼��");
        else
            Debug.Log($"��˷���������������: {_recordingClip.frequency}Hz");
    }

    void Update()
    {
        // �����������������¼��������Ĵ����� SpeechRecognizer �ص���
        _timer += Time.deltaTime;
        if (_timer >= inferenceInterval)
        {
            _timer -= inferenceInterval;
            // �����������ʵʱ�����������Ǻ�������������Ӵ���
            // ProcessMicrophoneData();
        }
    }

    void ProcessMicrophoneData()
    {
        int pos = Microphone.GetPosition(microphoneDeviceName);
        int total = _recordingClip.samples;
        int len = _processingBuffer.Length;
        int start = (pos - len + total) % total;
        _recordingClip.GetData(_processingBuffer, start);
        RecognizeEmotion(_processingBuffer);
    }

    void RecognizeEmotion(float[] audioData)
    {
        var (emo, score) = AnalyzeAudioSegment(audioData);
        if (resultTextUI != null)
            resultTextUI.text = $"����: {emo}\n(Logit: {score:F2})";
        OnAudioEmotionRecognized?.Invoke(emo);
        Debug.Log($"Sentis Ԥ��: {emo} ({score:F2})");
    }

    /// <summary>
    /// ��ȡָ��ʱ��/ƫ�Ƶ���Ƶ�Σ�ms��
    /// </summary>
    public float[] ExtractAudioSegment(double offsetMs, double durationMs)
    {
        int clipSamples = _recordingClip.samples;
        int startSample = (int)(offsetMs * SampleRate / 1000.0);
        int length = (int)(durationMs * SampleRate / 1000.0);
        var seg = new float[length];

        // ���λ����ȡ
        int idx = startSample % clipSamples;
        _recordingClip.GetData(seg, idx);
        return seg;
    }

    /// <summary>
    /// ��������Ƶ����һ���������� (����, logit �÷�)
    /// </summary>
    public (string emotion, float score) AnalyzeAudioSegment(float[] audioData)
    {
        var shape = new TensorShape(1, audioData.Length);
        using var input = new Tensor<float>(shape, audioData);
        _worker.SetInput("input_values", input);
        _worker.Schedule();

        using var output = _worker.PeekOutput("logits") as Tensor<float>;
        float[] logits = output.DownloadToArray();

        int best = 0;
        for (int i = 1; i < logits.Length; i++)
            if (logits[i] > logits[best]) best = i;

        return (_emotionLabels[best], logits[best]);
    }

    void OnDestroy()
    {
        if (_recordingClip != null && Microphone.IsRecording(microphoneDeviceName))
            Microphone.End(microphoneDeviceName);
        _worker?.Dispose();
    }
}

