using System;
using System.Linq;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using QueryParameters = ContentGeneration.Models.QueryParameters;

namespace ContentGeneration.Generators
{
    public abstract class Generator : MonoBehaviour
    {
        [SerializeField] TMP_InputField _inputField;
        [SerializeField] TMP_Text _label;
        [SerializeField] Button _button;

        string generationIdPlayerPrefKey => $"{gameObject.name}_{GetType().Name}_contentGeneratorKey";
        protected string assetUrlPlayerPrefKey => $"{gameObject.name}_{GetType().Name}_assetUrl";

        void Awake()
        {
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(generationIdPlayerPrefKey)))
            {
                ShowStatus("Generating...");
            }

            var generatedAsset = PlayerPrefs.GetString(assetUrlPlayerPrefKey);
            if (!string.IsNullOrEmpty(generatedAsset))
            {
                Enable(false);
                enabled = false;
                ReportGeneration(JsonConvert.DeserializeObject<PublishedAsset>(generatedAsset)).Finally(() =>
                {
                    Enable(true);
                    enabled = true;
                });
            }

            _button.onClick.AddListener(() =>
            {
                Enable(false);
                enabled = false;
                ShowStatus("Requesting...");
                RequestGeneration(_inputField.text).ContinueInMainThreadWith(t =>
                {
                    enabled = true;
                    if (t.IsFaulted)
                    {
                        ShowStatus($"Error: {t.Exception!.Message}");
                        OnError?.Invoke(t.Exception!.Message);
                        Enable(true);
                    }
                    else
                    {
                        ShowStatus("Generating...");
                        PlayerPrefs.SetString(generationIdPlayerPrefKey, t.Result);
                    }
                });
            });
        }

        protected abstract Task<string> RequestGeneration(string prompt);

        void ShowStatus(string text)
        {
            _label.text = text;
            _label.gameObject.SetActive(true);
        }

        void Enable(bool enable)
        {
            _inputField.interactable = enable;
            _button.interactable = enable;
        }

        bool _refreshing;

        void Update()
        {
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(generationIdPlayerPrefKey)))
            {
                Enable(false);
                if (!_refreshing)
                {
                    _refreshing = true;
                    Refresh().ContinueInMainThreadWith((t) =>
                    {
                        if (t.IsFaulted)
                        {
                            Debug.LogException(t.Exception);
                            PlayerPrefs.DeleteKey(generationIdPlayerPrefKey);
                        }

                        _refreshing = false;
                    });
                }
            }
            else
            {
                Enable(true);
            }
        }

        [Serializable]
        public class UnityEventError : UnityEvent<string>
        {
        }
        [SerializeField] UnityEventError OnError;
        async Task Refresh()
        {
            var id = PlayerPrefs.GetString(generationIdPlayerPrefKey);

            var result = await ContentGenerationApi.Instance.GetRequest(id);
            if (result == null)
            {
                PlayerPrefs.DeleteKey(generationIdPlayerPrefKey);
            }
            else if (await IsRequestGenerated(result))
            {
                await RequestWasGenerated(id, result);
            }
            else if (result.Status == RequestStatus.Failed)
            {
                ShowStatus($"Error: {result.GeneratorError.Message}");
                OnError?.Invoke(result.GeneratorError.Message);
                await ContentGenerationApi.Instance.DeleteRequest(id);
            }

            await Task.Delay(3000);
        }

        protected virtual Task<bool> IsRequestGenerated(Request result)
        {
            return Task.FromResult(result.Status == RequestStatus.Generated);
        }

        async Task RequestWasGenerated(string id, Request result)
        {
            ShowStatus("Generated!");
            ReportRequestWasJustGenerated(result);
            if(result.Assets == null || result.Assets.Length == 0)
            {
                await ContentGenerationApi.Instance.DeleteRequest(id);
                return;
            }
            
            await ContentGenerationApi.Instance.MakeAssetPublic(id, result.Assets[0].ID, true);
            await ContentGenerationApi.Instance.DeleteRequest(id);
            var publishedAssets = await ContentGenerationApi.Instance.GetPublishedAssets(new QueryParameters()
            {
                Sort = new[]
                {
                    new QueryParameters.SortBy
                    {
                        Target = QueryParameters.SortTarget.CreatedAt,
                        Direction = QueryParameters.SortDirection.Descending
                    }
                }
            });
            var publishedAsset = publishedAssets.First(i => i.Request.ID == id);
            await ReportGeneration(publishedAsset);
            PlayerPrefs.SetString(assetUrlPlayerPrefKey, JsonConvert.SerializeObject(publishedAsset));
            PlayerPrefs.DeleteKey(generationIdPlayerPrefKey);
        }

        protected virtual void ReportRequestWasJustGenerated(Request request)
        {
        }

        protected abstract Task ReportGeneration(PublishedAsset asset);
    }
}