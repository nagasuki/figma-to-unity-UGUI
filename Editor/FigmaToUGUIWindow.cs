using System;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace FigmaToUGUI.Editor
{
    public sealed class FigmaToUGUIWindow : EditorWindow
    {
        private const string TokenPrefsKey = "FigmaToUGUI.AccessToken";
        private const string FilePrefsKey = "FigmaToUGUI.File";
        private const string NodePrefsKey = "FigmaToUGUI.Node";
        private const string OutputPrefsKey = "FigmaToUGUI.Output";
        private const string ProfilePrefsKey = "FigmaToUGUI.Profile";
        private const string UseTextMeshProPrefsKey = "FigmaToUGUI.UseTextMeshPro";
        private const string ApplyAutoLayoutPrefsKey = "FigmaToUGUI.ApplyAutoLayout";
        private const string ApplyConstraintsPrefsKey = "FigmaToUGUI.ApplyConstraints";
        private const string ReplaceExistingPrefsKey = "FigmaToUGUI.ReplaceExisting";
        private const string AddMetadataPrefsKey = "FigmaToUGUI.AddMetadata";

        private string accessToken;
        private string fileInput;
        private string nodeId;
        private string outputFolder;
        private FigmaToUGUIProfile profile;
        private float imageScale = 2f;
        private bool renderUnsupportedNodesAsImages = true;
        private bool addCanvasIfNeeded = true;
        private bool useTextMeshPro;
        private bool applyAutoLayout = true;
        private bool applyConstraints = true;
        private bool replaceExistingImport = true;
        private bool addImportMetadata = true;
        private bool isImporting;
        private CancellationTokenSource cancellationTokenSource;
        private string status;
        private Vector2 scroll;

        [MenuItem("Tools/Figma/Import to UGUI")]
        public static void Open()
        {
            GetWindow<FigmaToUGUIWindow>("Figma to UGUI");
        }

        private void OnEnable()
        {
            accessToken = EditorPrefs.GetString(TokenPrefsKey, string.Empty);
            fileInput = EditorPrefs.GetString(FilePrefsKey, string.Empty);
            nodeId = EditorPrefs.GetString(NodePrefsKey, string.Empty);
            outputFolder = EditorPrefs.GetString(OutputPrefsKey, "Assets/FigmaToUGUI/Generated");
            useTextMeshPro = EditorPrefs.GetBool(UseTextMeshProPrefsKey, false);
            applyAutoLayout = EditorPrefs.GetBool(ApplyAutoLayoutPrefsKey, true);
            applyConstraints = EditorPrefs.GetBool(ApplyConstraintsPrefsKey, true);
            replaceExistingImport = EditorPrefs.GetBool(ReplaceExistingPrefsKey, true);
            addImportMetadata = EditorPrefs.GetBool(AddMetadataPrefsKey, true);
            string profileGuid = EditorPrefs.GetString(ProfilePrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(profileGuid))
            {
                string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                profile = AssetDatabase.LoadAssetAtPath<FigmaToUGUIProfile>(profilePath);
            }
        }

        private void OnDisable()
        {
            CancelImport();
            EditorUtility.ClearProgressBar();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            using (new EditorGUI.DisabledScope(isImporting))
            {
                EditorGUILayout.LabelField("Figma Source", EditorStyles.boldLabel);
                accessToken = EditorGUILayout.PasswordField("Access Token", accessToken);
                fileInput = EditorGUILayout.TextField("File URL or Key", fileInput);
                nodeId = EditorGUILayout.TextField("Node ID (optional)", nodeId);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Unity Output", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                    if (GUILayout.Button("Pick", GUILayout.Width(64f)))
                    {
                        PickOutputFolder();
                    }
                }

                imageScale = EditorGUILayout.Slider("Rendered Image Scale", imageScale, 0.25f, 4f);
                renderUnsupportedNodesAsImages = EditorGUILayout.ToggleLeft("Render images, vectors, and complex shapes as sprites", renderUnsupportedNodesAsImages);
                addCanvasIfNeeded = EditorGUILayout.ToggleLeft("Create or use a Canvas when no parent is selected", addCanvasIfNeeded);
                replaceExistingImport = EditorGUILayout.ToggleLeft("Replace previous import from the same Figma node", replaceExistingImport);
                addImportMetadata = EditorGUILayout.ToggleLeft("Add Figma import metadata to generated objects", addImportMetadata);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Mapping Profile", EditorStyles.boldLabel);
                profile = (FigmaToUGUIProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(FigmaToUGUIProfile), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    useTextMeshPro = EditorGUILayout.ToggleLeft("Use TextMeshPro when available", useTextMeshPro);
                    if (GUILayout.Button("Create Profile", GUILayout.Width(104f)))
                    {
                        CreateProfileAsset();
                    }
                }

                applyAutoLayout = EditorGUILayout.ToggleLeft("Convert Figma Auto Layout to Unity Layout Groups", applyAutoLayout);
                applyConstraints = EditorGUILayout.ToggleLeft("Convert Figma Constraints to RectTransform anchors", applyConstraints);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Select an existing RectTransform before importing to place the Figma UI under it. A profile can map Figma font families, layer names to prefabs, and layer names to nine-slice sprite borders.",
                MessageType.Info);

            if (isImporting)
            {
                EditorGUILayout.HelpBox("Import is running in Unity's progress window. Use that window's Cancel button to stop it.", MessageType.None);
            }
            else
            {
                if (GUILayout.Button("Import Figma UI", GUILayout.Height(36f)))
                {
                    _ = ImportAsync();
                }
            }

            DrawStatus();

            EditorGUILayout.EndScrollView();
        }

        private async Task ImportAsync()
        {
            string fileKey = ExtractFileKey(fileInput);
            string normalizedNodeId = NormalizeNodeId(nodeId);
            if (string.IsNullOrEmpty(normalizedNodeId))
            {
                normalizedNodeId = NormalizeNodeId(ExtractNodeId(fileInput));
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                status = "Please enter a Figma personal access token.";
                Repaint();
                return;
            }

            if (string.IsNullOrEmpty(fileKey))
            {
                status = "Please enter a Figma file URL or file key.";
                Repaint();
                return;
            }

            SavePrefs();

            isImporting = true;
            cancellationTokenSource = new CancellationTokenSource();
            status = "Starting import...";
            SetProgress(new FigmaImportProgress("Starting import", "Preparing request...", 0f));
            Repaint();

            try
            {
                FigmaImportSettings settings = new FigmaImportSettings
                {
                    fileKey = fileKey,
                    nodeId = normalizedNodeId,
                    outputFolder = outputFolder,
                    imageScale = imageScale,
                    renderUnsupportedNodesAsImages = renderUnsupportedNodesAsImages,
                    addCanvasIfNeeded = addCanvasIfNeeded,
                    useTextMeshPro = useTextMeshPro,
                    applyAutoLayout = applyAutoLayout && (profile == null || profile.applyAutoLayout),
                    applyConstraints = applyConstraints && (profile == null || profile.applyConstraints),
                    replaceExistingImport = replaceExistingImport && (profile == null || profile.replaceExistingImport),
                    addImportMetadata = addImportMetadata && (profile == null || profile.addImportMetadata),
                    profile = profile,
                    parent = Selection.activeTransform is RectTransform ? Selection.activeTransform : null,
                    cancellationToken = cancellationTokenSource.Token,
                    progress = SetProgress
                };

                FigmaApiClient client = new FigmaApiClient(accessToken);
                FigmaFile file = await client.GetFileAsync(fileKey, normalizedNodeId, cancellationTokenSource.Token, SetProgress);

                FigmaToUGUIImporter importer = new FigmaToUGUIImporter(client, settings);
                GameObject imported = await importer.ImportAsync(file);
                status = "Imported " + imported.name + ".";
            }
            catch (OperationCanceledException)
            {
                status = "Import cancelled.";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                status = ex.Message;
            }
            finally
            {
                isImporting = false;
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }

                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void CancelImport()
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                status = "Cancelling import...";
                cancellationTokenSource.Cancel();
                Repaint();
            }
        }

        private void SetProgress(FigmaImportProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            status = progress.title + " - " + progress.detail;
            if (EditorUtility.DisplayCancelableProgressBar(progress.title, progress.detail, Mathf.Clamp01(progress.value)))
            {
                CancelImport();
            }

            Repaint();
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(status, isImporting ? MessageType.None : MessageType.Info);
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(TokenPrefsKey, accessToken);
            EditorPrefs.SetString(FilePrefsKey, fileInput);
            EditorPrefs.SetString(NodePrefsKey, nodeId);
            EditorPrefs.SetString(OutputPrefsKey, outputFolder);
            EditorPrefs.SetBool(UseTextMeshProPrefsKey, useTextMeshPro);
            EditorPrefs.SetBool(ApplyAutoLayoutPrefsKey, applyAutoLayout);
            EditorPrefs.SetBool(ApplyConstraintsPrefsKey, applyConstraints);
            EditorPrefs.SetBool(ReplaceExistingPrefsKey, replaceExistingImport);
            EditorPrefs.SetBool(AddMetadataPrefsKey, addImportMetadata);

            string path = profile != null ? AssetDatabase.GetAssetPath(profile) : string.Empty;
            string guid = !string.IsNullOrEmpty(path) ? AssetDatabase.AssetPathToGUID(path) : string.Empty;
            EditorPrefs.SetString(ProfilePrefsKey, guid);
        }

        private void CreateProfileAsset()
        {
            string folder = string.IsNullOrEmpty(outputFolder) ? "Assets" : outputFolder;
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (folder != "Assets" && !folder.StartsWith("Assets/"))
            {
                folder = "Assets";
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                folder = "Assets";
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/FigmaToUGUIProfile.asset");
            FigmaToUGUIProfile asset = CreateInstance<FigmaToUGUIProfile>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            profile = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void PickOutputFolder()
        {
            string selected = EditorUtility.OpenFolderPanel("Generated sprite folder", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            selected = selected.Replace('\\', '/');
            string assets = Application.dataPath.Replace('\\', '/');
            if (!selected.StartsWith(assets))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Pick a folder inside this Unity project's Assets folder.", "OK");
                return;
            }

            outputFolder = "Assets" + selected.Substring(assets.Length);
        }

        private static string ExtractFileKey(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            input = input.Trim();
            Match match = Regex.Match(input, @"figma\.com/(?:file|design)/([^/?#]+)");
            return match.Success ? match.Groups[1].Value : input;
        }

        private static string ExtractNodeId(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            Match urlNode = Regex.Match(input, @"(?:node-id|node_id)=([^&]+)");
            return urlNode.Success ? urlNode.Groups[1].Value : string.Empty;
        }

        private static string NormalizeNodeId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            Match urlNode = Regex.Match(value, @"(?:node-id|node_id)=([^&]+)");
            if (urlNode.Success)
            {
                value = Uri.UnescapeDataString(urlNode.Groups[1].Value);
            }

            return value.Replace('-', ':');
        }
    }
}
