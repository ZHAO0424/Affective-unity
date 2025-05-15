// EmotionRecognizerSentis.cs

using UnityEngine;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem; // Keep for testing if needed, or remove if test logic is removed

public class EmotionRecognizerSentis : MonoBehaviour
{
    [Header("在 Inspector 里拖入")]
    [Tooltip("拖入你的 ONNX 模型文件 (会自动成为 ModelAsset)")]
    public ModelAsset modelAsset;

    [Tooltip("拖入你的词汇表文件 vocab.txt (会作为 TextAsset)")]
    public TextAsset vocabAsset;

    [Header("分词与标签映射")]
    [Tooltip("与训练时一致的最大序列长度")]
    public int maxTokenLength = 128;

    [Tooltip("根据训练时 label2id 映射顺序填写标签列表")]
    public List<string> id2label = new List<string> {
        "anger","disgust","fear","guilt","joy","sadness","shame"
    };

    private Dictionary<string, int> vocab;
    private int clsTokenId, sepTokenId, unkTokenId, padTokenId;
    private Worker worker;

    // 修改事件，使其可以传递情绪和分数，如果需要的话。
    // 或者，直接让 Speech2 调用 AnalyzeEmotion 并获取返回值。
    // 为简化，这里暂时不修改事件，Speech2将直接使用AnalyzeEmotion的返回值。
    // public event System.Action<string, float> OnTextSentimentRecognized; 

    void Start()
    {
        LoadVocabularyFromTextAsset();
        CreateWorkerFromModelAsset();
    }

    void LoadVocabularyFromTextAsset()
    {
        if (vocabAsset == null)
            throw new Exception("请在 Inspector 中为 vocabAsset 拖入 vocab.txt");

        var lines = vocabAsset.text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        vocab = lines
            .Select((word, idx) => new { word = word.Trim(), idx })
            .ToDictionary(x => x.word, x => x.idx);

        if (!vocab.TryGetValue("[CLS]", out clsTokenId))
            throw new Exception("[CLS] 丢失于词汇表");
        if (!vocab.TryGetValue("[SEP]", out sepTokenId))
            throw new Exception("[SEP] 丢失于词汇表");
        padTokenId = vocab.TryGetValue("[PAD]", out padTokenId) ? padTokenId : 0;
        unkTokenId = vocab.TryGetValue("[UNK]", out unkTokenId) ? unkTokenId : padTokenId;

        Debug.Log($"Loaded vocab ({vocab.Count} tokens): CLS={clsTokenId}, SEP={sepTokenId}, PAD={padTokenId}, UNK={unkTokenId}");
    }

    void CreateWorkerFromModelAsset()
    {
        if (modelAsset == null)
            throw new Exception("请在 Inspector 中为 modelAsset 拖入你的 ONNX 模型");

        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.CPU);
        Debug.Log("Sentis Worker 创建成功");
    }

    List<int> Tokenize(string text, out List<int> attentionMask)
    {
        text = text.ToLowerInvariant();
        char[] splitChars = new[] { ' ', '，', '。', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}' };
        var tokens = text.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

        var ids = new List<int> { clsTokenId };
        var mask = new List<int> { 1 };

        foreach (var t in tokens)
        {
            if (ids.Count >= maxTokenLength - 1)
                break;

            if (vocab.TryGetValue(t, out var id))
                ids.Add(id);
            else
                ids.Add(unkTokenId);

            mask.Add(1);
        }

        ids.Add(sepTokenId);
        mask.Add(1);

        while (ids.Count < maxTokenLength)
        {
            ids.Add(padTokenId);
            mask.Add(0);
        }

        attentionMask = mask;
        return ids;
    }

    // 修改返回类型为 (string emotion, float score)
    public (string emotion, float score) AnalyzeEmotion(string text)
    {
        if (worker == null)
        {
            Debug.LogError("Worker 未就绪，请检查模型加载");
            return ("Error", 0f);
        }

        var inputIds = Tokenize(text, out var attnMask);

        using var tInputIds = new Tensor<int>(new TensorShape(1, maxTokenLength), inputIds.ToArray());
        using var tMask = new Tensor<int>(new TensorShape(1, maxTokenLength), attnMask.ToArray());

        worker.SetInput("input_ids", tInputIds);
        worker.SetInput("attention_mask", tMask);
        worker.Schedule();

        using var tLogits = worker.PeekOutput("logits") as Tensor<float>;
        var logits = tLogits.DownloadToArray();

        var probs = Softmax(logits);
        int best = 0;
        for (int i = 1; i < probs.Length; i++)
        {
            if (probs[i] > probs[best])
            {
                best = i;
            }
        }
        // 或者使用 Linq (与原来保持一致):
        // int best = probs
        //     .Select((p, idx) => new { p, idx })
        //     .OrderByDescending(x => x.p)
        //     .First().idx;


        string label = best >= 0 && best < id2label.Count
            ? id2label[best]
            : "Unknown";

        float bestScore = probs[best];

        //Debug.Log($"输入: \"{text}\" → 文本情绪预测: {label} (Prob: {bestScore:P2})");
        // OnTextSentimentRecognized?.Invoke(label, bestScore); // 如果需要事件，也应修改事件签名
        return (label, bestScore); // 返回情绪和分数
    }

    float[] Softmax(float[] logits)
    {
        float maxLogit = logits.Max();
        var exps = logits.Select(l => Mathf.Exp(l - maxLogit)).ToArray();
        float sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    // Update 方法中的测试代码可以按需保留或移除
    // void Update()
    // {
    //     var kb = Keyboard.current;
    //     if (kb != null && kb.spaceKey.wasPressedThisFrame)
    //     {
    //         AnalyzeEmotion("I am feeling very happy today!");
    //         AnalyzeEmotion("This is so disappointing.");
    //     }
    // }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}
