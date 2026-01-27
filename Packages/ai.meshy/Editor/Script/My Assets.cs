using UnityEditor;
using UnityEngine;
using System.Net.Http;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Meshy
{
    public class MyAssetsManager
    {
        private Dictionary<string, MeshyModel> myAssets = new Dictionary<string, MeshyModel>();
        private Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();
        private readonly string thumbnailDir;
        private int currentPage = 1;
        private bool hasNextPage = false;
        private bool isLoading = false;
        private MeshyApi meshyApi;
        private Vector2 scrollPosition;

        public MyAssetsManager(string apiKey)
        {
            meshyApi = new MeshyApi(apiKey);
            thumbnailDir = Path.Combine(Application.temporaryCachePath, "MeshyMyAssets");
            if (!Directory.Exists(thumbnailDir))
            {
                Directory.CreateDirectory(thumbnailDir);
            }
        }

        public async Task FetchMyAssetsAsync()
        {
            if (isLoading) return;
            isLoading = true;

            try
            {
                var url = $"https://api.meshy.ai/web/v2/tasks";
                var queryParams = new Dictionary<string, string>
                {
                    {"pageNum", currentPage.ToString()},
                    {"pageSize", "16"},
                    {"sortBy", "-created_at"},
                    {"phases", "texture"},
                    {"source", "unity"}
                };

                var response = await meshyApi._httpClient.GetAsync(BuildUrl(url, queryParams));
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();
                var jsonObj = JObject.Parse(jsonContent);
                var resultArray = jsonObj["result"] as JArray;

                myAssets.Clear();
                ClearThumbnailCache();

                if (resultArray != null)
                {
                    foreach (JObject modelData in resultArray)
                    {
                        var model = ParseModelFromJson(modelData);
                        if (model != null)
                        {
                            myAssets[model.Id] = model;
                            await DownloadThumbnailAsync(model);
                        }
                    }

                    hasNextPage = resultArray.Count >= 16;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fetch assets: {ex.Message}");
            }
            finally
            {
                isLoading = false;
            }
        }

        private string BuildUrl(string baseUrl, Dictionary<string, string> parameters)
        {
            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            return $"{baseUrl}?{queryString}";
        }

        private MeshyModel ParseModelFromJson(JObject modelData)
        {
            try
            {
                string id = modelData["id"]?.ToString();
                string displayName = "";
                string taskType = "asset";
                string modelUrl = null;
                string thumbnailUrl = null;

                // 从多个来源尝试获取名称
                if (modelData["name"] != null && !string.IsNullOrWhiteSpace(modelData["name"].ToString()))
                {
                    displayName = modelData["name"].ToString();
                }
                else if (modelData["args"] != null &&
                    modelData["args"]["draft"] != null &&
                    modelData["args"]["draft"]["prompt"] != null)
                {
                    displayName = modelData["args"]["draft"]["prompt"].ToString();
                }
                else if (modelData["args"] != null &&
                    modelData["args"]["texture"] != null &&
                    modelData["args"]["texture"]["prompt"] != null)
                {
                    displayName = modelData["args"]["texture"]["prompt"].ToString();
                }

                // 如果没有找到名称，使用任务类型和ID
                if (string.IsNullOrEmpty(displayName))
                {
                    if (modelData["taskType"] != null)
                    {
                        taskType = modelData["taskType"].ToString();
                    }
                    else if (modelData["args"] != null)
                    {
                        var args = modelData["args"];
                        if (args["texture"] != null) taskType = "texture";
                        else if (args["stylize"] != null) taskType = "stylize";
                        else if (args["taskType"] != null) taskType = args["taskType"].ToString();
                    }
                    displayName = $"{taskType.Substring(0, 1).ToUpper()}{taskType.Substring(1)}_{id.Substring(0, 8)}";
                }

                // 截断过长的名称
                if (displayName.Length > 30)
                {
                    displayName = displayName.Substring(0, 27) + "...";
                }

                // 尝试从result字段获取缩略图和模型URL
                if (modelData["result"] != null && modelData["result"].Type != JTokenType.Null)
                {
                    var result = modelData["result"] as JObject;

                    // 先尝试获取缩略图URL
                    if (result["previewUrl"] != null && !string.IsNullOrEmpty(result["previewUrl"].ToString()))
                    {
                        thumbnailUrl = result["previewUrl"].ToString();
                        Debug.Log($"Got thumbnail URL from result->previewUrl: {thumbnailUrl}");
                    }
                    else if (result["videoUrl"] != null && !string.IsNullOrEmpty(result["videoUrl"].ToString()))
                    {
                        thumbnailUrl = result["videoUrl"].ToString();
                        Debug.Log($"Got thumbnail URL from result->videoUrl: {thumbnailUrl}");
                    }

                    // Check texture field
                    if (result["texture"] != null && result["texture"].Type != JTokenType.Null)
                    {
                        var textureData = result["texture"] as JObject;
                        taskType = "texture";

                        if (textureData != null && textureData["modelUrl"] != null)
                        {
                            modelUrl = textureData["modelUrl"].ToString();
                            Debug.Log($"Got model URL from result->texture->modelUrl: {modelUrl}");
                        }
                    }

                    // 检查stylize字段
                    else if (result["stylize"] != null && result["stylize"].Type != JTokenType.Null)
                    {
                        var stylizeData = result["stylize"] as JObject;
                        taskType = "stylize";

                        if (stylizeData != null && stylizeData["modelUrl"] != null)
                        {
                            modelUrl = stylizeData["modelUrl"].ToString();
                            Debug.Log($"Got model URL from result->stylize->modelUrl: {modelUrl}");
                        }
                    }

                    // Get modelUrl directly from result
                    else if (result["modelUrl"] != null)
                    {
                        modelUrl = result["modelUrl"].ToString();
                        Debug.Log($"Got model URL from result->modelUrl: {modelUrl}");
                    }
                    else if (result["model_url"] != null)
                    {
                        modelUrl = result["model_url"].ToString();
                        Debug.Log($"Got model URL from result->model_url: {modelUrl}");
                    }
                }

                // 如果没有从result获取到，尝试从args获取
                if (string.IsNullOrEmpty(modelUrl) && modelData["args"] != null && modelData["args"].Type != JTokenType.Null)
                {
                    var args = modelData["args"] as JObject;

                    if (args != null)
                    {
                        // Check texture field
                        if (args["texture"] != null && args["texture"].Type != JTokenType.Null)
                        {
                            var textureData = args["texture"] as JObject;
                            taskType = "texture";

                            if (textureData != null && textureData["modelUrl"] != null)
                            {
                                modelUrl = textureData["modelUrl"].ToString();
                                Debug.Log($"Got model URL from args->texture->modelUrl: {modelUrl}");
                            }
                        }

                        // Check stylize field
                        else if (args["stylize"] != null && args["stylize"].Type != JTokenType.Null)
                        {
                            var stylizeData = args["stylize"] as JObject;
                            taskType = "stylize";

                            if (stylizeData != null && stylizeData["modelUrl"] != null)
                            {
                                modelUrl = stylizeData["modelUrl"].ToString();
                                Debug.Log($"Got model URL from args->stylize->modelUrl: {modelUrl}");
                            }
                        }
                    }
                }

                // Try to get from root node
                if (string.IsNullOrEmpty(modelUrl))
                {
                    if (modelData["model_url"] != null && !string.IsNullOrEmpty(modelData["model_url"].ToString()))
                    {
                        modelUrl = modelData["model_url"].ToString();
                        Debug.Log($"Got model URL from root model_url: {modelUrl}");
                    }
                    else if (modelData["modelUrl"] != null && !string.IsNullOrEmpty(modelData["modelUrl"].ToString()))
                    {
                        modelUrl = modelData["modelUrl"].ToString();
                        Debug.Log($"Got model URL from root modelUrl: {modelUrl}");
                    }
                }

                // If thumbnail URL is still empty, try to get from root node
                if (string.IsNullOrEmpty(thumbnailUrl))
                {
                    if (modelData["thumbnail_url"] != null && !string.IsNullOrEmpty(modelData["thumbnail_url"].ToString()))
                    {
                        thumbnailUrl = modelData["thumbnail_url"].ToString();
                        Debug.Log($"Got thumbnail URL from root thumbnail_url: {thumbnailUrl}");
                    }
                    else if (modelData["thumbnailUrl"] != null && !string.IsNullOrEmpty(modelData["thumbnailUrl"].ToString()))
                    {
                        thumbnailUrl = modelData["thumbnailUrl"].ToString();
                        Debug.Log($"Got thumbnail URL from root thumbnailUrl: {thumbnailUrl}");
                    }
                }

                // If model URL is still empty, try to build download URL
                if (string.IsNullOrEmpty(modelUrl) && !string.IsNullOrEmpty(id))
                {
                    modelUrl = $"https://api.meshy.ai/web/v2/tasks/{id}/download?type=model";
                    Debug.Log($"Building download URL: {modelUrl}");
                }

                DateTime createdAt = DateTime.Parse(modelData["created_at"]?.ToString() ?? DateTime.Now.ToString());

                var model = new MeshyModel(
                    id,
                    displayName,
                    modelData["author"]?.ToString() ?? "Unknown",
                    thumbnailUrl,
                    modelUrl,
                    createdAt,
                    createdAt,
                    modelData["downloads"] != null ? Convert.ToInt32(modelData["downloads"]) : 0,
                    modelData["result_id"]?.ToString() ?? id
                );

                model.Phase = modelData["phase"]?.ToString() ?? "unknown";

                // 调试输出
                Debug.Log($"Parsed model: ID={id}, Name={displayName}, ThumbnailURL={thumbnailUrl}, ModelURL={modelUrl}");

                return model;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse model data: {ex.Message}");
                return null;
            }
        }

        private async Task DownloadThumbnailAsync(MeshyModel model)
        {
            if (string.IsNullOrEmpty(model.ThumbnailUrl))
            {
                Debug.LogWarning($"Model {model.Name} has no thumbnail URL");
                return;
            }

            var thumbnailPath = Path.Combine(thumbnailDir, $"{model.Id}.jpeg");

            // 如果文件已存在，直接使用
            if (File.Exists(thumbnailPath) && new FileInfo(thumbnailPath).Length > 0)
            {
                model.ThumbnailPath = thumbnailPath;
                LoadThumbnail(model);
                return;
            }

            try
            {
                Debug.Log($"Downloading thumbnail: {model.ThumbnailUrl}");
                var response = await meshyApi._httpClient.GetAsync(model.ThumbnailUrl);
                if (response.IsSuccessStatusCode)
                {
                    var imageData = await response.Content.ReadAsByteArrayAsync();
                    if (imageData != null && imageData.Length > 0)
                    {
                        await File.WriteAllBytesAsync(thumbnailPath, imageData);
                        model.ThumbnailPath = thumbnailPath;
                        LoadThumbnail(model);
                        Debug.Log($"Thumbnail downloaded successfully: {thumbnailPath}, size: {imageData.Length} bytes");
                    }
                    else
                    {
                        Debug.LogWarning($"Downloaded thumbnail data is empty: {model.Name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to download thumbnail: {response.StatusCode} - {model.ThumbnailUrl}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to download thumbnail: {ex.Message} - {model.ThumbnailUrl}");
            }
        }

        private void LoadThumbnail(MeshyModel model)
        {
            if (string.IsNullOrEmpty(model.ThumbnailPath) || !File.Exists(model.ThumbnailPath))
            {
                Debug.LogWarning($"Invalid thumbnail path: {model.Name} - {model.ThumbnailPath}");
                return;
            }

            try
            {
                if (thumbnailCache.ContainsKey(model.Id) && thumbnailCache[model.Id] != null)
                {
                    return;
                }

                var texture = new Texture2D(2, 2);
                var imageData = File.ReadAllBytes(model.ThumbnailPath);

                if (imageData != null && imageData.Length > 0)
                {
                    bool success = texture.LoadImage(imageData);
                    if (success)
                    {
                        thumbnailCache[model.Id] = texture;
                        Debug.Log($"Thumbnail loaded successfully: {model.Name}, size: {texture.width}x{texture.height}");
                        EditorWindow.GetWindow<APIKeyWindow>().Repaint();
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load thumbnail: {model.Name}");
                        UnityEngine.Object.Destroy(texture);
                    }
                }
                else
                {
                    Debug.LogWarning($"Thumbnail data is empty: {model.Name}");
                    UnityEngine.Object.Destroy(texture);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load thumbnail: {model.Name} - {ex.Message}");
            }
        }

        private async void ImportModel(MeshyModel model)
        {
            if (model == null)
            {
                EditorUtility.DisplayDialog("Error", "Cannot import model: Model is null", "OK");
                return;
            }

            if (string.IsNullOrEmpty(model.ModelUrl))
            {
                // 尝试构建下载URL
                string downloadUrl = $"https://api.meshy.ai/web/v2/tasks/{model.Id}/download?type=model";
                Debug.Log($"Model URL is empty, trying to use: {downloadUrl}");
                model.ModelUrl = downloadUrl;
            }

            if (string.IsNullOrEmpty(model.ModelUrl))
            {
                EditorUtility.DisplayDialog("Error", "Cannot import model: Model URL is empty", "OK");
                return;
            }

            isLoading = true;
            EditorUtility.DisplayProgressBar("Meshy", $"Downloading model: {model.Name}", 0.3f);

            try
            {
                string tempDir = Path.Combine(Application.temporaryCachePath, "MeshyModels");
                Directory.CreateDirectory(tempDir);

                string safeName = model.Name;
                if (safeName.Length > 50)
                {
                    safeName = safeName.Substring(0, 30) + "..." + safeName.Substring(safeName.Length - 15);
                }
                safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));

                string fileExtension = Path.GetExtension(new Uri(model.ModelUrl).AbsolutePath);
                if (string.IsNullOrEmpty(fileExtension)) fileExtension = ".glb";

                string modelPath = Path.Combine(tempDir, $"{safeName}{fileExtension}");
                string assetsPath = "Assets/MeshyModels";
                string destPath = Path.Combine(assetsPath, $"{safeName}{fileExtension}");

                using (var response = await meshyApi._httpClient.GetAsync(model.ModelUrl))
                {
                    response.EnsureSuccessStatusCode();
                    EditorUtility.DisplayProgressBar("Meshy", $"Downloading model: {model.Name}", 0.6f);

                    var modelBytes = await response.Content.ReadAsByteArrayAsync();
                    Directory.CreateDirectory(assetsPath);
                    await File.WriteAllBytesAsync(modelPath, modelBytes);
                    File.Copy(modelPath, destPath, true);

                    EditorUtility.DisplayProgressBar("Meshy", $"Importing model: {model.Name}", 0.9f);
                    AssetDatabase.Refresh();

                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
                            if (modelAsset != null)
                            {
                                var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                                if (modelInstance != null)
                                {
                                    modelInstance.name = model.Name;
                                    Selection.activeGameObject = modelInstance;
                                    SceneView.lastActiveSceneView?.FrameSelected();
                                    EditorUtility.DisplayDialog("Success", $"Model '{model.Name}' has been successfully imported into the scene", "OK");
                                }
                            }
                            else if (fileExtension.ToLower() == ".glb")
                            {
                                EditorUtility.DisplayDialog("Warning", "GLB format requires GLTF Importer plugin. Please install 'glTF for Unity' from Package Manager.", "OK");
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Warning", $"Model downloaded but failed to load asset. File format: {fileExtension}", "OK");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error importing model: {ex.Message}");
                            EditorUtility.DisplayDialog("Error", $"Error importing model: {ex.Message}", "OK");
                        }
                        finally
                        {
                            EditorUtility.ClearProgressBar();
                            isLoading = false;
                            EditorWindow.GetWindow<APIKeyWindow>().Repaint();
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error importing model: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Error importing model: {ex.Message}", "OK");
                EditorUtility.ClearProgressBar();
                isLoading = false;
                EditorWindow.GetWindow<APIKeyWindow>().Repaint();
            }
        }
        // 将这两个方法移到类的内部
        private void ClearThumbnailCache()
        {
            foreach (var texture in thumbnailCache.Values)
            {
                UnityEngine.Object.Destroy(texture);
            }
            thumbnailCache.Clear();
        }

        public void DrawMyAssetsPanel()
        {
            // 创建通用字体样式
            var commonLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 14 };
            var commonBoldStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            var commonButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            // 显示加载状态
            if (isLoading)
            {
                EditorGUILayout.HelpBox("Loading your assets...", MessageType.Info);
            }

            // 刷新按钮
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Refresh Assets", EditorGUIUtility.IconContent("Refresh").image),
                commonButtonStyle, GUILayout.Height(35), GUILayout.ExpandWidth(true)))
            {
                currentPage = 1;
                FetchMyAssetsAsync().ConfigureAwait(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 分页控制
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = currentPage > 1;
            if (GUILayout.Button("Prev Page", commonButtonStyle, GUILayout.Height(35)))
            {
                currentPage--;
                FetchMyAssetsAsync().ConfigureAwait(false);
            }
            GUI.enabled = hasNextPage;
            if (GUILayout.Button("Next Page", commonButtonStyle, GUILayout.Height(35)))
            {
                currentPage++;
                FetchMyAssetsAsync().ConfigureAwait(false);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            // 资产列表
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (isLoading)
            {
                EditorGUILayout.HelpBox("Loading assets...", MessageType.Info);
            }
            else if (myAssets.Count == 0)
            {
                EditorGUILayout.HelpBox("No assets found.", MessageType.Info);
            }
            else
            {
                foreach (var model in myAssets.Values)
                {
                    DrawAssetCard(model);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // 添加缺失的DrawAssetCard方法
        private void DrawAssetCard(MeshyModel model)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 缩略图
            if (thumbnailCache.ContainsKey(model.Id))
            {
                var texture = thumbnailCache[model.Id];
                if (texture != null)
                {
                    float aspectRatio = (float)texture.width / texture.height;
                    float maxWidth = EditorGUIUtility.currentViewWidth - 40;
                    float height = 150;
                    float width = height * aspectRatio;
                    width = Mathf.Min(width, maxWidth);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    var rect = GUILayoutUtility.GetRect(width, height);
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            else if (!string.IsNullOrEmpty(model.ThumbnailPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("Loading thumbnail...", MessageType.Info, true);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                LoadThumbnail(model);
            }

            // Prompt信息
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_SceneViewTools"), GUILayout.Width(20));
            EditorGUILayout.LabelField("Prompt:", GUILayout.Width(50));
            EditorGUILayout.LabelField(model.Name, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();

            // 创建时间
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_SceneViewTools"), GUILayout.Width(20));
            EditorGUILayout.LabelField("Created:", GUILayout.Width(50));
            EditorGUILayout.LabelField(model.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            EditorGUILayout.EndHorizontal();

            // 导入按钮
            if (GUILayout.Button(new GUIContent("Import Model", EditorGUIUtility.IconContent("Import").image),
                GUILayout.Height(30)))
            {
                ImportModel(model);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
