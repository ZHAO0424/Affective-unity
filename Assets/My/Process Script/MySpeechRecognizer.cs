using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;
using System.Collections.Concurrent; // 引入并发队列命名空间

public class MySpeechRecognizer : MonoBehaviour
{
    private SpeechRecognizer recognizer;
    private SpeechConfig config;

    // 替换为你的密钥和区域
    private string azureKey = "9Wrtp6XioyLyqpaRwOZvc4xdQhEByQz0Yy1ZZ5m2la7KBMJphvxiJQQJ99BDACYeBjFXJ3w3AAAEACOGo85n"; // 请替换为你自己的 Key
    private string azureRegion = "eastus"; // 请替换为你自己的 Region

    // 用于在线程间安全传递识别结果的队列
    private ConcurrentQueue<string> recognizedTextQueue = new ConcurrentQueue<string>();

    // 缓存 SentimentAnalyzer 实例，避免每次都 FindObjectOfType
    private SentimentAnalyzer sentimentAnalyzerInstance;

    async void Start()
    {
        // 在 Start 中查找并缓存 SentimentAnalyzer 实例
        sentimentAnalyzerInstance = FindObjectOfType<SentimentAnalyzer>();
        if (sentimentAnalyzerInstance == null)
        {
            Debug.LogError("场景中未找到 SentimentAnalyzer 组件！请确保已将其添加到某个活动的 GameObject 上。");
            return; // 如果找不到，则不继续执行
        }

        config = SpeechConfig.FromSubscription(azureKey, azureRegion);
        config.SpeechRecognitionLanguage = "en-US";

        recognizer = new SpeechRecognizer(config);

        recognizer.Recognizing += (s, e) => {
            // 这个日志在后台线程是安全的
            Debug.Log("识别中... " + e.Result.Text);
        };

        recognizer.Recognized += (s, e) => {
            // 检查识别是否成功并且有文本结果
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                string resultText = e.Result.Text;
                Debug.Log("识别完成 (准备入队): " + resultText);
                // 将结果放入队列，这是线程安全的
                recognizedTextQueue.Enqueue(resultText);
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Debug.Log("NOMATCH: 未识别到语音。");
            }
            else if (e.Result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(e.Result);
                Debug.LogWarning($"识别被取消: {cancellation.Reason}. 详情: {cancellation.ErrorDetails}");
            }
        };

        recognizer.Canceled += (s, e) => {
            // 这个日志在后台线程是安全的
            Debug.LogWarning($"识别被取消: {e.Reason}. 错误详情: {e.ErrorDetails}");
        };

        await recognizer.StartContinuousRecognitionAsync();
        Debug.Log("已启动连续语音识别...");
    }

    // Update 在 Unity 主线程上每帧运行
    void Update()
    {
        // 检查队列中是否有待处理的文本
        if (sentimentAnalyzerInstance != null && recognizedTextQueue.TryDequeue(out string textToAnalyze))
        {
            // 从队列中取出文本，并在主线程调用情感分析
            Debug.Log($"从队列取出，调用 AnalyzeSentiment: {textToAnalyze}");
            sentimentAnalyzerInstance.AnalyzeSentiment(textToAnalyze);
        }

        // 保留你的空格键测试（如果需要）
        // if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        // {
        //     if (sentimentAnalyzerInstance != null)
        //     {
        //         sentimentAnalyzerInstance.AnalyzeSentiment("我觉得很开心，谢谢你"); // 用空格键测试
        //     }
        // }
    }


    private async void OnDisable()
    {
        if (recognizer != null)
        {
            Debug.Log("停止连续语音识别...");
            await recognizer.StopContinuousRecognitionAsync();
            recognizer.Dispose();
            recognizer = null; // 清理引用
            Debug.Log("语音识别器已停止并释放。");
        }
    }

    // （可选）应用程序退出时也确保停止
    private async void OnApplicationQuit()
    {
        if (recognizer != null)
        {
            await recognizer.StopContinuousRecognitionAsync();
            recognizer.Dispose();
            recognizer = null;
        }
    }
}