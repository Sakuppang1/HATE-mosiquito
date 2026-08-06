using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWordList", menuName = "WordGame/WordList")]
public class WordData : ScriptableObject
{
    // 检查这里是否真的写了下面这一行！
    public List<WordItem> wordList; 
}

[Serializable]
public class WordItem
{
    public string wordName;
    public List<SyllableInfo> syllables;
}

[Serializable]
public class SyllableInfo
{
    public string text;
    public bool isStressed;
}