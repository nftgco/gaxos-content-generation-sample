using System.Collections.Generic;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using ContentGeneration.Models.ElevenLabs;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.ElevenLabs
{
    public class ElevenLabsSound : VisualElementComponent, IGeneratorVisualElement
    {
        public new class UxmlFactory : UxmlFactory<ElevenLabsSound, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }

        TextField code => this.Q<TextField>("code");
        GenerationOptionsElement generationOptionsElement => this.Q<GenerationOptionsElement>("generationOptions");
        VisualElement requestSent => this.Q<VisualElement>("requestSent");
        VisualElement requestFailed => this.Q<VisualElement>("requestFailed");
        VisualElement sendingRequest => this.Q<VisualElement>("sendingRequest");
        Button generateButton => this.Q<Button>("generateButton");

        PromptInput text => this.Q<PromptInput>("text");
        VisualElement promptRequired => this.Q<VisualElement>("textRequired");
        
        Button improvePrompt => this.Q<Button>("improvePromptButton");

        Toggle sendDuration => this.Q<Toggle>("sendDuration");
        Slider duration => this.Q<Slider>("duration");
        Slider promptInfluence => this.Q<Slider>("promptInfluence");

        public ElevenLabsSound()
        {
            generationOptionsElement.OnCodeHasChanged = RefreshCode;
            
            text.OnChanged += _ => RefreshCode();

            requestSent.style.display = DisplayStyle.None;
            requestFailed.style.display = DisplayStyle.None;
            sendingRequest.style.display = DisplayStyle.None;

            improvePrompt.clicked += () =>
            {
                if (string.IsNullOrEmpty(text.value))
                    return;
                
                if(!improvePrompt.enabledSelf)
                    return;

                improvePrompt.SetEnabled(false);
                text.SetEnabled(false);

                ContentGenerationApi.Instance.ImprovePrompt(text.value, "dalle-3").ContinueInMainThreadWith(
                    t =>
                    {
                        improvePrompt.SetEnabled(true);
                        text.SetEnabled(true);
                        if (t.IsFaulted)
                        {
                            Debug.LogException(t.Exception!.InnerException!);
                            return;
                        }

                        text.value = t.Result;
                    });
            };
            
            sendDuration.RegisterValueChangedCallback(evt =>
            {
                duration.SetEnabled(evt.newValue);
                RefreshCode();
            });
            duration.SetEnabled(sendDuration.value);

            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                if (!IsValid(true)) return;

                requestSent.style.display = DisplayStyle.None;
                requestFailed.style.display = DisplayStyle.None;

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
                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() => ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });

            RefreshCode();
        }

        Task<string> RequestGeneration(bool estimate)
        {
            var parameters = new ElevenLabsSoundParameters
            {
                Text = text.value,
                DurationSeconds = sendDuration.value ? duration.value: null,
                PromptInfluence = promptInfluence.value,
            };
            return ContentGenerationApi.Instance.RequestElevenLabsSoundGeneration(
                parameters,
                generationOptionsElement.GetGenerationOptions(), data: new
                {
                    player_id = ContentGenerationStore.editorPlayerId
                }, estimate:estimate);
        }

        bool IsValid(bool updateUI)
        {
            if (string.IsNullOrEmpty(text.value))
            {
                if(updateUI)
                {
                    promptRequired.style.visibility = Visibility.Visible;
                }
                return false;
            }

            if(updateUI)
            {
                promptRequired.style.visibility = Visibility.Hidden;
            }
            return true;
        }

        void RefreshCode()
        {
            code.value =
                "var requestId = await ContentGenerationApi.Instance.RequestElevenLabsSoundGeneration\n" +
                "\t(new ElevenLabsSoundParameters\n" +
                "\t{\n" +
                $"\t\tPrompt = \"{text.value}\",\n" +
                $"\t\tDurationSeconds = {(sendDuration.value ? duration.value : "null")},\n" +
                $"\t\tPromptInfluence = {promptInfluence.value}f,\n" +
                "\t},\n" +
                $"{generationOptionsElement?.GetCode()}" +
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

        public Generator generator => Generator.ElevenLabsSound;
        public void Show(Favorite favorite)
        {
            var parameters = favorite.GeneratorParameters.ToObject<ElevenLabsSoundParameters>();
            generationOptionsElement.Show(favorite.GenerationOptions);

            text.value = parameters.Text;
            sendDuration.value = parameters.DurationSeconds.HasValue;
            duration.value = parameters.DurationSeconds ?? 11;
            promptInfluence.value = parameters.PromptInfluence;
                
            RefreshCode();
        }
    }
}