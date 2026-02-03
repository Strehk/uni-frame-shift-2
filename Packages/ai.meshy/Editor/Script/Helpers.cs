using UnityEngine;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using System;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine.Networking;

namespace Meshy
{
    /// <summary>
    /// A window can render many GUIModel.
    /// One GUIModel bear one method.
    /// Include sending web request, getting response, getting the information you want to send, rendering the information you want to show.
    /// If you want to have more than one kind of web requests, please finish your own function to select witch request you want to send.
    ///
    /// How to use:
    /// 1. Create a class witch is inheritance of GUIModel.
    /// 2. Override the functions, witch you need.
    /// 3. You have to override Draw().
    /// 4. Use the Draw function in OnGUI function.
    /// </summary>
    public class GUIModel
    {
        static public string API_KEY_FIELD = "Meshy API Keys";
        public class jsonObj
        {
            public string message;
        }

        // Here achieve the web request.
        public virtual void WebRequest()
        {
            Debug.Log("Here override the WebRequest function.");
        }

        // Here achieve the GUI rendering.
        public virtual void Draw()
        {
            Debug.Log("Here override the Draw function.");
        }

        public static void CheckErrorCode(long errorCode, string text)
        {
            switch (errorCode)
            {
                case 400:
                    {
                        Debug.LogError("Bad Request!");
                        break;
                    }
                case 401:
                    {
                        Debug.LogError("Unauthorized!");
                        break;
                    }
                case 402:
                    {
                        Debug.LogError("Payment Required!");
                        break;
                    }
                case 404:
                    {
                        Debug.LogError("Not Found!");
                        break;
                    }
                case 429:
                    {
                        Debug.LogError("Too Many Requests!");
                        break;
                    }
                case var n when (n >= 500 && n < 600):
                    {
                        Debug.LogError("Server Error!");
                        break;
                    }
                default:
                    {
                        Debug.LogError("Unknown Error.");
                        break;
                    }
            }
            jsonObj jobj;
            jobj = JsonUtility.FromJson<jsonObj>(text);
            Debug.LogError(jobj.message);
        }
    }

    public class Utils
    {
        // Reflection workaround to export binary fbx
        // https://forum.unity.com/threads/fbx-exporter-binary-export-doesnt-work-via-editor-scripting.1114222/
        public static string ExportBinaryFBX(string filePath, UnityEngine.Object singleObject)
        {
            // Find relevant internal types in Unity.Formats.Fbx.Editor assembly
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().First(x => x.FullName == "Unity.Formats.Fbx.Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").GetTypes();
            Type optionsInterfaceType = types.First(x => x.Name == "IExportOptions");
            Type optionsType = types.First(x => x.Name == "ExportOptionsSettingsSerializeBase");

            // Instantiate a settings object instance
            MethodInfo optionsProperty = typeof(ModelExporter).GetProperty("DefaultOptions", BindingFlags.Static | BindingFlags.NonPublic).GetGetMethod(true);
            object optionsInstance = optionsProperty.Invoke(null, null);

            // Change the export setting from ASCII to binary
            FieldInfo exportFormatField = optionsType.GetField("exportFormat", BindingFlags.Instance | BindingFlags.NonPublic);
            exportFormatField.SetValue(optionsInstance, 1);

            // Invoke the ExportObject method with the settings param
            MethodInfo exportObjectMethod = typeof(ModelExporter).GetMethod("ExportObject", BindingFlags.Static | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(string), typeof(UnityEngine.Object), optionsInterfaceType }, null);
            return (string)exportObjectMethod.Invoke(null, new object[] { filePath, singleObject, optionsInstance });
        }

        public static void ImportAsAsset(byte[] results)
        {
            string rootPath = Application.dataPath;
            if (!File.Exists(rootPath + "/Meshy"))
            {
                Directory.CreateDirectory(rootPath + "/Meshy");
            }

            // Set importing path
            string path;
            string fileName = "Meshy-model";
            path = rootPath + "/Meshy" + "/" + fileName + ".fbx";

            int index = 1;
            while (File.Exists(path))
            {
                index++;
                path = rootPath + "/Meshy" + "/" + fileName + index.ToString() + ".fbx";
            }

            // Write binary data into file
            File.WriteAllBytes(path, results);
            AssetDatabase.Refresh();
            Debug.Log("Download " + fileName + " completed!");
        }

        public static bool CheckRequestSuccess(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success && request.downloadHandler != null)
            {
                return true;
            }
            GUIModel.CheckErrorCode(request.responseCode, request.downloadHandler.text);
            return false;
        }

    }
}
