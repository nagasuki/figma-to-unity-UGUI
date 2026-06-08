using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FigmaToUGUI.Editor
{
    internal sealed class FigmaApiClient
    {
        private const string BaseUrl = "https://api.figma.com/v1";
        private const int ImageBatchSize = 8;

        private readonly string accessToken;

        public FigmaApiClient(string accessToken)
        {
            this.accessToken = accessToken;
        }

        public async Task<FigmaFile> GetFileAsync(string fileKey, string nodeId, CancellationToken cancellationToken, Action<FigmaImportProgress> progress)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, "Downloading Figma file", "Reading file data from Figma...", 0.05f);

            string url;
            if (string.IsNullOrEmpty(nodeId))
            {
                url = BaseUrl + "/files/" + UnityWebRequest.EscapeURL(fileKey);
            }
            else
            {
                url = BaseUrl + "/files/" + UnityWebRequest.EscapeURL(fileKey) +
                      "/nodes?ids=" + UnityWebRequest.EscapeURL(nodeId);
            }

            string json = await GetTextAsync(url, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, "Parsing Figma file", "Preparing imported layers...", 0.15f);
            return FigmaModelParser.ParseFile(json, nodeId);
        }

        public async Task<Dictionary<string, string>> GetNodeImageUrlsAsync(string fileKey, IList<string> nodeIds, float scale, CancellationToken cancellationToken, Action<FigmaImportProgress> progress)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            if (nodeIds == null || nodeIds.Count == 0)
            {
                return results;
            }

            float clampedScale = Mathf.Clamp(scale, 0.01f, 4f);
            for (int start = 0; start < nodeIds.Count; start += ImageBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<string> batch = new List<string>();
                for (int i = start; i < Mathf.Min(start + ImageBatchSize, nodeIds.Count); i++)
                {
                    batch.Add(nodeIds[i]);
                }

                float progressValue = 0.2f + 0.2f * ((float)start / nodeIds.Count);
                Report(progress, "Rendering Figma sprites", "Requesting sprite URLs " + Mathf.Min(start + ImageBatchSize, nodeIds.Count) + "/" + nodeIds.Count + "...", progressValue);
                await RequestNodeImageUrlsWithRetryAsync(fileKey, batch, clampedScale, results, cancellationToken);
            }

            return results;
        }

        private async Task RequestNodeImageUrlsWithRetryAsync(string fileKey, List<string> nodeIds, float scale, Dictionary<string, string> results, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nodeIds == null || nodeIds.Count == 0)
            {
                return;
            }

            try
            {
                await RequestNodeImageUrlsAsync(fileKey, nodeIds, scale, results, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                if (!IsRenderTimeout(ex))
                {
                    throw;
                }

                if (nodeIds.Count > 1)
                {
                    int midpoint = nodeIds.Count / 2;
                    await RequestNodeImageUrlsWithRetryAsync(fileKey, nodeIds.GetRange(0, midpoint), scale, results, cancellationToken);
                    await RequestNodeImageUrlsWithRetryAsync(fileKey, nodeIds.GetRange(midpoint, nodeIds.Count - midpoint), scale, results, cancellationToken);
                    return;
                }

                float nextScale = scale * 0.5f;
                if (nextScale >= 0.25f)
                {
                    await RequestNodeImageUrlsWithRetryAsync(fileKey, nodeIds, nextScale, results, cancellationToken);
                    return;
                }

                Debug.LogWarning("Figma render timed out for node " + nodeIds[0] + ". The importer will keep the UI hierarchy and skip this generated sprite.");
            }
        }

        private async Task RequestNodeImageUrlsAsync(string fileKey, List<string> nodeIds, float scale, Dictionary<string, string> results, CancellationToken cancellationToken)
        {
            string ids = string.Join(",", nodeIds.ToArray());
            string url = BaseUrl + "/images/" + UnityWebRequest.EscapeURL(fileKey) +
                         "?ids=" + UnityWebRequest.EscapeURL(ids) +
                         "&format=png&scale=" + scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            string json = await GetTextAsync(url, cancellationToken);
            Dictionary<string, object> payload = MiniJson.Deserialize(json) as Dictionary<string, object>;
            Dictionary<string, object> images = payload != null && payload.ContainsKey("images")
                ? payload["images"] as Dictionary<string, object>
                : null;

            if (images == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> image in images)
            {
                if (image.Value != null)
                {
                    results[image.Key] = image.Value.ToString();
                }
            }
        }

        public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                await SendAsync(request, cancellationToken);
                return request.downloadHandler.data;
            }
        }

        private async Task<string> GetTextAsync(string url, CancellationToken cancellationToken)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("X-Figma-Token", accessToken);
                await SendAsync(request, cancellationToken);
                return request.downloadHandler.text;
            }
        }

        private static async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

#if UNITY_2020_2_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                throw new InvalidOperationException(request.error + "\n" + request.downloadHandler.text);
            }
        }

        private static bool IsRenderTimeout(Exception ex)
        {
            return ex.Message.IndexOf("Render timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Report(Action<FigmaImportProgress> progress, string title, string detail, float value)
        {
            if (progress != null)
            {
                progress(new FigmaImportProgress(title, detail, value));
            }
        }
    }
}
