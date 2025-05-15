// EmotionRecognizerSentis.cs

using UnityEngine;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem; // Keep for testing if needed, or remove if test logic is removed

public class EmotionRecognizerSentis : MonoBehaviour
{
    [Header("�� Inspector ������")]
    [Tooltip("������� ONNX ģ���ļ� (���Զ���Ϊ ModelAsset)")]
    public ModelAsset modelAsset;

    [Tooltip("������Ĵʻ���ļ� vocab.txt (����Ϊ TextAsset)")]
    public TextAsset vocabAsset;

    [Header("�ִ����ǩӳ��")]
    [Tooltip("��ѵ��ʱһ�µ�������г���")]
    public int maxTokenLength = 128;

    [Tooltip("����ѵ��ʱ label2id ӳ��˳����д��ǩ�б�")]
    public List<string> id2label = new List<string> {
        "anger","disgust","fear","guilt","joy","sadness","shame"
    };

    private Dictionary<string, int> vocab;
    private int clsTokenId, sepTokenId, unkTokenId, padTokenId;
    private Worker worker;

    // �޸��¼���ʹ����Դ��������ͷ����������Ҫ�Ļ���
    // ���ߣ�ֱ���� Speech2 ���� AnalyzeEmotion ����ȡ����ֵ��
    // Ϊ�򻯣�������ʱ���޸��¼���Speech2��ֱ��ʹ��AnalyzeEmotion�ķ���ֵ��
    // public event System.Action<string, float> OnTextSentimentRecognized; 

    void Start()
    {
        LoadVocabularyFromTextAsset();
        CreateWorkerFromModelAsset();
    }

    void LoadVocabularyFromTextAsset()
    {
        if (vocabAsset == null)
            throw new Exception("���� Inspector ��Ϊ vocabAsset ���� vocab.txt");

        var lines = vocabAsset.text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        vocab = lines
            .Select((word, idx) => new { word = word.Trim(), idx })
            .ToDictionary(x => x.word, x => x.idx);

        if (!vocab.TryGetValue("[CLS]", out clsTokenId))
            throw new Exception("[CLS] ��ʧ�ڴʻ��");
        if (!vocab.TryGetValue("[SEP]", out sepTokenId))
            throw new Exception("[SEP] ��ʧ�ڴʻ��");
        padTokenId = vocab.TryGetValue("[PAD]", out padTokenId) ? padTokenId : 0;
        unkTokenId = vocab.TryGetValue("[UNK]", out unkTokenId) ? unkTokenId : padTokenId;

        Debug.Log($"Loaded vocab ({vocab.Count} tokens): CLS={clsTokenId}, SEP={sepTokenId}, PAD={padTokenId}, UNK={unkTokenId}");
    }

    void CreateWorkerFromModelAsset()
    {
        if (modelAsset == null)
            throw new Exception("���� Inspector ��Ϊ modelAsset ������� ONNX ģ��");

        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.CPU);
        Debug.Log("Sentis Worker �����ɹ�");
    }

    List<int> Tokenize(string text, out List<int> attentionMask)
    {
        text = text.ToLowerInvariant();
        char[] splitChars = new[] { ' ', '��', '��', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}' };
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

    // �޸ķ�������Ϊ (string emotion, float score)
    public (string emotion, float score) AnalyzeEmotion(string text)
    {
        if (worker == null)
        {
            Debug.LogError("Worker δ����������ģ�ͼ���");
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
        // ����ʹ�� Linq (��ԭ������һ��):
        // int best = probs
        //     .Select((p, idx) => new { p, idx })
        //     .OrderByDescending(x => x.p)
        //     .First().idx;


        string label = best >= 0 && best < id2label.Count
            ? id2label[best]
            : "Unknown";

        float bestScore = probs[best];

        //Debug.Log($"����: \"{text}\" �� �ı�����Ԥ��: {label} (Prob: {bestScore:P2})");
        // OnTextSentimentRecognized?.Invoke(label, bestScore); // �����Ҫ�¼���ҲӦ�޸��¼�ǩ��
        return (label, bestScore); // ���������ͷ���
    }

    float[] Softmax(float[] logits)
    {
        float maxLogit = logits.Max();
        var exps = logits.Select(l => Mathf.Exp(l - maxLogit)).ToArray();
        float sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    // Update �����еĲ��Դ�����԰��豣�����Ƴ�
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
