// Speech2.cs

using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;

// 定义一个结构体或元组来更好地组织同步后的结果
public struct SynchronizedEmotionResult
{
    public string UtteranceText { get; }
    public string TextEmotion { get; }
    public float TextEmotionScore { get; }
    public string AudioEmotion { get; }
    public float AudioEmotionScore { get; }

    public SynchronizedEmotionResult(string utterance, string textEmo, float textScore, string audioEmo, float audioScore)
    {
        UtteranceText = utterance;
        TextEmotion = textEmo;
        TextEmotionScore = textScore;
        AudioEmotion = audioEmo;
        AudioEmotionScore = audioScore;
    }
}

public class Speech2 : MonoBehaviour
{
    private SpeechRecognizer recognizer;
    private SpeechConfig config;

    [Header("Azure 语音服务配置")]
    public string azureKey = "YOUR_KEY";       // 强烈建议从安全的地方加载，而不是硬编码
    public string azureRegion = "YOUR_REGION"; // 强烈建议从安全的地方加载，而不是硬编码

    private ConcurrentQueue<(string text, double offsetMs, double durationMs)> utteranceQueue
        = new ConcurrentQueue<(string, double, double)>();

    private RealTimeEmotionRecognizer audioRecognizer;
    private EmotionRecognizerSentis textRecognizer;

    // 新增事件，用于传递同步后的完整情绪结果
    public event Action<SynchronizedEmotionResult> OnSynchronizedEmotionRecognized;

    async void Start()
    {
        audioRecognizer = FindObjectOfType<RealTimeEmotionRecognizer>();
        textRecognizer = FindObjectOfType<EmotionRecognizerSentis>();
        if (audioRecognizer == null || textRecognizer == null)
        {
            Debug.LogError("场景中必须同时挂载 RealTimeEmotionRecognizer 和 EmotionRecognizerSentis 脚本。");
            enabled = false; // 禁用此脚本以避免后续错误
            return;
        }

        if (string.IsNullOrEmpty(azureKey) || azureKey == "YOUR_KEY")
        {
            Debug.LogError("Azure Speech Key 未配置!");
            enabled = false;
            return;
        }
        if (string.IsNullOrEmpty(azureRegion) || azureRegion == "YOUR_REGION")
        {
            Debug.LogError("Azure Speech Region 未配置!");
            enabled = false;
            return;
        }


        config = SpeechConfig.FromSubscription(azureKey, azureRegion);
        config.SpeechRecognitionLanguage = "en-US"; // 根据需要设置语言

        // 为了获取更准确的 offset 和 duration，可以考虑启用词级别时间戳
        // config.RequestWordLevelTimestamps(); 
        // 注意：如果启用，e.Result.OffsetInTicks 和 e.Result.Duration 仍然是句子级别的
        // 词级别时间戳需要通过 e.Result.Best() 和 WordLevelTimingResult 处理

        recognizer = new SpeechRecognizer(config);
        recognizer.Recognized += RecognizedHandler;
        // 可以添加其他事件处理器，例如处理会话开始/结束，识别错误等
        // recognizer.SessionStarted += (s, e) => Debug.Log("Speech session started.");
        // recognizer.SessionStopped += (s, e) => Debug.Log("Speech session stopped.");
        // recognizer.Canceled += (s, e) => Debug.LogError($"Speech recognition canceled: {e.Reason}, ErrorDetails: {e.ErrorDetails}");


        try
        {
            await recognizer.StartContinuousRecognitionAsync();
            Debug.Log("Continuous speech recognition started.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start continuous recognition: {ex.Message}");
            enabled = false;
        }
    }

    private void RecognizedHandler(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
        {
            string text = e.Result.Text;
            // OffsetInTicks 是相对于音频流开始的偏移量，单位是 100 纳秒 (0.0001毫秒)
            // Duration 是识别出的语音片段的持续时间
            double offsetMs = e.Result.OffsetInTicks / 10000.0;
            double durationMs = e.Result.Duration.TotalMilliseconds;

            // 确保 duration 是正数，因为后续的 ExtractAudioSegment 需要
            if (durationMs <= 0)
            {
                Debug.LogWarning($"Recognized speech \"{text}\" has zero or negative duration ({durationMs}ms). Skipping emotion analysis for this segment.");
                return;
            }

            utteranceQueue.Enqueue((text, offsetMs, durationMs));
            // Debug.Log($"Queued: \"{text}\" (Offset: {offsetMs:F0}ms, Duration: {durationMs:F0}ms)");
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            // Debug.Log("NOMATCH: Speech could not be recognized.");
        }
    }

    void Update()
    {
        if (utteranceQueue.TryDequeue(out var item))
        {
            // 1. 文本情绪分析 (现在返回情绪和分数)
            (string textEmo, float textScore) = textRecognizer.AnalyzeEmotion(item.text);

            // 2. 提取对应音频并进行语音情绪分析
            // 确保 audioRecognizer 和 _recordingClip 已准备好
            // 并且 item.offsetMs 和 item.durationMs 是有效的
            float[] audioSegment = null;
            try
            {
                // 注意：ExtractAudioSegment 依赖于 _recordingClip 仍然包含这个时间段的音频数据
                // 如果识别延迟很高，或者 recordingBufferLengthSec 太短，这里可能会取到不正确的音频
                audioSegment = audioRecognizer.ExtractAudioSegment(item.offsetMs, item.durationMs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error extracting audio segment for \"{item.text}\": {ex.Message}. Offset: {item.offsetMs}, Duration: {item.durationMs}");
                // 可以选择跳过语音情绪分析或使用默认值
            }

            string audioEmo = "Error";
            float audioScore = 0f;

            if (audioSegment != null && audioSegment.Length > 0)
            {
                (audioEmo, audioScore) = audioRecognizer.AnalyzeAudioSegment(audioSegment);
            }
            else
            {
                Debug.LogWarning($"Audio segment for \"{item.text}\" was null or empty. Skipping audio emotion analysis.");
                audioEmo = "N/A"; // Or some other indicator
            }


            // 3. 合并输出
            string outputLog = $"完整句: \"{item.text}\"\n" +
                               $"✍️ 文本情绪: {textEmo} (Score: {textScore:P2})\n" +
                               $"🔊 语音情绪: {audioEmo} (Score: {audioScore:F2})"; // audioScore 来自 AnalyzeAudioSegment 的第二个返回值
            Debug.Log(outputLog);

            // 4. 触发事件，传递包含所有信息的结果
            var result = new SynchronizedEmotionResult(item.text, textEmo, textScore, audioEmo, audioScore);
            OnSynchronizedEmotionRecognized?.Invoke(result);
        }
    }

    private async void OnDestroy()
    {
        if (recognizer != null)
        {
            recognizer.Recognized -= RecognizedHandler;
            // Optionally unsubscribe from other events if you added them
            // recognizer.SessionStarted -= ...
            // recognizer.SessionStopped -= ...
            // recognizer.Canceled -= ...
            try
            {
                await recognizer.StopContinuousRecognitionAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Exception during StopContinuousRecognitionAsync: {ex.Message}");
            }
            finally
            {
                recognizer.Dispose();
                recognizer = null;
            }
            Debug.Log("Speech recognizer stopped and disposed.");
        }
    }
}

