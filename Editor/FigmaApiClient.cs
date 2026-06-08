using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FigmaToUGUI.Editor
{
    internal sealed class FigmaApiClient
    {
        private const string BaseUrl = "https://api.figma.com/v1";

        private readonly string accessToken;

        public FigmaApiClient(string accessToken)
        {
            this.accessToken = accessToken;
        }

        public async Task<FigmaFile> GetFileAsync(string fileKey, string nodeId)
        {
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

            string json = await GetTextAsync(url);
            return FigmaModelParser.ParseFile(json, nodeId);
        }

        public async Task<Dictionary<string, string>> GetNodeImageUrlsAsync(string fileKey, IList<string> nodeIds, float scale)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            if (nodeIds == null || nodeIds.Count == 0)
            {
                return results;
            }

            const int batchSize = 40;
            for (int start = 0; start < nodeIds.Count; start += batchSize)
            {
                List<string> batch = new List<string>();
                for (int i = start; i < Mathf.Min(start + batchSize, nodeIds.Count); i++)
                {
                    batch.Add(nodeIds[i]);
                }

                string ids = string.Join(",", batch.ToArray());
                string url = BaseUrl + "/images/" + UnityWebRequest.EscapeURL(fileKey) +
                             "?ids=" + UnityWebRequest.EscapeURL(ids) +
                             "&format=png&scale=" + Mathf.Clamp(scale, 0.01f, 4f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                string json = await GetTextAsync(url);
                Dictionary<string, object> payload = MiniJson.Deserialize(json) as Dictionary<string, object>;
                Dictionary<string, object> images = payload != null && payload.ContainsKey("images")
                    ? payload["images"] as Dictionary<string, object>
                    : null;

                if (images == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, object> image in images)
                {
                    if (image.Value != null)
                    {
                        results[image.Key] = image.Value.ToString();
                    }
                }
            }

            return results;
        }

        public async Task<byte[]> DownloadBytesAsync(string url)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                await SendAsync(request);
                return request.downloadHandler.data;
            }
        }

        private async Task<string> GetTextAsync(string url)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("X-Figma-Token", accessToken);
                await SendAsync(request);
                return request.downloadHandler.text;
            }
        }

        private static async Task SendAsync(UnityWebRequest request)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
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
    }
}
