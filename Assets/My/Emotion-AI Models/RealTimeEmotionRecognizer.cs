using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using Unity.Sentis;

public class RealTimeEmotionRecognizer : MonoBehaviour
{
    [Header("模型设置")]
    public ModelAsset modelAsset;
    public event Action<string> OnAudioEmotionRecognized;

    [Header("音频输入设置")]
    public string microphoneDeviceName = null;
    public int recordingBufferLengthSec = 5;   // 环形缓冲区长度(秒)
    public float inferenceInterval = 0.5f;     // 连续推理间隔(秒)

    [Header("实时结果 (只读)")]
    public Text resultTextUI = null;           // UI 文本组件

    private const int SampleRate = 16000;      // wav2vec2 预期采样率
    // 6 类情绪标签(与 id2label 对应)
    private readonly List<string> _emotionLabels = new List<string> {
        "SAD","ANGRY","DISGUST","FEAR","HAPPY","NEUTRAL"
    };

    private Worker _worker;
    private AudioClip _recordingClip;
    private float[] _processingBuffer;       // 环形缓冲区
    private float _timer = 0f;

    void Start()
    {
        if (modelAsset == null) throw new Exception("请在 Inspector 拖入 ModelAsset");
        var runtimeModel = ModelLoader.Load(modelAsset);
        _worker = new Worker(runtimeModel, BackendType.CPU);

        // 环形缓冲区长度 = bufferSec * sampleRate
        _processingBuffer = new float[recordingBufferLengthSec * SampleRate];

        if (Microphone.devices.Length == 0) throw new Exception("未检测到麦克风设备");
        if (string.IsNullOrEmpty(microphoneDeviceName))
            microphoneDeviceName = Microphone.devices[0];

        _recordingClip = Microphone.Start(microphoneDeviceName, true, recordingBufferLengthSec, SampleRate);
        Invoke(nameof(CheckMicStarted), 0.5f);
    }

    void CheckMicStarted()
    {
        if (Microphone.GetPosition(microphoneDeviceName) <= 0)
            Debug.LogWarning("麦克风尚未开始录制");
        else
            Debug.Log($"麦克风已启动，采样率: {_recordingClip.frequency}Hz");
    }

    void Update()
    {
        // 连续采样并不触发事件，真正的触发在 SpeechRecognizer 回调里
        _timer += Time.deltaTime;
        if (_timer >= inferenceInterval)
        {
            _timer -= inferenceInterval;
            // 这里可以做半实时反馈，但我们后面会用完整句子触发
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
            resultTextUI.text = $"情绪: {emo}\n(Logit: {score:F2})";
        OnAudioEmotionRecognized?.Invoke(emo);
        Debug.Log($"Sentis 预测: {emo} ({score:F2})");
    }

    /// <summary>
    /// 截取指定时长/偏移的音频段（ms）
    /// </summary>
    public float[] ExtractAudioSegment(double offsetMs, double durationMs)
    {
        int clipSamples = _recordingClip.samples;
        int startSample = (int)(offsetMs * SampleRate / 1000.0);
        int length = (int)(durationMs * SampleRate / 1000.0);
        var seg = new float[length];

        // 环形缓冲读取
        int idx = startSample % clipSamples;
        _recordingClip.GetData(seg, idx);
        return seg;
    }

    /// <summary>
    /// 对任意音频段做一次推理，返回 (情绪, logit 得分)
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

