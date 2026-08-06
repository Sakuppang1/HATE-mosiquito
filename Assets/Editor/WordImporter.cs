using UnityEngine;
using UnityEditor; // 这个工具只能在编辑器中使用
using System.IO;
using System.Collections.Generic;

public class WordImporter : EditorWindow
{
    [MenuItem("Tools/Import Words")]
    public static void Import()
    {
        string filePath = EditorUtility.OpenFilePanel("选择单词CSV", "", "csv");
        if (string.IsNullOrEmpty(filePath)) return;

        WordData wordData = ScriptableObject.CreateInstance<WordData>();
        wordData.wordList = new List<WordItem>();

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 1; i < lines.Length; i++) // 从 1 开始跳过标题行
        {
            string[] cols = lines[i].Split(',');
            WordItem item = new WordItem { wordName = cols[0], syllables = new List<SyllableInfo>() };

            string[] syls = cols[1].Split('-');
            int stressedIndex = int.Parse(cols[2]);

            for (int j = 0; j < syls.Length; j++)
            {
                item.syllables.Add(new SyllableInfo { 
                    text = syls[j], 
                    isStressed = (j == stressedIndex) 
                });
            }
            wordData.wordList.Add(item);
        }

        AssetDatabase.CreateAsset(wordData, "Assets/B2_WordList.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("导入成功！");
    }
}