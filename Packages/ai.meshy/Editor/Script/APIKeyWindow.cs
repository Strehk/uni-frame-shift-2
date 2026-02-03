#if UNITY_EDITOR
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
    public class MeshyModel
    {
        public string Id { get; set; }  // 改为 get; set;
        public string Name { get; set; }
        public string Author { get; set; }
        public string ThumbnailUrl { get; set; }
        public string ModelUrl { get; set; }
        public string ThumbnailPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int Downloads { get; set; }  // 改名以匹配My Assets.cs
        public string ResultId { get; set; }
        public string Phase { get; set; }  // 添加Phase属性

        public MeshyModel(string id, string name, string author, string thumbnailUrl, string modelUrl, DateTime createdAt, DateTime updatedAt, int downloadCount, string resultId)
        {
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
            Author = string.IsNullOrWhiteSpace(author) ? "Unknown" : author;
            ThumbnailUrl = thumbnailUrl;
            ModelUrl = modelUrl;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            Downloads = downloadCount;
            ResultId = resultId;
        }

        public void SetThumbnailPath(string path)
        {
            ThumbnailPath = path;
        }
    }

    public enum SearchContext
    {
        prompt,     // 只搜索资产 prompt
        author        // 只搜索名字
    }

    public class MeshyApi : IDisposable
    {
        public readonly HttpClient _httpClient;
        private readonly string _thumbnailDir;

        public MeshyApi(string apiKey)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // 添加 API Key 到请求头
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }
            
            _thumbnailDir = Path.Combine(Application.persistentDataPath, "MeshyThumbnails");
            if (!Directory.Exists(_thumbnailDir))
            {
                Directory.CreateDirectory(_thumbnailDir);
            }
        }

        public async Task<Dictionary<string, MeshyModel>> FetchModelsAsync(int page = 1, 
            int pageSize = 24, string searchQuery = "", string sortBy = "-created_at", SearchContext context = SearchContext.prompt)
        {
  
            // 构建基础URL和参数
            var baseUrl = "https://api.meshy.ai/plugin/v1/showcases";
            var queryParams = new Dictionary<string, string>
            {
                ["pageNum"] = page.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["sortBy"] = sortBy,
                ["source"] = "unity"
            };

            // 根据搜索上下文添加不同的搜索参数
            if (!string.IsNullOrEmpty(searchQuery))
            {
                switch (context)
                {
                    case SearchContext.prompt:
                        queryParams["prompt"] = searchQuery;
                        break;
                    case SearchContext.author:
                        queryParams["author"] = searchQuery;
                        break;
                }
            }

            // 构建完整的URL
            var url = baseUrl + "?" + string.Join("&", queryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                
                // 输出原始JSON以便调试
                Debug.Log($"API Response: {json.Substring(0, Math.Min(500, json.Length))}...");
                
                // 使用JObject解析JSON以便更灵活地处理
                var jsonObj = JObject.Parse(json);
                var resultArray = jsonObj["result"] as JArray;
                
                var models = new Dictionary<string, MeshyModel>();
                
                if (resultArray != null)
                {
                    foreach (JObject modelData in resultArray)
                    {
                        // 使用字符串处理日期，而不是直接解析
                        DateTime createdAt = DateTime.UtcNow; // 默认使用当前时间
                        
                        try
                        {
                            if (modelData["createdAt"] != null)
                            {
                                // 尝试将日期字符串转换为DateTime
                                if (DateTime.TryParse(modelData["createdAt"].ToString(), out var date))
                                {
                                    createdAt = date;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Failed to parse date for model {modelData["id"]}: {ex.Message}");
                        }
                        
                        // 获取模型名称 - 使用类似Python代码的逻辑
                        string displayName = "";
                        
                        // 从name字段获取名称
                        if (modelData["name"] != null && !string.IsNullOrWhiteSpace(modelData["name"].ToString()))
                        {
                            displayName = modelData["name"].ToString();
                        }
                        
                        // 如果名称为空，尝试从draft的prompt中获取
                        if (string.IsNullOrEmpty(displayName))
                        {
                            try
                            {
                                if (modelData["args"] != null && 
                                    modelData["args"]["draft"] != null && 
                                    modelData["args"]["draft"]["prompt"] != null)
                                {
                                    string prompt = modelData["args"]["draft"]["prompt"].ToString();
                                    
                                    // 如果prompt长度超过12个字符，截取前10个字符并添加省略号
                                    if (prompt.Length > 12)
                                    {
                                        displayName = prompt.Substring(0, 10) + "...";
                                    }
                                    else
                                    {
                                        displayName = prompt;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Failed to get prompt for model {modelData["id"]}: {ex.Message}");
                            }
                        }
                        
                        // 如果经过上述处理仍然没有名称，使用"Untitled"作为默认名称
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = "Untitled";
                        }
                        
                        // 调试输出模型数据
                        Debug.Log($"Model data: ID={modelData["id"]}, Processed Name={displayName}, Author={modelData["author"]}");
                        
                        var model = new MeshyModel(
                            modelData["id"].ToString(),
                            displayName,  // 使用处理后的名称
                            modelData["author"]?.ToString() ?? "Unknown",
                            modelData["thumbnailUrl"]?.ToString(),
                            modelData["modelUrl"]?.ToString(),
                            createdAt,
                            createdAt, // 如果没有updatedAt，使用createdAt
                            modelData["downloadCount"] != null ? (int)modelData["downloadCount"] : 0,
                            modelData["resultId"]?.ToString() ?? modelData["id"].ToString()
                        );
                        
                        models[model.Id] = model;
                    }
                }
                
                return models;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fetch models: {ex.Message}");
                return new Dictionary<string, MeshyModel>();
            }
        }

        public async Task DownloadThumbnailAsync(MeshyModel model)
        {
            if (string.IsNullOrEmpty(model.ThumbnailUrl)) return;
            
            var thumbnailPath = Path.Combine(_thumbnailDir, $"{model.Id}.jpeg");
            
            if (File.Exists(thumbnailPath) && new FileInfo(thumbnailPath).Length > 0)
            {
                model.SetThumbnailPath(thumbnailPath);
                return;
            }

            try
            {
                var response = await _httpClient.GetAsync(model.ThumbnailUrl);
                response.EnsureSuccessStatusCode();
                
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(thumbnailPath, imageBytes);
                
                model.SetThumbnailPath(thumbnailPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to download thumbnail: {ex.Message}");
            }
        }

        public async Task LoadThumbnailsAsync(IEnumerable<MeshyModel> models)
        {
            var tasks = new List<Task>();
            foreach (var model in models)
            {
                tasks.Add(DownloadThumbnailAsync(model));
            }
            await Task.WhenAll(tasks);
        }

        public void ClearThumbnailCache()
        {
            try
            {
                if (Directory.Exists(_thumbnailDir))
                {
                    Directory.Delete(_thumbnailDir, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to clear thumbnail cache: {ex.Message}");
            }
        }

    

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    public class ShowcaseResponse
    {
        public List<MeshyModelData> result { get; set; }
    }

    public class MeshyModelData
    {
        public string id { get; set; }
        public string name { get; set; }
        public string author { get; set; }
        public string thumbnailUrl { get; set; }
        public string modelUrl { get; set; }
        public object createdAt { get; set; } // 改为object类型
        public object updatedAt { get; set; } // 改为object类型
        public int downloadCount { get; set; }
    }
    
    // 修正响应类结构
    public class UserInfoResponse
    {
        public UserInfoResult result { get; set; }
    }

    public class UserInfoResult
    {
        public string nickname { get; set; }
        public string email { get; set; }
    }

    public class UserTierResponse
    {
        public UserTierResult result { get; set; }
    }

    public class UserTierResult
    {
        public string tier { get; set; }
    }

    public class UserCreditsResponse
    {
        public UserCreditsResult result { get; set; }
    }

    public class UserCreditsResult
    {
        public int creditBalance { get; set; }
        public int freeCreditBalance { get; set; }
        public int shareCreditEarned { get; set; }
    }

    public class APIKeyWindow : EditorWindow
    {
        static string API_KEY_FIELD = "Meshy API Keys";
        private string APIKey;
        private string userName = "";
        private string userTier = "";
        private int creditsRemaining = 0;
        private Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();
        private Vector2 scrollPosition;
        private string selectedModelId;
        private string searchQuery = "";
        private int pageNum = 1;
        private bool hasNextPage = false;
        private bool isLoading = false;
        private Dictionary<string, MeshyModel> searchResults = new Dictionary<string, MeshyModel>();
        private string sortBy = "-created_at";
        private SearchContext searchContext = SearchContext.prompt; // 默认搜索 prompt
        private MyAssetsManager myAssetsManager;

        private readonly string[] sortOptions = new[]
        {
            "-created_at", 
            "-public_popularity", 
            "-downloads"
        };
        private readonly string[] sortOptionLabels = new[]
        {
            "Newest",
            "Trending", 
            "Most Downloaded"
        };
        private int selectedTab = 0;
        private MeshyApi _meshyApi; 

        [MenuItem("Meshy/Meshy For Unity")]
        public static void ShowMeshyWindow()
        {
            APIKeyWindow wnd = GetWindow<APIKeyWindow>();
            wnd.titleContent = new GUIContent("Meshy For Unity ");
            wnd.titleContent.image = Resources.Load<Texture>("Meshy_Icon_64");
            wnd.APIKey = EditorPrefs.HasKey(API_KEY_FIELD) ? EditorPrefs.GetString(API_KEY_FIELD) : "";
        }

        private void OnGUI()
        {   
            // 创建通用字体样式
            var commonLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 14 };
            var commonBoldStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            var commonButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            var commonTextFieldStyle = new GUIStyle(EditorStyles.textField) { fontSize = 14 };
            var commonPopupStyle = new GUIStyle(EditorStyles.popup) { fontSize = 14 };

            // 设置工具栏样式
            var toolbarStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fixedHeight = 35,  // 增加高度
                margin = new RectOffset(0, 0, 0, 0),  // 移除外边距
                padding = new RectOffset(10, 10, 10, 10),  // 调整内边距
                border = new RectOffset(1, 1, 1, 1),  // 最小化边框
                alignment = TextAnchor.MiddleCenter  // 确保文本居中
            };
            selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "Meshy Login", "Assets Browser", "My Assets" }, toolbarStyle);
            EditorGUILayout.Space(25);
            // 内容区域开始
            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 0, 5)  // 调整上边距为0
            });
            switch (selectedTab)
            {
                case 0:
                    DrawLoginPanel();
                    break;
                case 1:
                    if (!string.IsNullOrEmpty(userTier))
                    {
                        DrawAssetsBrowser();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Please login first to access the Assets Browser.", MessageType.Info);
                    }
                    break;
                case 2:
                    if (!string.IsNullOrEmpty(userTier))
                    {
                        if (myAssetsManager == null)
                        {
                            myAssetsManager = new MyAssetsManager(APIKey);
                        }
                        myAssetsManager.DrawMyAssetsPanel();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Please login first to access your assets.", MessageType.Info);
                    }
                    break;
                
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLoginPanel()
        {
            var commonLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 14 };
            var commonBoldStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            var commonButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            var commonTextFieldStyle = new GUIStyle(EditorStyles.textField) { fontSize = 14,alignment = TextAnchor.MiddleLeft };

            bool isLoggedIn = !string.IsNullOrEmpty(userTier);
            if (!isLoggedIn)
            {
                EditorGUILayout.LabelField("Login to Meshy", commonBoldStyle);
                EditorGUILayout.Space(25);
                EditorGUILayout.LabelField("API Key", commonLabelStyle); 
                APIKey = EditorGUILayout.TextField(APIKey, commonTextFieldStyle,GUILayout.Height(35));
                EditorGUILayout.Space(25);
                if (GUILayout.Button("Login", commonButtonStyle,GUILayout.Height(35)))
                {
                    SaveAPIKey();
                    FetchUserInfo();
                }
                EditorGUILayout.Space(25);
                if (GUILayout.Button("Register", commonButtonStyle,GUILayout.Height(35)))
                {
                    Application.OpenURL("https://www.meshy.ai/");
                }
                EditorGUILayout.Space(25);
            }
            else
            {
                EditorGUILayout.Space(25);
                EditorGUILayout.LabelField($"Logged in as: {userName}", commonBoldStyle);
                EditorGUILayout.Space(25);
                EditorGUILayout.LabelField($"User tier: {userTier}", commonLabelStyle);
                EditorGUILayout.Space(25);
                EditorGUILayout.LabelField($"Credits remaining: {creditsRemaining}", commonLabelStyle);
                EditorGUILayout.Space(25);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh", commonButtonStyle, GUILayout.Height(35)))
                {
                    FetchUserInfo();
                }
                var originalBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(197f/255f, 249f/255f, 85f/255f);
                if (GUILayout.Button("View Usage", commonButtonStyle, GUILayout.Height(35)))
                {
                    Application.OpenURL("https://www.meshy.ai/settings/subscription");
                }
                GUI.backgroundColor = originalBackgroundColor;
                if (GUILayout.Button("Logout", commonButtonStyle, GUILayout.Height(35)))
                {
                    Logout();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawAssetsBrowser()
        {
            
            // 显示加载状态
            if (isLoading)
            {
                EditorGUILayout.HelpBox("Loading models and thumbnails...", MessageType.Info);
            }
            
            // Search and Sort Controls
            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(15, 15, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            });

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            var textFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 14
            };
            // 搜索输入框
            EditorGUILayout.BeginHorizontal(GUILayout.Height(35));
            EditorGUILayout.LabelField("Search:", labelStyle, GUILayout.Width(70));
            searchQuery = EditorGUILayout.TextField(searchQuery, textFieldStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // 搜索类型
            EditorGUILayout.BeginHorizontal(GUILayout.Height(35));
            EditorGUILayout.LabelField("Search by:", labelStyle, GUILayout.Width(70));
            var searchContextRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
            var popupStyle = new GUIStyle(EditorStyles.popup) { fontSize = 14 };
            searchContext = (SearchContext)EditorGUI.EnumPopup(searchContextRect, searchContext, popupStyle);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // 排序方式
            EditorGUILayout.BeginHorizontal(GUILayout.Height(35));
            EditorGUILayout.LabelField("Sort by:", labelStyle, GUILayout.Width(70));
            var sortRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
            int selectedIndex = Array.IndexOf(sortOptions, sortBy);
            selectedIndex = EditorGUI.Popup(sortRect, selectedIndex, sortOptionLabels, popupStyle);
            sortBy = sortOptions[selectedIndex];
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            
            // 搜索按钮
            var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            if (GUILayout.Button("Search", buttonStyle, GUILayout.Height(35)))
            {
                pageNum = 1;
                SearchModels();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
            
            // Pagination Controls
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = pageNum > 1;
            if (GUILayout.Button("Prev Page", GUILayout.Height(35)))
            {
                pageNum--;
                SearchModels();
            }
            GUI.enabled = hasNextPage;
            if (GUILayout.Button("Next Page", GUILayout.Height(35)))
            {
                pageNum++;
                SearchModels();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Model List
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (isLoading)
            {
                EditorGUILayout.HelpBox("Loading models...", MessageType.Info);
            }
            else if (searchResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No models found.", MessageType.Info);
            }
            else
            {
                foreach (var model in searchResults.Values)
                {
                    DrawModelCard(model);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawModelCard(MeshyModel model)
        {
            // 使用更明显的边框样式
            GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
            cardStyle.margin = new RectOffset(5, 5, 5, 5);
            cardStyle.padding = new RectOffset(10, 10, 10, 10);
            
            EditorGUILayout.BeginVertical(cardStyle);
            
            // 缩略图 - 放在最上方
            if (thumbnailCache.ContainsKey(model.Id) && thumbnailCache[model.Id] != null)
            {
                var texture = thumbnailCache[model.Id];
                float aspectRatio = (float)texture.width / texture.height;
                float maxWidth = EditorGUIUtility.currentViewWidth - 40; // 留出边距
                float height = Mathf.Min(150, texture.height);
                float width = Mathf.Min(maxWidth, height * aspectRatio);
                
                // 居中显示缩略图
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(texture, GUILayout.Width(width), GUILayout.Height(height));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // 显示占位符
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("Loading thumbnail...", MessageType.Info, true);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                // 尝试加载缩略图
                if (!string.IsNullOrEmpty(model.ThumbnailPath))
                {
                    LoadThumbnail(model);
                }
            }
            
            // 模型信息区域
            EditorGUILayout.BeginVertical();
            
            // 模型名称图标和名称
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_Prefab Icon"), GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.LabelField($"Model Name: {model.Name}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // 作者信息
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow"), GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.LabelField($"Author: {model.Author}");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            // 按钮区域 - 使用水平布局
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            // 导入按钮 - 使用 "Import" 图标
            if (GUILayout.Button(new GUIContent("Import Model", EditorGUIUtility.IconContent("Import").image), GUILayout.Height(30)))
            {
                ImportModel(model);
            }
            var originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(197f/255f, 249f/255f, 85f/255f);
            // 查看按钮 - 使用 "ViewToolZoom" 图标
            if (GUILayout.Button(new GUIContent("View on Meshy", EditorGUIUtility.IconContent("ViewToolZoom").image), GUILayout.Height(30)))
            {
                ViewOnMeshy(model);
            }
            GUI.backgroundColor = originalBackgroundColor;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            // 添加间隔
            EditorGUILayout.Space(10);
        }

        private async void LoadThumbnail(MeshyModel model)
        {
            if (string.IsNullOrEmpty(model.ThumbnailPath) || !File.Exists(model.ThumbnailPath)) 
            {
                Debug.LogWarning($"Thumbnail path is invalid for model: {model.Name}");
                return;
            }

            try
            {
                // 检查缓存中是否已存在
                if (thumbnailCache.ContainsKey(model.Id) && thumbnailCache[model.Id] != null)
                    return;
                
                // 创建新的纹理并加载图像
                var texture = new Texture2D(2, 2);
                var imageData = await File.ReadAllBytesAsync(model.ThumbnailPath);
                
                if (imageData != null && imageData.Length > 0)
                {
                    texture.LoadImage(imageData);
                    thumbnailCache[model.Id] = texture;
                    Repaint(); // 立即更新UI
                }
                else
                {
                    Debug.LogWarning($"Empty image data for model: {model.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load thumbnail for {model.Name}: {ex.Message}");
            }
        }

        private void SaveAPIKey()
        {
            if (!string.IsNullOrEmpty(APIKey))
            {
                EditorPrefs.SetString(API_KEY_FIELD, APIKey);
            }
        }

        private async void SearchModels(bool showLoadingDialog = true)
        {
            if (string.IsNullOrEmpty(APIKey))
            {
                if (showLoadingDialog)
                {
                    EditorUtility.DisplayDialog("Error", "Please login first.", "OK");
                }
                return;
            }

            isLoading = true;
            Repaint();
            
            try
            {
                if (_meshyApi == null)
                {
                    _meshyApi = new MeshyApi(APIKey);
                }
                
                Debug.Log($"Searching models with query: '{searchQuery}', sort: '{sortBy}', page: {pageNum}, context: {searchContext}");
                searchResults = await _meshyApi.FetchModelsAsync(pageNum, 24, searchQuery, sortBy, searchContext);
                Debug.Log($"Found {searchResults.Count} models");
                
                hasNextPage = searchResults.Count >= 24;

                // 清除旧的缩略图缓存
                ClearThumbnailCache();
                
                // 加载缩略图
                if (searchResults.Count > 0)
                {
                    await _meshyApi.LoadThumbnailsAsync(searchResults.Values);
                    
                    foreach (var model in searchResults.Values)
                    {
                        if (!string.IsNullOrEmpty(model.ThumbnailPath))
                        {
                            LoadThumbnail(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to search models: {ex.Message}");
                if (showLoadingDialog)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to search models: {ex.Message}", "OK");
                }
            }
            finally
            {
                isLoading = false;
                Repaint();
            }
        }

        private void ClearThumbnailCache()
        {
            foreach (var texture in thumbnailCache.Values)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            thumbnailCache.Clear();
        }

        private async void ImportModel(MeshyModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.ModelUrl))
            {
                EditorUtility.DisplayDialog("Error", "Cannot import model: Model URL is empty", "OK");
                return;
            }

            isLoading = true;
            Repaint();

            try
            {
                // 创建临时目录用于存储下载的模型
                string tempDir = Path.Combine(Application.temporaryCachePath, "MeshyModels");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // 处理文件名（防止过长）
                string safeName = model.Name;
                if (safeName.Length > 50)
                {
                    safeName = safeName.Substring(0, 30) + "..." + safeName.Substring(safeName.Length - 15);
                }
                safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));

                // 检查模型URL的文件扩展名
                string fileExtension = ".glb"; // 默认为glb
                Uri uri = new Uri(model.ModelUrl);
                string path = uri.AbsolutePath;
                if (path.Contains("."))
                {
                    fileExtension = Path.GetExtension(path);
                }
                
                // 输出调试信息
                Debug.Log($"Model URL: {model.ModelUrl}");
                Debug.Log($"Detected file extension: {fileExtension}");

                // 设置临时文件路径
                string modelPath = Path.Combine(tempDir, $"{safeName}{fileExtension}");

                // 显示进度条
                EditorUtility.DisplayProgressBar("Meshy", $"Downloading model: {model.Name}", 0.3f);

                // 下载模型文件
                using (var response = await _meshyApi._httpClient.GetAsync(model.ModelUrl))
                {
                    response.EnsureSuccessStatusCode();
                    EditorUtility.DisplayProgressBar("Meshy", $"Downloading model: {model.Name}", 0.6f);
                    
                    var modelBytes = await response.Content.ReadAsByteArrayAsync();
                    
                    // 输出文件大小信息
                    Debug.Log($"Downloaded model file size: {modelBytes.Length} bytes");
                    
                    await File.WriteAllBytesAsync(modelPath, modelBytes);
                    
                    EditorUtility.DisplayProgressBar("Meshy", $"Importing model: {model.Name}", 0.9f);

                    // 确保路径是相对于Assets文件夹的
                    string assetsPath = "Assets/MeshyModels";
                    if (!Directory.Exists(assetsPath))
                    {
                        Directory.CreateDirectory(assetsPath);
                    }

                    string destPath = Path.Combine(assetsPath, $"{safeName}{fileExtension}");
                    
                    // 复制文件到Assets目录
                    File.Copy(modelPath, destPath, true);
                    
                    // 输出目标路径信息
                    Debug.Log($"Model copied to: {destPath}");
                    
                    // 刷新资源数据库以识别新文件
                    AssetDatabase.Refresh();
                    
                    // 在主线程中执行导入操作
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            // 加载导入的模型
                            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
                            Debug.Log($"Load asset result: {(modelAsset != null ? "success" : "failed")}");
                            
                            if (modelAsset != null)
                            {
                                // 实例化模型到场景
                                var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                                Debug.Log($"Instantiation result: {(modelInstance != null ? "success" : "failed")}");
                                
                                if (modelInstance != null)
                                {
                                    // 设置模型名称
                                    modelInstance.name = model.Name;
                                    
                                    // 选中新创建的模型
                                    Selection.activeGameObject = modelInstance;
                                    
                                    // 聚焦到模型
                                    SceneView.lastActiveSceneView?.FrameSelected();
                                    
                                    EditorUtility.DisplayDialog("Success", $"Model '{model.Name}' has been successfully imported into the scene", "OK");
                                }
                                else
                                {
                                    // 尝试直接创建一个新的GameObject并添加模型作为子对象
                                    var container = new GameObject(model.Name);
                                    var instance = GameObject.Instantiate(modelAsset);
                                    instance.transform.SetParent(container.transform);
                                    
                                    Selection.activeGameObject = container;
                                    SceneView.lastActiveSceneView?.FrameSelected();
                                    
                                    EditorUtility.DisplayDialog("Partial Success", "Model has been imported but instantiated using fallback method", "OK");
                                }
                            }
                            else
                            {
                                // 尝试使用其他方法导入
                                if (fileExtension.ToLower() == ".glb")
                                {
                                    EditorUtility.DisplayDialog("Warning", "GLB format requires GLTF Importer plugin. Please install 'glTF for Unity' from Package Manager.", "OK");
                                }
                                else
                                {
                                    EditorUtility.DisplayDialog("Warning", $"Model downloaded but failed to load asset. File format: {fileExtension}", "OK");
                                }
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
                            Repaint();
                        }
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.LogError($"Failed to download model: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to download model: {ex.Message}", "OK");
                EditorUtility.ClearProgressBar();
                isLoading = false;
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error occurred while importing model: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Error occurred while importing model: {ex.Message}", "OK");
                EditorUtility.ClearProgressBar();
                isLoading = false;
                Repaint();
            }
        }

        

        private async void FetchUserInfo()
        {
            if (string.IsNullOrEmpty(APIKey))
            {
                Debug.LogWarning("API Key is empty");
                return;
            }

            isLoading = true;
            Repaint();
            
            try
            {
                // 创建新的HttpClient而不是使用_meshyApi
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", APIKey);
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    // 获取用户基本信息
                    var infoResponse = await client.GetAsync("https://api.meshy.ai/web/v1/me/info");
                    infoResponse.EnsureSuccessStatusCode();
                    var infoJson = await infoResponse.Content.ReadAsStringAsync();
                    var info = JsonConvert.DeserializeObject<UserInfoResponse>(infoJson);
                    userName = info.result.nickname;
                    
                    // 获取用户等级
                    var tierResponse = await client.GetAsync("https://api.meshy.ai/web/v1/me/tier");
                    tierResponse.EnsureSuccessStatusCode();
                    var tierJson = await tierResponse.Content.ReadAsStringAsync();
                    var tier = JsonConvert.DeserializeObject<UserTierResponse>(tierJson);
                    userTier = tier.result.tier;
                    
                    // 获取用户积分
                    var creditsResponse = await client.GetAsync("https://api.meshy.ai/web/v1/me/credits");
                    creditsResponse.EnsureSuccessStatusCode();
                    var creditsJson = await creditsResponse.Content.ReadAsStringAsync();
                    var credits = JsonConvert.DeserializeObject<UserCreditsResponse>(creditsJson);
                    creditsRemaining = credits.result.creditBalance
                        + credits.result.freeCreditBalance
                        + credits.result.shareCreditEarned;
                    
                    // 创建MeshyApi实例用于后续操作
                    _meshyApi = new MeshyApi(APIKey);
                    
                    // 登录成功后自动搜索模型
                    searchQuery = ""; 
                    pageNum = 1;     
                    SearchModels(false);   
                }
                
                Repaint();
            }
            catch (HttpRequestException ex)
            {
                Debug.LogError($"HTTP request failed: {ex.Message}");
                if (ex.Message.Contains("401"))
                {
                    Debug.LogWarning("Invalid API Key");
                    EditorUtility.DisplayDialog("Error", "Invalid API Key. Please check your API key and try again.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to connect to Meshy servers. Please check your internet connection.", "OK");
                }
                Logout(); // 登录失败时登出
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fetch user info: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"An error occurred: {ex.Message}", "OK");
                Logout();
            }
            finally
            {
                isLoading = false;
                Repaint();
            }
        }

        private void Logout()
        {
            APIKey = "";
            userName = "";
            userTier = "";
            creditsRemaining = 0;
            EditorPrefs.DeleteKey(API_KEY_FIELD);
            
            // 清理资源
            if (_meshyApi != null)
            {
                _meshyApi.Dispose();
                _meshyApi = null;
            }
            
            // 清理搜索结果和缩略图
            searchResults.Clear();
            ClearThumbnailCache();
            
            Repaint();
        }

        private void ViewOnMeshy(MeshyModel model)
        {
            if (!string.IsNullOrEmpty(model.Id))
            {
                Application.OpenURL($"https://www.meshy.ai/3d-models/{model.ResultId}");
            }
            else
            {
                Debug.LogWarning("Model ID is empty, cannot open URL");
            }
        }

        private void OnEnable()
        {
            // 如果已经有API Key，尝试自动登录
            if (!string.IsNullOrEmpty(APIKey) && string.IsNullOrEmpty(userTier))
            {
                FetchUserInfo();
                myAssetsManager = new MyAssetsManager(APIKey);      
            }
        }

        private void OnDisable()
        {
            // 清理资源
            ClearThumbnailCache();
            if (_meshyApi != null)
            {
                _meshyApi.Dispose();
                _meshyApi = null;
            }
        }
    }
}
#endif