using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;
using System.Text;

public class CustomEditorUtility{
    [MenuItem("Utility/Copy Relative Path &c", false, 1)]
    public static void CopyRelativePath()
    {
        if (Selection.activeTransform == null)
        {//Project视图
            UnityEngine.Object[] objs = Selection.GetFiltered(typeof(object), SelectionMode.Unfiltered);
            if (objs.Length == 2)
            {
                string path1 = Path.GetFullPath(AssetDatabase.GetAssetPath(objs[0])).Replace("\\", "/");
                string path2 = Path.GetFullPath(AssetDatabase.GetAssetPath(objs[1])).Replace("\\", "/");
                if (path1.IndexOf(path2) == 0)
                {
                    EditorGUIUtility.systemCopyBuffer = path1.Substring(path2.Length + 1);
                }
                else if (path2.IndexOf(path1) == 0)
                {
                    EditorGUIUtility.systemCopyBuffer = path2.Substring(path1.Length + 1);
                }
            }
        }
        else
        {//Hierarchy视图
            UnityEngine.Object[] objs = Selection.GetFiltered(typeof(Transform), SelectionMode.Unfiltered);
            if (objs.Length == 2)
            {
                Transform parent = null, child = null;
                Transform t1 = objs[0] as Transform;
                Transform t2 = objs[1] as Transform;
                if (t1.IsChildOf(t2))
                {
                    parent = t2;
                    child = t1;
                }
                else if (t2.IsChildOf(t1))
                {
                    parent = t1;
                    child = t2;
                }

                if (parent != null && child != null)
                {
                    StringBuilder path = new StringBuilder(child.name);
                    while(child.parent != parent)
                    {
                        child = child.parent;
                        path.Insert(0, child.name + "/");
                    }
                    EditorGUIUtility.systemCopyBuffer = path.ToString();
                }
            }
        }
    }
}
