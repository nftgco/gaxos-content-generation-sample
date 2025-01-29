using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using ContentGeneration.Models.ElevenLabs;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.ElevenLabs
{
    // https://elevenlabs.io/docs/api-reference/text-to-speech
    public class ElevenLabsTextToSpeech : VisualElementComponent, IGeneratorVisualElement
    {
        public new class UxmlFactory : UxmlFactory<ElevenLabsTextToSpeech, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }

        DropdownField voiceId => this.Q<DropdownField>("voiceId");
        PromptInput text => this.Q<PromptInput>("text");
        Label textRequired => this.Q<Label>("textRequired");
        EnumField outputFormat => this.Q<EnumField>("outputFormat");
        Toggle sendVoiceSettings => this.Q<Toggle>("sendVoiceSettings");
        VisualElement voiceSettings => this.Q<VisualElement>("voiceSettings");
        FloatField stability => this.Q<FloatField>("stability");
        FloatField similarityBoost => this.Q<FloatField>("similarityBoost");
        FloatField styleField => this.Q<FloatField>("style");
        Toggle useSpeakerBoost => this.Q<Toggle>("useSpeakerBoost");

        IntegerField seed => this.Q<IntegerField>("seed");
        TextField previousText => this.Q<TextField>("previousText");
        TextField nextText => this.Q<TextField>("nextText");
        TextField previousRequestIds => this.Q<TextField>("previousRequestIds");
        TextField nextRequestIds => this.Q<TextField>("nextRequestIds");
        EnumField applyTextNormalization => this.Q<EnumField>("applyTextNormalization");

        VisualElement requestSent => this.Q<VisualElement>("requestSent");
        VisualElement requestFailed => this.Q<VisualElement>("requestFailed");
        VisualElement sendingRequest => this.Q<VisualElement>("sendingRequest");
        Button generateButton => this.Q<Button>("generateButton");
        TextField code => this.Q<TextField>("code");

        public ElevenLabsTextToSpeech()
        {
            requestSent.style.display = DisplayStyle.None;
            requestFailed.style.display = DisplayStyle.None;
            sendingRequest.style.display = DisplayStyle.None;

            generateButton.SetEnabled(false);
            GetVoices().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogException(t.Exception!.GetBaseException());
                }
            });

            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                if (!IsValid(true)) return;

                requestSent.style.display = DisplayStyle.None;
                requestFailed.style.display = DisplayStyle.None;
                sendingRequest.style.display = DisplayStyle.None;

                generateButton.SetEnabled(false);
                sendingRequest.style.display = DisplayStyle.Flex;

                RequestGeneration(false).ContinueInMainThreadWith(
                    t =>
                    {
                        generateButton.SetEnabled(true);
                        sendingRequest.style.display = DisplayStyle.None;
                        if (t.IsFaulted)
                        {
                            requestFailed.style.display = DisplayStyle.Flex;
                            Debug.LogException(t.Exception);
                        }
                        else
                        {
                            requestSent.style.display = DisplayStyle.Flex;
                        }

                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() =>
                            ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });

            voiceId.RegisterValueChangedCallback(VoiceIdHasChanged);
            text.OnChanged += _ => RefreshCode();
            textRequired.RegisterValueChangedCallback(_ => RefreshCode());
            outputFormat.RegisterValueChangedCallback(_ => RefreshCode());
            sendVoiceSettings.RegisterValueChangedCallback(v => RefreshVoiceSettings(v.newValue));
            stability.RegisterValueChangedCallback(_ => RefreshCode());
            similarityBoost.RegisterValueChangedCallback(_ => RefreshCode());
            styleField.RegisterValueChangedCallback(_ => RefreshCode());
            useSpeakerBoost.RegisterValueChangedCallback(_ => RefreshCode());

            seed.RegisterValueChangedCallback(_ => RefreshCode());
            previousText.RegisterValueChangedCallback(_ => RefreshCode());
            nextText.RegisterValueChangedCallback(_ => RefreshCode());
            previousRequestIds.RegisterValueChangedCallback(_ => RefreshCode());
            nextRequestIds.RegisterValueChangedCallback(_ => RefreshCode());
            applyTextNormalization.RegisterValueChangedCallback(_ => RefreshCode());

            RefreshVoiceSettings(sendVoiceSettings.value);
        }

        void VoiceIdHasChanged(ChangeEvent<string> evt)
        {
            generateButton.SetEnabled(voiceId.index >= 0);
            RefreshCode();
        }

        Task<string> RequestGeneration(bool estimate)
        {
            var parameters = new ElevenLabsTextToSpeechParameters
            {
                VoiceID = _voiceIds[voiceId.index],
                OutputFormat = (OutputFormat)outputFormat.value,
                Text = text.value,
                Seed = seed.value,
                PreviousText = previousText.value,
                NextText = nextText.value,
                PreviousRequestIds = previousRequestIds.value.Split(','),
                NextRequestIds = nextRequestIds.value.Split(','),
                ApplyTextNormalization = (TextNormalization)applyTextNormalization.value
            };
            if (sendVoiceSettings.value)
            {
                parameters.VoiceSettings = new VoiceSettings
                {
                    Stability = stability.value,
                    SimilarityBoost = similarityBoost.value,
                    Style = styleField.value,
                    UseSpeakerBoost = useSpeakerBoost.value,
                };
            }

            return ContentGenerationApi.Instance.RequestElevenLabsTextToSpeechGeneration(
                parameters,
                data: new
                {
                    player_id = ContentGenerationStore.editorPlayerId
                }, estimate:estimate);
        }

        bool IsValid(bool updateUI)
        {
            if(updateUI)
            {
                textRequired.style.visibility = Visibility.Hidden;
            }
            if (string.IsNullOrEmpty(text.value))
            {
                if(updateUI)
                {
                    textRequired.style.visibility = Visibility.Visible;
                }
                return false;
            }

            return true;
        }

        void RefreshVoiceSettings(bool v)
        {
            voiceSettings.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshCode();
        }

        readonly List<string> _voiceIds = new();

        async Task GetVoices()
        {
            var voices = await SendGetRequest("https://api.elevenlabs.io/v1/voices");
            voiceId.choices.Clear();
            _voiceIds.Clear();
            foreach (var voice in voices.GetValue("voices")!)
            {
                voiceId.choices.Add(voice["name"]!.ToObject<string>());
                _voiceIds.Add(voice["voice_id"]!.ToObject<string>());
            }

            RefreshVoiceSettings(sendVoiceSettings.value);
        }

        Task<JObject> SendGetRequest(string url)
        {
            var ret = new TaskCompletionSource<JObject>();
            Dispatcher.instance.StartCoroutine(SendRequestCo(url, ret));
            return ret.Task;
        }

        IEnumerator SendRequestCo(string url, TaskCompletionSource<JObject> ret)
        {
            var www = new UnityWebRequest(url);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                ret.SetException(new ContentGenerationApiException(www, null, null));
                yield break;
            }

            ret.SetResult(JObject.Parse(www.downloadHandler.text));
        }

        void RefreshCode()
        {
            if(_voiceIds.Count == 0 || voiceId.index < 0)
                return;
            
            var parameters =
                    $"\t\tVoiceID = {_voiceIds[voiceId.index]},\n" +
                    $"\t\tOutputFormat = OutputFormat.${outputFormat.value},\n" +
                    $"\t\tText = \"${text.value}\",\n" +
                    $"\t\tSeed = ${seed.value},\n" +
                    $"\t\tPreviousText = \"${previousText.value}\",\n" +
                    $"\t\tNextText = \"${nextText.value}\",\n" +
                    $"\t\tPreviousRequestIds = [{string.Join(',', previousRequestIds.value.Split(',').Select(i => $"\"{i}\""))}],\n" +
                    $"\t\tNextRequestIds = [{string.Join(',', nextRequestIds.value.Split(',').Select(i => $"\"{i}\""))}],\n" +
                    $"\t\tApplyTextNormalization = TextNormalization.${applyTextNormalization.value},\n"
                ;
            if (sendVoiceSettings.value)
            {
                parameters +=
                    $"\t\tVoiceSettings = new VoiceSettings\n" +
                    $"\t\t{{\n" +
                    $"\t\t\tStability = {stability.value}f,\n" +
                    $"\t\t\tSimilarityBoost = {similarityBoost.value}f,\n" +
                    $"\t\t\tStyle = {styleField.value}f,\n" +
                    $"\t\t\tUseSpeakerBoost = {useSpeakerBoost.value},\n" +
                    $"\t\t}},\n";
            }

            code.value =
                "var requestId = await ContentGenerationApi.Instance.RequestElevenLabsTextToSpeechGeneration\n" +
                "\t(new ElevenLabsTextToSpeechParameters\n" +
                "\t{\n" +
                parameters +
                "\t}\n" +
                ")";

            if (IsValid(false))
            {
                generateButton.text = "Generate [...]";
                CostEstimation.WillRequestEstimation(() => RequestGeneration(true)).ContinueInMainThreadWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Debug.LogException(t.Exception!.GetBaseException());
                        return;
                    }

                    if(!string.IsNullOrEmpty(t.Result))
                        generateButton.text = $"Generate [estimated cost: {t.Result}]";
                });
            }
        }

        public Generator generator => Generator.ElevenLabsTextToSpeech;

        public void Show(Favorite favorite)
        {
            var parameters = favorite.GeneratorParameters.ToObject<ElevenLabsTextToSpeechParameters>();

            voiceId.index = _voiceIds.IndexOf(parameters.VoiceID);
            outputFormat.value = parameters.OutputFormat;
            text.value = parameters.Text;
            seed.value = parameters.Seed;
            previousText.value = parameters.PreviousText;
            nextText.value = parameters.NextText;
            previousRequestIds.value = string.Join(',', parameters.PreviousRequestIds);
            nextRequestIds.value = string.Join(',', parameters.NextRequestIds);
            applyTextNormalization.value = parameters.ApplyTextNormalization;

            sendVoiceSettings.value = parameters.VoiceSettings != null;
            if (parameters.VoiceSettings != null)
            {
                stability.value = parameters.VoiceSettings.Stability;
                similarityBoost.value = parameters.VoiceSettings.SimilarityBoost;
                styleField.value = parameters.VoiceSettings.Style;
                useSpeakerBoost.value = parameters.VoiceSettings.UseSpeakerBoost;
            }

            RefreshVoiceSettings(sendVoiceSettings.value);
        }
    }
}