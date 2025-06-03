using UnityEngine;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;

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
        "anger","disgust","fear","happy","neutral","sadness"
    };

    private Dictionary<string, int> vocab;
    private int clsTokenId, sepTokenId, unkTokenId, padTokenId;
    private Worker worker;

    void Start()
    {
        LoadVocabularyFromTextAsset();
        CreateWorkerFromModelAsset();
    }

    void LoadVocabularyFromTextAsset()
    {
        if (vocabAsset == null)
            throw new Exception("请在 Inspector 中为 vocabAsset 拖入 vocab.txt");

        vocab = new Dictionary<string, int>();

        // 去掉开头和结尾的花括号
        string cleanedText = vocabAsset.text.Trim().TrimStart('{').TrimEnd('}');

        // 每个词条都是用逗号分隔的
        var entries = cleanedText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var parts = entry.Split(new[] { ':' }, 2);
            if (parts.Length != 2)
                continue;

            string key = parts[0].Trim().Trim('"');
            string value = parts[1].Trim();

            if (int.TryParse(value, out int id))
            {
                vocab[key] = id;
            }
        }

        if (!vocab.TryGetValue("<s>", out clsTokenId))
            throw new Exception("<s> 丢失于词汇表");
        if (!vocab.TryGetValue("</s>", out sepTokenId))
            throw new Exception("</s> 丢失于词汇表");

        padTokenId = vocab.TryGetValue("<pad>", out padTokenId) ? padTokenId : 1;
        unkTokenId = vocab.TryGetValue("<unk>", out unkTokenId) ? unkTokenId : padTokenId;

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
        // RoBERTa 是大小写敏感的，所以不要强制 ToLower
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

        logits[4] += logits[6]; // 合并 surprise 到 neutral
        float[] mergedLogits = logits.Take(6).ToArray();

        var probs = Softmax(mergedLogits);
        int best = Array.IndexOf(probs, probs.Max());
        string label = best >= 0 && best < id2label.Count ? id2label[best] : "Unknown";
        return (label, probs[best]);
    }

    float[] Softmax(float[] logits)
    {
        float maxLogit = logits.Max();
        var exps = logits.Select(l => Mathf.Exp(l - maxLogit)).ToArray();
        float sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}
