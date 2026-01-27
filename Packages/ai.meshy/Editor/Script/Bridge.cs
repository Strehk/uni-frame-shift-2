using UnityEngine;
using UnityEditor;
using System.Net;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.IO.Compression;
using UnityEditor.SceneManagement;
using System.Linq;  // 用于 OfType<AnimationClip>()
using UnityEditor.Animations;  // 用于 AnimatorController

public class MeshyBridgeWindow : EditorWindow
{
    private static MeshyBridge bridgeInstance;
    private static bool isBridgeRunning = false;
    private GUIContent runButtonContent;
    private GUIContent stopButtonContent;

    [MenuItem("Meshy/Bridge")]
    public static void ShowWindow()
    {
        var window = GetWindow<MeshyBridgeWindow>("Meshy Bridge");
        window.minSize = new Vector2(250, 100);
        window.maxSize = new Vector2(400, 150);
    }

    private void OnEnable()
    {
        runButtonContent = new GUIContent("Run Bridge");
        stopButtonContent = new GUIContent("Bridge ON");

        isBridgeRunning = bridgeInstance != null;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        GUILayout.Space(10);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.fixedHeight = 40;

        Color originalColor = GUI.backgroundColor;
        if (isBridgeRunning)
        {
            GUI.backgroundColor = new Color(0.4f, 0.6f, 1.0f);
        }

        GUIContent currentContent = isBridgeRunning ? stopButtonContent : runButtonContent;
        if (GUILayout.Button(currentContent, buttonStyle))
        {
            ToggleBridgeState();
        }

        GUI.backgroundColor = originalColor;

        EditorGUILayout.EndVertical();
    }

    private void ToggleBridgeState()
    {
        if (isBridgeRunning)
        {
            StopBridge();
        }
        else
        {
            StartBridge();
        }
    }

    private static void StartBridge()
    {
        if (bridgeInstance == null)
        {
            var go = new GameObject("MeshyBridge");
            bridgeInstance = go.AddComponent<MeshyBridge>();
            isBridgeRunning = true;
            Debug.Log("Meshy Bridge started");
        }
    }

    private static void StopBridge()
    {
        if (bridgeInstance != null)
        {
            bridgeInstance.StopServer();
            DestroyImmediate(bridgeInstance.gameObject);
            bridgeInstance = null;
            isBridgeRunning = false;
            Debug.Log("Meshy Bridge stopped");
        }
    }

    private void OnDestroy()
    {
    }
}

public static class MeshyBridgeCommands
{
    private static void StartBridge()
    {
        MeshyBridgeWindow.ShowWindow();
        var windowType = typeof(MeshyBridgeWindow);
        var method = windowType.GetMethod("StartBridge", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Invoke(null, null);
    }
}

[ExecuteInEditMode]
public class MeshyBridge : MonoBehaviour
{
    private string _tempCachePath;

    private Thread serverThread;
    private Thread guardThread;
    private bool _serverStop = false;
    private TcpListener listener;
    private Queue<MeshTransfer> importQueue = new Queue<MeshTransfer>();

    [System.Serializable]
    public class MeshTransfer
    {
        public string file_format;
        public string path;
        public string name;
        public int frameRate;
    }

    void Start()
    {
        Debug.Log("[Meshy Bridge] Starting");
        _tempCachePath = Application.temporaryCachePath;
        try
        {
            StartServer();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] Error: {e.Message}\n{e.StackTrace}");
        }
    }

    public void StartServer()
    {
        Debug.Log("[Meshy Bridge] Starting server");
        _serverStop = false;
        serverThread = new Thread(RunServer);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    private void GuardJob()
    {
        while (!_serverStop)
        {
            Thread.Sleep(200);
        }

        if (listener != null)
        {
            listener.Stop();
            Debug.Log("[Meshy Bridge] Guard thread shutting down server");
        }
    }

    void RunServer()
    {
        listener = new TcpListener(IPAddress.Any, 5326);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Start();

        guardThread = new Thread(GuardJob);
        guardThread.IsBackground = true;
        guardThread.Start();

        Debug.Log("[Meshy Bridge] Listening on port 5326");

        while (!_serverStop)
        {
            if (listener.Pending())
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    ProcessClientRequest(stream);
                }
            }
            Thread.Sleep(100);
        }

        listener.Stop();
        Debug.Log("[Meshy Bridge] Server stopped");
    }

    public void StopServer()
    {
        _serverStop = true;

        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Join();
        }

        if (guardThread != null && guardThread.IsAlive)
        {
            guardThread.Join();
        }
    }

    private readonly string[] allowedOrigins = new string[]
    {
        "https://www.meshy.ai",
        "https://app-staging.meshy.ai",
        "http://localhost:3700"
    };

    void ProcessClientRequest(NetworkStream stream)
    {
        try
        {
            Debug.Log("[Meshy Bridge] Processing request");
            byte[] buffer = new byte[1024 * 16];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log($"[Meshy Bridge] Received request ({bytesRead} bytes):\n{request}");

            var requestLines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);

            if (requestLines.Length == 0)
            {
                Debug.LogWarning("[Meshy Bridge] Empty request");
                SendErrorResponse(stream, "Empty request");
                return;
            }

            string[] requestParts = requestLines[0].Split(' ');
            if (requestParts.Length < 2)
            {
                Debug.LogWarning("[Meshy Bridge] Invalid request format: " + requestLines[0]);
                SendErrorResponse(stream, "Invalid request format");
                return;
            }

            string method = requestParts[0];
            string path = requestParts[1];
            string origin = GetHeaderValue(requestLines, "Origin");

            if (method == "OPTIONS")
            {
                SendOptionsResponse(stream, origin);
                return;
            }

            if (method == "GET" && (path == "/status" || path == "/ping"))
            {
                SendStatusResponse(stream, origin);
                return;
            }

            if (method == "POST" && path == "/import")
            {
                ProcessImportRequest(stream, request, origin);
                return;
            }

            SendNotFoundResponse(stream, origin);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] Error processing request: {e.GetType().Name} - {e.Message}\n{e.StackTrace}");
            try
            {
                SendErrorResponse(stream, e.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Meshy Bridge] Failed to send error response: {ex.Message}");
            }
        }
    }

    [System.Serializable]
    private class ImportResponseData
    {
        public string status;
        public string message;
        public string path;
    }

    private void ProcessImportRequest(NetworkStream stream, string request, string origin)
    {
        try
        {
            int jsonStart = request.IndexOf('{');
            if (jsonStart < 0)
            {
                Debug.LogWarning("[Meshy Bridge] Missing JSON");
                throw new Exception("Invalid request format: JSON not found");
            }

            string jsonBody = request.Substring(jsonStart);
            if (!jsonBody.Trim().StartsWith("{") || !jsonBody.Trim().EndsWith("}"))
            {
                Debug.LogError("Invalid JSON format");
                throw new Exception("Invalid JSON format");
            }
            Debug.Log($"[Meshy Bridge] JSON: {jsonBody}");

            var data = JsonUtility.FromJson<ImportRequestData>(jsonBody);

            string fileName = $"bridge_model_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{Guid.NewGuid().ToString("N")[..8]}";
            string filePath = Path.Combine(Path.GetTempPath(), "Meshy", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            // 确保文件不存在
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[Meshy Bridge] Deleted existing file: {filePath}");
            }

            Debug.Log($"[Meshy Bridge] Downloading: {data.url}");
            using (var client = new WebClient())
            {
                client.DownloadFile(data.url, filePath);
            }

            string fileExtension = ".glb";
            byte[] header = new byte[4];
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = fs.Read(header, 0, header.Length);
                
                // 根据JSON中声明的format来决定检测逻辑
                if (data.format.ToLower() == "glb")
                {
                    // GLB格式：检查是否为ZIP或GLB
                    if (header[0] == 'P' && header[1] == 'K' && header[2] == 0x03 && header[3] == 0x04)
                    {
                        fileExtension = ".zip";
                    }
                    else if (header[0] == 'g' && header[1] == 'l' && header[2] == 'T' && header[3] == 'F')
                    {
                        fileExtension = ".glb";
                    }
                }
                else if (data.format.ToLower() == "fbx")
                {
                    // FBX格式：检查是否为ZIP或FBX
                    if (header[0] == 'P' && header[1] == 'K' && header[2] == 0x03 && header[3] == 0x04)
                    {
                        fileExtension = ".zip";
                    }
                    else
                    {
                        fileExtension = ".fbx";
                    }
                }
            }

            string finalPath = filePath + fileExtension;

            // 确保目标文件不存在
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
                Debug.Log($"[Meshy Bridge] Deleted existing target file: {finalPath}");
            }

            File.Move(filePath, finalPath);
            filePath = finalPath;
            Debug.Log($"[Meshy Bridge] File saved: {filePath}");

            lock (importQueue)
            {
                importQueue.Enqueue(new MeshTransfer
                {
                    file_format = data.format,
                    path = filePath,
                    name = data.name ?? "",
                    frameRate = data.frameRate
                });
            }

            var responseData = new ImportResponseData
            {
                status = "ok",
                message = "File queued for import",
                path = filePath
            };

            string jsonResponse = JsonUtility.ToJson(responseData);

            string response = string.Join("\r\n",
                $"HTTP/1.1 200 OK",
                $"Access-Control-Allow-Origin: {GetAllowedOrigin(origin)}",
                "Content-Type: application/json; charset=utf-8",
                "Connection: close",
                $"Content-Length: {jsonResponse.Length}",
                "",
                jsonResponse);

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();
            Debug.Log("[Meshy Bridge] Response sent");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] Error processing import request: {e.Message}");
            SendErrorResponse(stream, e.Message);
        }
    }

    [System.Serializable]
    private class ImportRequestData
    {
        public string url;
        public string format;
        public string name;
        public int frameRate = 30;
    }

    private string GetAllowedOrigin(string origin)
    {
        return Array.Exists(allowedOrigins, o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)) ?
            origin : "https://www.meshy.ai";
    }

    private void SendOptionsResponse(NetworkStream stream, string origin)
    {
        string response = $"HTTP/1.1 200 OK\r\n" +
                        $"Access-Control-Allow-Origin: {GetAllowedOrigin(origin)}\r\n" +
                        "Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n" +
                        "Access-Control-Allow-Headers: *\r\n" +
                        "Access-Control-Max-Age: 86400\r\n\r\n";
        stream.Write(Encoding.UTF8.GetBytes(response), 0, response.Length);
    }

    [System.Serializable]
    private class StatusResponseData
    {
        public string status = "ok";
        public string dcc = "unity";
        public string version;
    }

    private void SendStatusResponse(NetworkStream stream, string origin)
    {
        var responseData = new StatusResponseData
        {
            dcc = "unity",
            status = "ok",
            version = Application.unityVersion
        };

        string jsonResponse = JsonUtility.ToJson(responseData);

        string response = string.Join("\r\n",
            "HTTP/1.1 200 OK",
            $"Access-Control-Allow-Origin: {GetAllowedOrigin(origin)}",
            "Content-Type: application/json; charset=utf-8",
            "Connection: close",
            $"Content-Length: {jsonResponse.Length}",
            "",
            jsonResponse);

        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);
        stream.Flush();

        Debug.Log($"[Meshy Bridge] Status response sent: {jsonResponse}");
    }

    private void SendNotFoundResponse(NetworkStream stream, string origin)
    {
        string response = $"HTTP/1.1 404 Not Found\r\n" +
                        $"Access-Control-Allow-Origin: {GetAllowedOrigin(origin)}\r\n" +
                        "Content-Type: application/json\r\n\r\n" +
                        JsonUtility.ToJson(new { status = "path not found" });
        stream.Write(Encoding.UTF8.GetBytes(response), 0, response.Length);
    }

    private void SendErrorResponse(NetworkStream stream, string message)
    {
        try
        {
            string jsonBody = JsonUtility.ToJson(new { status = "error", message });
            string response = string.Join("\r\n",
                "HTTP/1.1 500 Internal Server Error",
                "Access-Control-Allow-Origin: *",
                "Content-Type: application/json; charset=utf-8",
                "Connection: close",
                $"Content-Length: {jsonBody.Length}",
                "",
                jsonBody);

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] Failed to send error response: {e.Message}");
        }
    }

    private string GetHeaderValue(string[] headers, string headerName)
    {
        foreach (var header in headers)
        {
            if (header.StartsWith(headerName + ":"))
                return header.Substring(headerName.Length + 1).Trim();
        }
        return "";
    }

    void Update()
    {
        lock (importQueue)
        {
            while (importQueue.Count > 0)
            {
                var transfer = importQueue.Dequeue();
                ProcessMeshTransfer(transfer);
            }
        }
    }

    private void ProcessMeshTransfer(MeshTransfer transfer)
    {
        try
        {
            string fileExtension = Path.GetExtension(transfer.path)?.ToLower();
            switch (fileExtension)
            {
                case ".glb":
                    ImportModelWithMaterial(transfer);
                    break;
                case ".zip":
                    ProcessZipFile(transfer);
                    break;
                case ".fbx":
                    ImportFBXWithTextures(transfer);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] Error processing mesh: {e.Message}");
        }
        finally
        {
            CleanupTempFile(transfer.path);
        }
    }

    private void ImportModelWithMaterial(MeshTransfer transfer)
    {
        try
        {
            string importDir = "Assets/MeshyImports";
            if (!Directory.Exists(importDir))
            {
                Directory.CreateDirectory(importDir);
                AssetDatabase.Refresh();
            }

            string modelName = string.IsNullOrEmpty(transfer.name) ? "Meshy_Model" : transfer.name;
            modelName = string.Join("_", modelName.Split(Path.GetInvalidFileNameChars()));

            string extension = Path.GetExtension(transfer.path);
            if (string.IsNullOrEmpty(extension))
            {
                extension = $".{transfer.file_format}";
            }

            string uniqueFileName = $"{modelName}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}{extension}";
            string relativePath = Path.Combine(importDir, uniqueFileName);

            if (!File.Exists(transfer.path))
            {
                Debug.LogError($"[Meshy Bridge] Source file not found: {transfer.path}");
                return;
            }

            File.Copy(transfer.path, relativePath, true);

            // 配置ModelImporter以正确导入多个动画
            AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
            
            // 获取ModelImporter并配置动画导入
            ModelImporter importer = AssetImporter.GetAtPath(relativePath) as ModelImporter;
            if (importer != null)
            {
                // 配置动画导入设置
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                
                // 如果GLB包含多个动画，尝试提取所有clips
                if (transfer.file_format.ToLower() == "glb")
                {
                    // 重新导入以应用设置
                    importer.SaveAndReimport();
                    
                    // 检查是否有多个动画clips
                    var clips = AssetDatabase.LoadAllAssetsAtPath(relativePath).OfType<AnimationClip>().ToArray();
                    if (clips.Length > 1)
                    {
                        Debug.Log($"[Meshy Bridge] Found {clips.Length} animation clips in GLB file");
                        foreach (var clip in clips)
                        {
                            Debug.Log($"[Meshy Bridge] Animation clip: {clip.name}");
                        }
                    }
                }
            }

            GameObject importedObject = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
            if (importedObject != null)
            {
                importedObject.name = uniqueFileName;

                AddDefaultMaterial(importedObject);

                EditorUtility.SetDirty(importedObject);
                AssetDatabase.SaveAssets();

                EditorApplication.delayCall += () =>
                {
                    GameObject sceneObject = PrefabUtility.InstantiatePrefab(importedObject) as GameObject;
                    if (sceneObject != null)
                    {
                        sceneObject.transform.position = Vector3.zero;
                        sceneObject.transform.rotation = Quaternion.identity;
                        sceneObject.transform.localScale = Vector3.one;

                        // 如果有动画组件，设置Animator
                        var animator = sceneObject.GetComponent<Animator>();
                        if (animator == null)
                        {
                            animator = sceneObject.AddComponent<Animator>();
                        }

                        // 创建AnimatorController以使用多个动画
                        CreateAnimatorControllerForMultipleClips(sceneObject, relativePath);

                        Selection.activeGameObject = sceneObject;
                        EditorSceneManager.MarkSceneDirty(sceneObject.scene);

                        Debug.Log($"[Meshy Bridge] Model successfully added to scene: {sceneObject.name}");
                    }
                };
            }
            Debug.Log($"[Meshy Bridge] Model imported successfully: {relativePath}, Name: {uniqueFileName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] Model import failed: {e.Message}\n{e.StackTrace}");
        }
    }

    private void AddDefaultMaterial(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterials.Length == 0)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = "Meshy_Material";
            renderer.sharedMaterial = material;
        }
    }

    private void ProcessZipFile(MeshTransfer transfer)
    {
        string extractPath = Path.Combine(_tempCachePath, "extracted");
        ZipFile.ExtractToDirectory(transfer.path, extractPath);

        // 处理GLB文件
        foreach (string file in Directory.GetFiles(extractPath, "*.glb", SearchOption.AllDirectories))
        {
            MeshTransfer newTransfer = new MeshTransfer
            {
                file_format = "glb",
                path = file,
                name = transfer.name
            };
            ImportModelWithMaterial(newTransfer);
        }

        // 处理FBX文件 - 使用专门的FBX导入函数
        foreach (string file in Directory.GetFiles(extractPath, "*.fbx", SearchOption.AllDirectories))
        {
            MeshTransfer newTransfer = new MeshTransfer
            {
                file_format = "fbx",
                path = file,
                name = transfer.name
            };
            ImportFBXWithTextures(newTransfer);
        }

        Directory.Delete(extractPath, true);
    }

    private void ImportFBXWithTextures(MeshTransfer transfer)
    {
        try
        {
            string importDir = "Assets/MeshyImports";
            if (!Directory.Exists(importDir))
            {
                Directory.CreateDirectory(importDir);
                AssetDatabase.Refresh();
            }

            string modelName = string.IsNullOrEmpty(transfer.name) ? "Meshy_Model" : transfer.name;
            modelName = string.Join("_", modelName.Split(Path.GetInvalidFileNameChars()));

            // Create dedicated folder for each FBX model
            string modelFolderName = $"{modelName}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}";
            string modelDir = Path.Combine(importDir, modelFolderName);
            Directory.CreateDirectory(modelDir);

            // Copy FBX file
            string fbxFileName = Path.GetFileName(transfer.path);
            string fbxRelativePath = Path.Combine(modelDir, fbxFileName);
            
            if (!File.Exists(transfer.path))
            {
                Debug.LogError($"[Meshy Bridge] Source FBX file not found: {transfer.path}");
                return;
            }

            File.Copy(transfer.path, fbxRelativePath, true);

            // Find and copy texture files
            string sourceDir = Path.GetDirectoryName(transfer.path);
            ImportTextureFiles(sourceDir, modelDir);

            // Refresh asset database
            AssetDatabase.Refresh();

            // Import FBX file
            AssetDatabase.ImportAsset(fbxRelativePath, ImportAssetOptions.ForceUpdate);

            // Get imported FBX object
            GameObject importedObject = AssetDatabase.LoadAssetAtPath<GameObject>(fbxRelativePath);
            if (importedObject != null)
            {
                importedObject.name = modelName;

                // Check and fix material texture references
                FixMaterialTextureReferences(importedObject, modelDir);

                EditorUtility.SetDirty(importedObject);
                AssetDatabase.SaveAssets();

                // Instantiate in scene
                EditorApplication.delayCall += () =>
                {
                    GameObject sceneObject = PrefabUtility.InstantiatePrefab(importedObject) as GameObject;
                    if (sceneObject != null)
                    {
                        sceneObject.transform.position = Vector3.zero;
                        sceneObject.transform.rotation = Quaternion.identity;
                        sceneObject.transform.localScale = Vector3.one;

                        Selection.activeGameObject = sceneObject;
                        EditorSceneManager.MarkSceneDirty(sceneObject.scene);

                        Debug.Log($"[Meshy Bridge] FBX model successfully added to scene: {sceneObject.name}");
                    }
                };
            }
            
            Debug.Log($"[Meshy Bridge] FBX model imported successfully: {fbxRelativePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Meshy Bridge] FBX import failed: {e.Message}\n{e.StackTrace}");
        }
    }

    private void ImportTextureFiles(string sourceDir, string targetDir)
    {
        // Supported texture file extensions
        string[] textureExtensions = { "*.jpg", "*.jpeg", "*.png", "*.tga", "*.bmp", "*.tiff", "*.tif", "*.exr", "*.hdr" };
        
        // Copy texture files from source directory
        foreach (string pattern in textureExtensions)
        {
            string[] textureFiles = Directory.GetFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly);
            foreach (string textureFile in textureFiles)
            {
                string fileName = Path.GetFileName(textureFile);
                string targetPath = Path.Combine(targetDir, fileName);
                
                File.Copy(textureFile, targetPath, true);
                Debug.Log($"[Meshy Bridge] Copied texture file: {fileName}");
            }
        }

        // Find and copy texture files in subdirectories (e.g., Textures folder)
        string[] subDirectories = Directory.GetDirectories(sourceDir);
        foreach (string subDir in subDirectories)
        {
            string subDirName = Path.GetFileName(subDir);
            string targetSubDir = Path.Combine(targetDir, subDirName);
            
            // Create corresponding subdirectory
            Directory.CreateDirectory(targetSubDir);
            
            // Copy texture files in subdirectory
            foreach (string pattern in textureExtensions)
            {
                string[] textureFiles = Directory.GetFiles(subDir, pattern, SearchOption.AllDirectories);
                foreach (string textureFile in textureFiles)
                {
                    string relativePath = Path.GetRelativePath(subDir, textureFile);
                    string targetPath = Path.Combine(targetSubDir, relativePath);
                    
                    // Ensure target directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    
                    File.Copy(textureFile, targetPath, true);
                    Debug.Log($"[Meshy Bridge] Copied subdirectory texture file: {subDirName}/{relativePath}");
                }
            }
        }
    }

    private void FixMaterialTextureReferences(GameObject fbxObject, string modelDir)
    {
        // Get all Renderer components from FBX object
        Renderer[] renderers = fbxObject.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material != null)
                {
                    // Check material's main texture
                    if (material.mainTexture == null)
                    {
                        // Try to find corresponding texture based on material name
                        string textureName = material.name;
                        Texture2D foundTexture = FindTextureInDirectory(modelDir, textureName);
                        
                        if (foundTexture != null)
                        {
                            material.mainTexture = foundTexture;
                            Debug.Log($"[Meshy Bridge] Set texture for material {material.name}: {foundTexture.name}");
                        }
                        else
                        {
                            // If no corresponding texture found, try to find the first available texture
                            Texture2D firstTexture = FindFirstTextureInDirectory(modelDir);
                            if (firstTexture != null)
                            {
                                material.mainTexture = firstTexture;
                                Debug.Log($"[Meshy Bridge] Set default texture for material {material.name}: {firstTexture.name}");
                            }
                        }
                    }
                    
                    // Check other common texture properties
                    CheckAndAssignTexture(material, "_BumpMap", modelDir, "normal", "Normal");
                    CheckAndAssignTexture(material, "_MetallicGlossMap", modelDir, "metallic", "Metallic");
                    CheckAndAssignTexture(material, "_OcclusionMap", modelDir, "occlusion", "AO");
                    CheckAndAssignTexture(material, "_EmissionMap", modelDir, "emission", "Emissive");
                }
            }
        }
    }

    private void CheckAndAssignTexture(Material material, string propertyName, string modelDir, params string[] nameKeywords)
    {
        if (material.HasProperty(propertyName) && material.GetTexture(propertyName) == null)
        {
            foreach (string keyword in nameKeywords)
            {
                Texture2D texture = FindTextureInDirectory(modelDir, keyword);
                if (texture != null)
                {
                    material.SetTexture(propertyName, texture);
                    Debug.Log($"[Meshy Bridge] Set {propertyName} texture for material {material.name}: {texture.name}");
                    break;
                }
            }
        }
    }

    private Texture2D FindTextureInDirectory(string directory, string nameKeyword)
    {
        string[] textureExtensions = { "*.jpg", "*.jpeg", "*.png", "*.tga", "*.bmp", "*.tiff", "*.tif" };
        
        foreach (string pattern in textureExtensions)
        {
            string[] textureFiles = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            foreach (string textureFile in textureFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(textureFile);
                if (fileName.ToLower().Contains(nameKeyword.ToLower()))
                {
                    string relativePath = textureFile.Replace('\\', '/');
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                }
            }
        }
        
        return null;
    }

    private Texture2D FindFirstTextureInDirectory(string directory)
    {
        string[] textureExtensions = { "*.jpg", "*.jpeg", "*.png", "*.tga", "*.bmp", "*.tiff", "*.tif" };
        
        foreach (string pattern in textureExtensions)
        {
            string[] textureFiles = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
            if (textureFiles.Length > 0)
            {
                string relativePath = textureFiles[0].Replace('\\', '/');
                return AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
            }
        }
        
        return null;
    }

    private void CleanupTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error cleaning up: {e.Message}");
        }
    }

    // 将这个方法移到类内部
    private void CreateAnimatorControllerForMultipleClips(GameObject sceneObject, string modelPath)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToArray();
        if (clips.Length > 1)
        {
            // 创建AnimatorController来管理多个动画
            string controllerPath = modelPath.Replace(Path.GetExtension(modelPath), "_Controller.controller");
            
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            
            // 为每个动画clip创建状态
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                var state = controller.layers[0].stateMachine.AddState(clip.name);
                state.motion = clip;
                
                // 设置第一个动画为默认状态
                if (i == 0)
                {
                    controller.layers[0].stateMachine.defaultState = state;
                }
            }
            
            // 应用controller到animator
            var animator = sceneObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
            }
            
            Debug.Log($"[Meshy Bridge] Created AnimatorController with {clips.Length} animation clips");
        }
    }

    void OnDestroy()
    {
        StopServer();
    }
}