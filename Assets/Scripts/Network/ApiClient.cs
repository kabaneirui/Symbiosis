using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Symbiosis.Models;

namespace Symbiosis.Network
{
    public class ApiClient
    {
        private readonly string _baseUrl;

        public string ServerUrl { get { return _baseUrl; } }

        public ApiClient(string baseUrl = "http://127.0.0.1:8000")
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public Task<string> PostRaw(string path, string json)
        {
            var tcs = new TaskCompletionSource<string>();
            string url = _baseUrl + path;

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            var operation = request.SendWebRequest();
            operation.completed += delegate
            {
                if (request.isNetworkError || request.isHttpError)
                    tcs.SetException(new Exception(request.error));
                else
                    tcs.SetResult(request.downloadHandler.text);
                request.Dispose();
            };

            return tcs.Task;
        }

        public Task<string> GetRaw(string path)
        {
            var tcs = new TaskCompletionSource<string>();
            string url = _baseUrl + path;

            var request = UnityWebRequest.Get(url);
            request.timeout = 15;

            var operation = request.SendWebRequest();
            operation.completed += delegate
            {
                if (request.isNetworkError || request.isHttpError)
                    tcs.SetException(new Exception(request.error));
                else
                    tcs.SetResult(request.downloadHandler.text);
                request.Dispose();
            };

            return tcs.Task;
        }

        public Task<UserInitResponse> InitUser(string nickname)
        {
            var req = new UserInitRequest { nickname = nickname };
            return Post<UserInitResponse>("/user/init", req);
        }

        public Task<ChatResponse> Chat(int userId, string message)
        {
            var req = new ChatRequest { user_id = userId, message = message };
            return Post<ChatResponse>("/chat", req);
        }

        public Task<GiftResponse> SendGift(int userId, string giftId)
        {
            var req = new GiftRequest { user_id = userId, gift_id = giftId };
            return Post<GiftResponse>("/gift", req);
        }

        public Task<GiftListResponse> GetGifts()
        {
            return Get<GiftListResponse>("/gifts");
        }

        public Task<StateResponse> GetState(int userId)
        {
            return Get<StateResponse>("/state?user_id=" + userId);
        }

        private Task<T> Post<T>(string path, object body)
        {
            var tcs = new TaskCompletionSource<T>();
            string url = _baseUrl + path;
            string json = JsonUtility.ToJson(body);
            Debug.Log("[API] POST " + url + " → " + json);

            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            var operation = request.SendWebRequest();
            operation.completed += delegate
            {
                if (request.isNetworkError || request.isHttpError)
                {
                    string err = request.error + "\n" + request.downloadHandler.text;
                    Debug.LogError("[API] POST " + path + " 失败: " + err);
                    tcs.SetException(new Exception("API 请求失败: " + request.error));
                }
                else
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log("[API] POST " + path + " ← " + responseText);
                    tcs.SetResult(JsonUtility.FromJson<T>(responseText));
                }
                request.Dispose();
            };

            return tcs.Task;
        }

        private Task<T> Get<T>(string path)
        {
            var tcs = new TaskCompletionSource<T>();
            string url = _baseUrl + path;
            Debug.Log("[API] GET " + url);

            var request = UnityWebRequest.Get(url);
            request.timeout = 15;

            var operation = request.SendWebRequest();
            operation.completed += delegate
            {
                if (request.isNetworkError || request.isHttpError)
                {
                    string err = request.error + "\n" + request.downloadHandler.text;
                    Debug.LogError("[API] GET " + path + " 失败: " + err);
                    tcs.SetException(new Exception("API 请求失败: " + request.error));
                }
                else
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log("[API] GET " + path + " ← " + responseText);
                    tcs.SetResult(JsonUtility.FromJson<T>(responseText));
                }
                request.Dispose();
            };

            return tcs.Task;
        }
    }
}
