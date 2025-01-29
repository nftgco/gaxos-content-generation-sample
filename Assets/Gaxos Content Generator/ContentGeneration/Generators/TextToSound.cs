using System;
using System.Threading.Tasks;
using ContentGeneration.Models;
using ContentGeneration.Models.ElevenLabs;
using UnityEngine;
using UnityEngine.Events;

namespace ContentGeneration.Generators
{
    public class TextToSound : Generator
    {
        protected override Task<string> RequestGeneration(string prompt)
        {
            return ContentGenerationApi.Instance.RequestElevenLabsSoundGeneration(new ElevenLabsSoundParameters
            {
                Text = prompt
            });
        }

        [Serializable]
        class UnityEventUrl : UnityEvent<string>
        {
        }

        [SerializeField] UnityEventUrl _soundUrl;

        protected override void ReportRequestWasJustGenerated(Request request)
        {
            _soundUrl?.Invoke(request.GeneratorResult["url"]!.ToObject<string>());
            base.ReportRequestWasJustGenerated(request);
        }

        protected override Task ReportGeneration(PublishedAsset asset)
        {
            PlayerPrefs.DeleteKey(assetUrlPlayerPrefKey);
            return Task.CompletedTask;
        }
    }
}